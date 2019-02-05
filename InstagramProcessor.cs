using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Web;
using Microsoft.Scripting.Hosting;
using IronPython;
using IronPython.Hosting;
using IronPython.Modules;
using InstaSharper.API;
using InstaSharper.Classes;
using InstaSharper.API.Builder;
using InstaSharper.Logger;
using System.Collections.Specialized;
using InstaSharper.Classes.Models;
using InstaSharper.Classes.ResponseWrappers;

namespace IGTomesheqAutoLiker
{
    public static class IGProc
    {
        private static IInstaApi _instaApi;

        public static string login;
        public static string password;

        // po podaniu adresu postu na insta - znajduje ID postu
        public async static Task<string> GetMediaIdFromUrl(string url)
        {
            Uri urli = new Uri(url);
            var test = await _instaApi.GetMediaIdFromUrlAsync(urli);
            return test.Value;
        }

        // loguje użytkownika do instagrama
        public async static Task<InstaLoginResult> Login(string username, string password)
        {
            // przechowuje wyniki zapytan
            IResult<InstaLoginResult> logInResult;
            InstaLoginResult ret;

            // ustawienie loginu i hasla
            var userSession = new UserSessionData
            {
                UserName = username,
                Password = password
            };

            // utworzenie objektu komunikacji z insta - wykorzystywany rowniez w innych metodach
            IRequestDelay delay = RequestDelay.Empty();
            _instaApi = InstaApiBuilder.CreateBuilder()
                    .SetUser(userSession)
                    .UseLogger(new DebugLogger(LogLevel.All)) // use logger for requests and debug messages
                    .SetRequestDelay(delay)
                    .Build();

            // jesli uzytkownik jeszcze niezalogowany
            if (!_instaApi.IsUserAuthenticated)
            {
                // login
                Console.WriteLine($"Logging in as {userSession.UserName}");
                logInResult = await _instaApi.LoginAsync();
                ret = logInResult.Value;
                if (logInResult.Value == InstaLoginResult.ChallengeRequired)
                {
                    login = username;
                    var lola = await _instaApi.ResetChallenge();
                    var res = await _instaApi.ChooseVerifyMethod(1);
                }
                if (!logInResult.Succeeded)
                {
                    Console.WriteLine($"Unable to login: {logInResult.Info.Message}");
                    ret = InstaLoginResult.Exception;
                }
            }
            else // jesli juz zalogowany
            {
                login = username;
                ret = InstaLoginResult.Success;
            }

            // zwraca wartosc
            return ret;
        }

        public async static Task<bool> FindMyComment(string post_id, string current_username)
        {
            IResult<InstaCommentList> comments = await _instaApi.GetMediaCommentsAsync(post_id, PaginationParameters.Empty);
            if (comments.Value.Comments.Any(x => x.User.UserName == current_username))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async static Task<InstaCommentList> GetComments(string post_id)
        {
            IResult<InstaCommentList> comments = await _instaApi.GetMediaCommentsAsync(post_id, PaginationParameters.Empty);
            return comments.Value;
        }

        public async static Task<bool> Logout()
        {
            IResult<bool> logout_successful = await _instaApi.LogoutAsync();
            return logout_successful.Value;
        }

        public async static Task CheckSomething()
        {
            if (_instaApi.IsUserAuthenticated)
            {
                IResult<InstaMedia> mediaItem = await _instaApi.GetMediaByIdAsync("1918376805137779938");
                //System.Diagnostics.Debug.Write("\nUser zalogowany!\nUser: " + user.Value.FullName);
            }
            else
            {
                System.Diagnostics.Debug.Write("\nUser niezalogowany...");
            }
        }

        public async static Task Like(string post_id)
        {
            await _instaApi.LikeMediaAsync(post_id);
        }

        public async static Task Comment(string post_id, string comment_text)
        {
            await _instaApi.CommentMediaAsync(post_id, comment_text);
        }

        public static bool IsUserAuthenticated()
        {
            if (_instaApi != null)
            {
                if (_instaApi.IsUserAuthenticated)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async static Task<InstaResetChallenge> SendVerifyCode(string code)
        {
            IResult<InstaResetChallenge> res = await _instaApi.SendVerifyCode(code);
            return res.Value;
        }

        public async static Task<IResult<InstaMedia>> GetInstaPost(string id)
        {
            IResult<InstaMedia> post = await _instaApi.GetMediaByIdAsync(id);
            return post;
        }
    }

    class InstagramProcessor
    {
        private string login;
        public string Login
        {
            get { return login; }
            set { login = value; }
        }
        private string password;
        public string Password
        {
            get { return password; }
            set { password = value; }
        }

        public InstagramProcessor()
        {

        }

        public void SaveImage(string url, string filename, ImageFormat format)
        {

            WebClient client = new WebClient();
            Stream stream = client.OpenRead(url);
            Bitmap bitmap = new Bitmap(stream);

            if (bitmap != null)
                bitmap.Save(filename, format);

            stream.Flush();
            stream.Close();
            client.Dispose();
        }

        internal class InstagramApi
        {
            private const string ApiEndpoint = "https://i.instagram.com/api/v1/";
            public CookieCollection CookieCollection;
            public HttpWebRequest WebRequest;
            public HttpWebResponse WebResponse;

            public string PostData(string endpoint, string postData, string userAgent)
            {
                string url = $"{ApiEndpoint}{endpoint}";
                WebRequest = (HttpWebRequest)System.Net.WebRequest.Create(url);
                WebRequest.UserAgent = userAgent;

                WebRequest.CookieContainer = new CookieContainer();
                //lets make sure we add our cookies back
                if (CookieCollection != null && CookieCollection.Count > 0)
                {
                    WebRequest.CookieContainer.Add(CookieCollection);
                }
                WebRequest.Method = "POST";
                WebRequest.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                try
                {
                    var postBytes = Encoding.UTF8.GetBytes(postData);
                    WebRequest.ContentLength = postBytes.Length;
                    var postDataStream = WebRequest.GetRequestStream();
                    postDataStream.Write(postBytes, 0, postBytes.Length);
                    postDataStream.Close();
                    try
                    {
                        WebResponse = (HttpWebResponse)WebRequest.GetResponse();
                        //check if the status code is http 200 or http ok

                        if (WebResponse.StatusCode == HttpStatusCode.OK)
                        {
                            //get all the cookies from the current request and add them to the response object cookies

                            WebResponse.Cookies = WebRequest.CookieContainer.GetCookies(WebRequest.RequestUri);

                            if (WebResponse.Cookies.Count > 0)
                            {
                                //check if this is the first request/response, if this is the response of first request cookieCollection
                                //will be null
                                if (CookieCollection == null)
                                {
                                    CookieCollection = WebResponse.Cookies;
                                }
                                else
                                {
                                    foreach (Cookie oRespCookie in WebResponse.Cookies)
                                    {
                                        var bMatch = false;
                                        foreach (
                                            var oReqCookie in
                                                CookieCollection.Cast<Cookie>()
                                                    .Where(oReqCookie => oReqCookie.Name == oRespCookie.Name))
                                        {
                                            oReqCookie.Value = oRespCookie.Value;
                                            bMatch = true;
                                            break;
                                        }
                                        if (!bMatch)
                                            CookieCollection.Add(oRespCookie);
                                    }
                                }
                            }
                            var reader = new StreamReader(WebResponse.GetResponseStream());
                            var responseString = reader.ReadToEnd();
                            reader.Close();
                            return responseString;
                        }
                    }
                    catch (WebException wex)
                    {
                        if (wex.Response != null)
                        {
                            using (var errorResponse = (HttpWebResponse)wex.Response)
                            {
                                using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                                {
                                    var error = reader.ReadToEnd();
                                    return error;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                return "Error in posting data";
            }


            public string PostImage(string localImagePath, string userAgent)
            {
                var url = $"{ApiEndpoint}media/upload/";
                const string paramName = "photo";
                const string contentType = "image/jpeg";
                var nvc = new NameValueCollection
            {
                {"device_timestamp", (int) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds + ""}
            };
                var responseStr = string.Empty;

                try
                {
                    var boundary = $"---------------------------{DateTime.Now.Ticks}";
                    var boundarybytes = Encoding.ASCII.GetBytes($"\r\n--{boundary}\r\n");
                    WebRequest = (HttpWebRequest)System.Net.WebRequest.Create(url);
                    WebRequest.ContentType = $"multipart/form-data; boundary={boundary}";
                    WebRequest.Method = "POST";
                    WebRequest.KeepAlive = true;
                    WebRequest.Credentials = CredentialCache.DefaultCredentials;
                    WebRequest.UserAgent = userAgent;

                    WebRequest.CookieContainer = new CookieContainer();
                    if (CookieCollection != null && CookieCollection.Count > 0)
                    {
                        WebRequest.CookieContainer.Add(CookieCollection);
                    }

                    using (var requestStream = WebRequest.GetRequestStream())
                    {
                        var contentTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
                        foreach (string key in nvc.Keys)
                        {
                            requestStream.Write(boundarybytes, 0, boundarybytes.Length);
                            var formitem = string.Format(contentTemplate, key, nvc[key]);
                            var formitembytes = Encoding.UTF8.GetBytes(formitem);
                            requestStream.Write(formitembytes, 0, formitembytes.Length);
                        }
                        requestStream.Write(boundarybytes, 0, boundarybytes.Length);
                        var fileName = Path.GetFileName(localImagePath);

                        var headerTemplate =
                            "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
                        var header = string.Format(headerTemplate, paramName, fileName, contentType);
                        var headerbytes = Encoding.UTF8.GetBytes(header);
                        requestStream.Write(headerbytes, 0, headerbytes.Length);

                        using (var fileStream = new FileStream(localImagePath, FileMode.Open, FileAccess.Read))
                        {
                            var buffer = new byte[4096];
                            var bytesRead = 0;
                            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                requestStream.Write(buffer, 0, bytesRead);
                            }
                        }
                        var trailer = Encoding.ASCII.GetBytes($"\r\n--{boundary}--\r\n");
                        requestStream.Write(trailer, 0, trailer.Length);
                    }

                    if (CookieCollection != null && CookieCollection.Count > 0)
                    {
                        WebRequest.CookieContainer.Add(CookieCollection);
                    }

                    WebResponse wresp = null;
                    try
                    {
                        wresp = WebRequest.GetResponse();
                        var downStream = wresp.GetResponseStream();
                        if (downStream != null)
                        {
                            using (var downReader = new StreamReader(downStream))
                            {
                                responseStr = downReader.ReadToEnd();
                            }
                        }
                        return responseStr;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        if (wresp != null)
                        {
                            wresp.Close();
                            wresp = null;
                        }
                    }
                    finally
                    {
                        WebRequest = null;
                    }
                    return responseStr;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    // ignored
                }
                return responseStr;
            }
        }

        private string ReplaceInString(string source_str, string new_value)
        {
            System.Diagnostics.Debug.Write(source_str.Replace("%s", new_value));
            return source_str.Replace("%s", new_value);
        }
    }

    
}
