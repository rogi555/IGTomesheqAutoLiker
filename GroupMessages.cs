using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TLSharp.Core;
using TeleSharp.TL.Messages;
using TeleSharp.TL;
using System.Text.RegularExpressions;
using System.Data.SQLite;

namespace IGTomesheq
{
    class GroupMessages
    {
        //private TLChannelMessages all_messages;
        private List<TLMessage> filtered_messages;
        private TLMessages all_messages;

        public GroupMessages(int chatId, string chatName, Type dialogType)
        {
            all_messages = new TLMessages();
            filtered_messages = new List<TLMessage>();
        }

        public void AddAndFilterMessages(TLChannelMessages msgs)
        {
            try
            {
                for (int i = 0; i < msgs.Messages.Count; i++)
                {
                    if (msgs.Messages[i].ToString() == "TeleSharp.TL.TLMessage")
                    {
                        var message = (TLMessage)msgs.Messages[i];
                        string tmp_msg = message.Message.ToString();
                        if (tmp_msg.Contains("instagram.com"))
                        {
                            filtered_messages.Add(message);
                        }
                    }
                    else
                    {
                        //MessageBox.Show("To MessageService - pierdole to...");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("sad " + ex.Message.ToString());
            }
        }

        public void AddAndFilterMessages(TLAbsMessages msgs)
        {
            try
            {
                all_messages = (TLMessages)msgs;
                for (int i = 0; i < all_messages.Messages.Count; i++)
                {
                    if (all_messages.Messages[i].ToString() == "TeleSharp.TL.TLMessage")
                    {
                        var message = (TLMessage)all_messages.Messages[i];
                        string tmp_msg = message.Message.ToString();
                        if (tmp_msg.Contains("instagram.com"))
                        {
                            filtered_messages.Add(message);
                        }
                    }
                    else
                    {
                        //MessageBox.Show("To MessageService - pierdole to...");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("sad " + ex.Message.ToString());
            }
        }

        public void AddAndFilterMessages(List<TLMessage> msgs)
        {
            try
            {
                Regex reg = new Regex(@"https\:\/\/[www\.]*instagram\.com\/p\/[\w-]+[\/]*"); // regex linku do zdjecia
                MatchCollection matches;
                foreach (TLMessage msg in msgs)
                {
                    if(msg.Media != null)
                    {
                        if (msg.Media is TLMessageMediaWebPage)
                        {
                            TLMessageMediaWebPage mm = (TLMessageMediaWebPage)msg.Media;
                            if (mm is TLMessageMediaWebPage)
                            {
                                TLWebPage wp = mm.Webpage as TLWebPage;
                                if (wp is TLWebPage)
                                {
                                    matches = reg.Matches(wp.Url);
                                    if (matches.Count == 1)
                                    {
                                        filtered_messages.Add(msg);
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.Write($"\nNie znaleziono jednoznacznego przyporządkowania dla wiadomości o URL = {wp.Url}");
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.Write($"\nmm.WebPage nie jest typu TLWebPage, tylko {msg.Media.GetType().ToString()}");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.Write($"\nmsg.Media nie jest typu TLMessageMediaWebPage, tylko {msg.Media.GetType().ToString()}");
                            } 
                        }
                        else
                        {
                            System.Diagnostics.Debug.Write("\nMedia != null, ale tonie WebPage, tylko " + msg.Media.GetType().ToString());
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.Write("Filtrowanie wiadomosci telegrama nie powiodlo sie: " + ex.Message.ToString());
            }
        }

        public List<TLMessage> GetFilteredMessages()
        {
            return filtered_messages;
        }

        public List<InstagramPost> CreatePostsFromMessages()
        {
            List<InstagramPost> insta_posts = new List<InstagramPost>();
            System.Diagnostics.Debug.Write("Krok 4.1: Wchodze do foreach\n");
            foreach (var msg in filtered_messages)
            {
                System.Diagnostics.Debug.Write("Krok 4.2: We foreach\n");
                InstagramPost tmp_post = new InstagramPost();
                TLMessageMediaWebPage wp = (TLMessageMediaWebPage)msg.Media;
                TLWebPage webPage = (TLWebPage)wp.Webpage;
                System.Diagnostics.Debug.Write("Krok 4.3: Webpage gotowe\n");
                Regex reg = new Regex(@"https\:\/\/[www\.]*instagram\.com\/p\/[\w-]+[\/]*"); // regex linku do zdjecia
                MatchCollection matches;
                matches = reg.Matches(webPage.Url);
                System.Diagnostics.Debug.Write("Krok 4.4: Sprawdzam czy znaleziono odwzorowanie\n");
                if (matches.Count > 0)
                {
                    System.Diagnostics.Debug.Write("Krok 4.5: Znaleziono odwzorowanie (msg.Date = " + msg.Date.ToString() + ")\n");
                    if(tmp_post.SetTelegramInfo(matches[0].Value, msg.Message, (long)msg.Date))
                    {
                        System.Diagnostics.Debug.Write("Krok 4.5: Znaleziono odwzorowanie (msg.Date = " + msg.Date.ToString() + ")\n");
                        insta_posts.Add(tmp_post);
                        System.Diagnostics.Debug.Write("Krok 4.6: Dodano post do insta_posts\n");
                    }
                    else
                    {
                        System.Diagnostics.Debug.Write("Krok 4.5: Nie udalo sie dodac Telegram Data\n");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.Write("\nPomijam " + webPage.Url);
                }
            }

            return insta_posts;
        }

        public bool isEmpty()
        {
            return (filtered_messages.Count == 0);
        }
    }
}
