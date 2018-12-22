#!/usr/bin/env python
# -*- coding: utf-8 -*-

import sys
sys.path.append("C:\\Python27\\Lib")
sys.path.append("C:\\Python27\\Lib\\site-packages")
import urllib
import time
import atexit
import datetime
import itertools
import json
import logging
import random
import signal
import sys
import time
import requests
import re

from fake_useragent import UserAgent

class InstaRogi:
    # linki
    url = 'https://www.instagram.com/'
    url_tag = 'https://www.instagram.com/explore/tags/%s/?__a=1'
    url_location = 'https://www.instagram.com/explore/locations/%s/?__a=1'
    url_likes = 'https://www.instagram.com/web/likes/%s/like/'
    url_unlike = 'https://www.instagram.com/web/likes/%s/unlike/'
    url_comment = 'https://www.instagram.com/web/comments/%s/add/'
    url_follow = 'https://www.instagram.com/web/friendships/%s/follow/'
    url_unfollow = 'https://www.instagram.com/web/friendships/%s/unfollow/'
    url_login = 'https://www.instagram.com/accounts/login/ajax/'
    url_logout = 'https://www.instagram.com/accounts/logout/'
    url_media_detail = 'https://www.instagram.com/p/%s/?__a=1'
    url_user_detail = 'https://www.instagram.com/%s/'
    api_user_detail = 'https://i.instagram.com/api/v1/users/%s/info/'

    # potrzebne dla headers
    user_agent = "" ""
    accept_language = 'en-US,en;q=0.5'

    # Last photo URL
    last_photo_url = 'ada'
    last_media_id = ''
    last_media_id_url = ''
    last_username = ''

    # logowanie
    user_login = ''
    user_password = ''
    
    def __init__(self):

        fake_ua = UserAgent()
        self.user_agent = fake_ua.random

        self.s = requests.Session()

        #self.user_login = login.lower()
        #self.user_password = password

        now_time = datetime.datetime.now()
        self.log_mod = 0

        #self.login()

        signal.signal(signal.SIGTERM, self.cleanup)
        atexit.register(self.cleanup)

    def test(self, n):
        print(self.last_photo_url + " | " + self.last_media_id)

    def login(self):
        log_string = 'Trying to login as %s...\n' % (self.user_login)
        self.write_log(log_string)
        self.login_post = {
            'username': self.user_login,
            'password': self.user_password
        }
        print("login:"+self.user_login+"|password:"+self.user_password)

        self.s.headers.update({
            'Accept': '*/*',
            'Accept-Language': self.accept_language,
            'Accept-Encoding': 'gzip, deflate, br',
            'Connection': 'keep-alive',
            'Content-Length': '0',
            'Host': 'www.instagram.com',
            'Origin': 'https://www.instagram.com',
            'Referer': 'https://www.instagram.com/',
            'User-Agent': self.user_agent,
            'X-Instagram-AJAX': '1',
            'Content-Type': 'application/x-www-form-urlencoded',
            'X-Requested-With': 'XMLHttpRequest'
        })

        r = self.s.get(self.url)
        self.s.headers.update({'X-CSRFToken': r.cookies['csrftoken']})
        time.sleep(5 * random.random())
        login = self.s.post(
            self.url_login, data=self.login_post, allow_redirects=True)
        self.s.headers.update({'X-CSRFToken': login.cookies['csrftoken']})
        self.csrftoken = login.cookies['csrftoken']
        #ig_vw=1536; ig_pr=1.25; ig_vh=772;  ig_or=landscape-primary;
        self.s.cookies['ig_vw'] = '1536'
        self.s.cookies['ig_pr'] = '1.25'
        self.s.cookies['ig_vh'] = '772'
        self.s.cookies['ig_or'] = 'landscape-primary'
        time.sleep(5 * random.random())

        if login.status_code == 200:
            self.write_log("Przed getem...")
            r = self.s.get('https://www.instagram.com/')
            finder = r.content.find(self.user_login)
            if finder != -1:
                ui = UserInfo()
                self.user_id = ui.get_user_id_by_login(self.user_login)
                self.login_status = True
                log_string = '%s login success!' % (self.user_login)
                self.write_log(log_string)
            else:
                self.login_status = False
                self.write_log('Login error! Check your login data!')
        else:
            self.write_log('Login error! Connection error!')

    def logout(self):
        now_time = datetime.datetime.now()
        log_string = 'Trying to logout...'
        self.write_log(log_string)
        try:
            logout_post = {'csrfmiddlewaretoken': self.csrftoken}
            logout = self.s.post(self.url_logout, data=logout_post)
            self.write_log("Logout success!")
            self.login_status = False
        except:
            logging.exception("Logout error!")

    def get_username_by_media_id(self, media_id):
        """ Get username by media ID Thanks to Nikished """

        if self.login_status:
            if self.login_status == 1:
                self.last_media_id_url = self.get_instagram_url_from_media_id(int(media_id), only_code=True)
                url_media = self.url_media_detail % (self.last_media_id_url)
                try:
                    r = self.s.get(url_media)
                    all_data = json.loads(r.text)
                    self.last_username = str(all_data['graphql']['shortcode_media']['owner']['username'])
                    self.write_log("media_id=" + media_id + ", media_id_url=" +
                                   self.last_media_id_url + ", username_by_media_id=" + self.last_username)
                    return self.last_username
                except:
                    logging.exception("username_by_mediaid exception")
                    return False
            else:
                return ""
    def get_instagram_url_from_media_id(self, media_id, url_flag=True, only_code=None):
        """ Get Media Code or Full Url from Media ID Thanks to Nikished """
        media_id = int(media_id)
        if url_flag is False: return ""
        else:
            alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_'
            shortened_id = ''
            while media_id > 0:
                media_id, idx = divmod(media_id, 64)
                shortened_id = alphabet[idx] + shortened_id
            if only_code: return shortened_id
            else: return 'instagram.com/p/' + shortened_id + '/'

    def like(self, media_id):
        """ Send http request to like media by ID """
        if self.login_status:
            url_likes = self.url_likes % (media_id)
            try:
                like = self.s.post(url_likes)
                last_liked_media_id = media_id
            except:
                logging.exception("Except on like!")
                like = 0
            return like

    def comment(self, media_id, comment_text):
        """ Send http request to comment """
        if self.login_status:
            comment_post = {'comment_text': comment_text}
            url_comment = self.url_comment % (media_id)
            try:
                comment = self.s.post(url_comment, data=comment_post)
                return comment
            except:
                logging.exception("Except on comment!")
        return False

    def check_exisiting_comment(self, media_code):
        url_check = self.url_media_detail % (media_code)
        check_comment = self.s.get(url_check)
        if check_comment.status_code == 200:
            all_data = json.loads(check_comment.text)
            if all_data['graphql']['shortcode_media']['owner']['id'] == self.user_id:
                self.write_log("Keep calm - It's your own media ;)")
                # Del media to don't loop on it
                del self.media_by_tag[0]
                return True
            comment_list = list(all_data['graphql']['shortcode_media']['edge_media_to_comment']['edges'])
            for d in comment_list:
                if d['node']['owner']['id'] == self.user_id:
                    self.write_log("Keep calm - Media already commented ;)")
                    # Del media to don't loop on it
                    del self.media_by_tag[0]
                    return True
            return False
        else:
            insert_media(self, self.media_by_tag[0]['node']['id'], str(check_comment.status_code))
            self.media_by_tag.remove(self.media_by_tag[0])
            return False

    def get_media_id_by_url(self, url):
        _rul = 'https://api.instagram.com/oembed/?url=' + url
        req = requests.get(unicode(_rul))
        parsed = json.loads(unicode(req.content))
        self.last_media_id = parsed['media_id']

    def get_raw_photo_by_url(self, url, file_path):
        media_code = url + "?__a=1"
        #url_check = self.url_media_detail % (media_code)
        url_check = media_code
        photo_page = self.s.get(url_check)
        if photo_page.status_code == 200:
            all_data = json.loads(unicode(photo_page.content))
            last_photo_url = all_data['graphql']['shortcode_media']['display_url']
            media_code = all_data['graphql']['shortcode_media']['shortcode']
            if last_photo_url.find(".jpg") > 0:
                extension = ".jpg"
            elif last_photo_url.find(".png") > 0:
                extension = ".png"
            file_name = unicode(file_path) + media_code + extension
            urllib.urlretrieve(last_photo_url, file_name)
        else:
            print("Insta nie zwrocil response.status == 200")

    def write_log(self, log_text):
        """ Write log by print() or logger """

        if self.log_mod == 0:
            try:
                now_time = datetime.datetime.now()
                print(now_time.strftime("%d.%m.%Y_%H:%M")  + " " + log_text)
            except UnicodeEncodeError:
                print("Your text has unicode problem!")
        elif self.log_mod == 1:
            # Create log_file if not exist.
            if self.log_file == 0:
                self.log_file = 1
                now_time = datetime.datetime.now()
                self.log_full_path = '%s%s_%s.log' % (
                    self.log_file_path, self.user_login,
                    now_time.strftime("%d.%m.%Y_%H:%M"))
                formatter = logging.Formatter('%(asctime)s - %(name)s '
                                              '- %(message)s')
                self.logger = logging.getLogger(self.user_login)
                self.hdrl = logging.FileHandler(self.log_full_path, mode='w')
                self.hdrl.setFormatter(formatter)
                self.logger.setLevel(level=logging.INFO)
                self.logger.addHandler(self.hdrl)
            # Log to log file.
            try:
                self.logger.info(log_text)
            except UnicodeEncodeError:
                print("Your text has unicode problem!")

    def media_id_excluder(self, media_id):
        new_media_id = ''
        for c in media_id:
            if c != '_':
                new_media_id = new_media_id + c;
            else:
                break
        return new_media_id

    def safe_unicode(obj, *args):
        """ return the unicode representation of obj """
        try:
            return unicode(obj, *args)
        except UnicodeDecodeError:
            # obj is byte string
            ascii_text = str(obj).encode('string_escape')
            return unicode(ascii_text)

    def safe_str(obj):
        """ return the byte string representation of obj """
        try:
            return str(obj)
        except UnicodeEncodeError:
            # obj is unicode
            return unicode(obj).encode('unicode_escape')

    def cleanup(self, *_):
        # Logout
        if self.login_status:
            self.logout()

class UserInfo:
    '''
    This class try to take some user info (following, followers, etc.)
    '''
    user_agent = ("Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 "
                  "(KHTML, like Gecko) Chrome/48.0.2564.103 Safari/537.36")
    url_user_info = "https://www.instagram.com/%s/"
    url_list = {
        "ink361": {
            "main": "http://ink361.com/",
            "user": "http://ink361.com/app/users/%s",
            "search_name": "https://data.ink361.com/v1/users/search?q=%s",
            "search_id": "https://data.ink361.com/v1/users/ig-%s",
            "followers": "https://data.ink361.com/v1/users/ig-%s/followed-by",
            "following": "https://data.ink361.com/v1/users/ig-%s/follows",
            "stat": "http://ink361.com/app/users/ig-%s/%s/stats"
        }
    }

    def __init__(self, info_aggregator="ink361"):
        self.i_a = info_aggregator
        print("\nsaid hello\n")
        self.hello()

    def hello(self):
        self.s = requests.Session()
        self.s.headers.update({'User-Agent': self.user_agent})
        main = self.s.get(self.url_list[self.i_a]["main"])
        if main.status_code == 200:
            return True
        return False

    def get_user_id_by_login(self, user_name):
        url_info = self.url_user_info % (user_name)
        info = self.s.get(url_info)
        json_info = json.loads(re.search('window._sharedData = (.*?);</script>', info.text, re.DOTALL).group(1))
        id_user = json_info['entry_data']['ProfilePage'][0]['graphql']['user']['id']
        return id_user

    def search_user(self, user_id=None, user_name=None):
        '''
        Search user_id or user_name, if you don't have it.
        '''
        self.user_id = user_id or False
        self.user_name = user_name or False

        if not self.user_id and not self.user_name:
            # you have nothing
            return False
        elif self.user_id:
            # you have just id
            search_url = self.url_list[self.i_a]["search_id"] % self.user_id
        elif self.user_name:
            # you have just name
            search_url = self.url_list[self.i_a][
                "search_name"] % self.user_name
        else:
            # you have id and name
            return True

        search = self.s.get(search_url)

        if search.status_code == 200:
            r = json.loads(search.text)
            if self.user_id:
                # you have just id
                self.user_name = r["data"]["username"]
            else:
                for u in r["data"]:
                    if u["username"] == self.user_name:
                        t = u["id"].split("-")
                        self.user_id = t[1]
                # you have just name
            return True
        return False

    def get_followers(self, limit=-1):
        self.followers = None
        self.followers = []
        if self.user_id:
            next_url = self.url_list[self.i_a]["followers"] % self.user_id
            while True:
                followers = self.s.get(next_url)
                r = json.loads(followers.text)
                for u in r["data"]:
                    if limit > 0 or limit < 0:
                        self.followers.append({
                            "username": u["username"],
                            #"profile_picture": u["profile_picture"],
                            "id": u["id"].split("-")[1],
                            #"full_name": u["full_name"]
                        })
                        limit -= 1
                    else:
                        return True
                if r["pagination"]["next_url"]:
                    # have more data
                    next_url = r["pagination"]["next_url"]
                else:
                    # end of data
                    return True
        return False

    def get_following(self, limit=-1):
        self.following = None
        self.following = []
        if self.user_id:
            next_url = self.url_list[self.i_a]["following"] % self.user_id
            while True:
                following = self.s.get(next_url)
                r = json.loads(following.text)
                for u in r["data"]:
                    if limit > 0 or limit < 0:
                        self.following.append({
                            "username": u["username"],
                            #"profile_picture": u["profile_picture"],
                            "id": u["id"].split("-")[1],
                            #"full_name": u["full_name"]
                        })
                        limit -= 1
                    else:
                        return True
                if r["pagination"]["next_url"]:
                    # have more data
                    next_url = r["pagination"]["next_url"]
                else:
                    # end of data
                    return True
        return False

    def get_stat(self, limit):
        # todo
        return False
#InstaEngine = InstaRogi()
#InstaEngine.user_login = "martha_farewell"
#InstaEngine.user_password = "rooney892"
#InstaEngine.login()
#InstaEngine.like(1913492838298014674)
#InstaEngine.comment(1913492838298014674, "test")
#print(InstaEngine.get_username_by_media_id(InstaEngine.media_id_excluder(InstaEngine.get_media_id_by_url("https://www.instagram.com/p/BqOF5krAdPS/"))))
#print(InstaEngine.get_raw_photo_by_url(InstaEngine.last_media_id_url))