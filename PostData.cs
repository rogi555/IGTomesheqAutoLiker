using InstaSharper.Classes.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TeleSharp.TL;

namespace IGTomesheq
{
    class PostData
    {
        public TLMessage TelegramMessage;
        public InstagramPost InstagramPost;
        public string Url;

        public PostData(TLMessage message)
        {
            TelegramMessage = message;
            Url = GetUrlFromMessage();
        }

        public bool CreateInstagramPost(string base64pic, string description)
        {
            InstagramPost = new InstagramPost(base64pic);
            InstagramPost.Description = description;
            return true;
        }

        public bool CreateInstagramPost(Image image)
        {
            InstagramPost = new InstagramPost(image);
            return true;
        }

        public bool CreateInstagramPost(InstaMedia post)
        {
            InstagramPost = new InstagramPost(post);
            return true;
        }

        private string GetUrlFromMessage()
        {
            if (TelegramMessage.Media != null)
            {
                // zapisuje obiekt Media z wiadomosci
                var media_msg = TelegramMessage.Media;

                // sprawdza czy obiekt Media jest stroną internetową
                if (media_msg.GetType().ToString() == "TeleSharp.TL.TLMessageMediaWebPage")
                {
                    // zapisuje obiekt Media jako wiadomość zawierającą stronę internetową do dalszej obróbki
                    TeleSharp.TL.TLMessageMediaWebPage web_page_msg = (TeleSharp.TL.TLMessageMediaWebPage)media_msg;
                    // sprawdzenie czy obiekt Webpage istnieje
                    if (web_page_msg.Webpage != null)
                    {
                        // zapisuje obiekt Media jako stronę internetową do dalszej obróbki
                        TeleSharp.TL.TLWebPage web_page = (TeleSharp.TL.TLWebPage)web_page_msg.Webpage;
                        // jeśli Telegram pobrał zdjęcie
                        if (web_page.Url != null)
                        {
                            return web_page.Url;
                        }
                    }
                }
            }

            return "none"; // zwraca, gdy cos poszlo nie tak i nie ma linka do insta w wiadomosci
        }

        // poprawić, żeby filtrowało z linka jakieś przydługawe końcówki np. utm-source itd
        public string GetMediaShortcodeFromURL()
        {
            string ret = "";

            Regex reg = new Regex(@"\/[\w-]+[^.]*[\/]");
            if (this.Url.Last() != '/')
            {
                this.Url += "/";
            }
            MatchCollection matches1 = reg.Matches(this.Url);

            if (matches1.Count == 1)
            {
                foreach (Match match in matches1)
                {
                    ret = match.Value.Substring(3, match.Value.Length - 3);
                    if (ret.Last() == '/')
                    {
                        ret = ret.Remove(ret.Length - 1);
                    }
                }
            }

            return ret;
        }
    }
}
