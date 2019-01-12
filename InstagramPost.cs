using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IGTomesheq
{
    class InstagramPost
    {
        private string URL;
        private string telegram_message;
        public string TelegramMessage
        {
            get { return this.telegram_message; }
            set { this.telegram_message = value; }
        }
        private long telegram_message_timestamp;
        public long TelegramMessageTimestamp
        {
            get { return this.telegram_message_timestamp; }
            set { this.telegram_message_timestamp = value; }
        }
        private bool liked;
        public bool IsLiked
        {
            get { return this.liked; }
            set { this.liked = value; }
        }
        private bool commented;
        public bool IsCommented
        {
            get { return this.commented; }
            set { this.commented = value; }
        }
        private DateTime date_commented;
        private DateTime date_liked;
        private string comment_text;
        private string picture_path_jpg;
        public string PicturePathJpg
        {
            get { return this.picture_path_jpg; }
            set { this.picture_path_jpg = value; }
        }

        private string picture_path_png;
        public string PicturePathPng
        {
            get { return this.picture_path_png; }
            set { this.picture_path_png = value; }
        }
        private string insta_media_id;
        public string InstaMediaID
        {
            get { return this.insta_media_id; }
            set { this.insta_media_id = value; }
        }

        private string insta_media_shortcode;
        public string InstaMediaShortcode
        {
            get { return this.insta_media_shortcode; }
            set { this.insta_media_shortcode = value; }
        }

        private string owner;
        public string Owner
        {
            get { return this.owner; }
            set { this.owner = value; }
        }

        private string description;
        public string Description
        {
            get { return this.description; }
            set { this.description = value; }
        }

        private bool successfully_created;
        public bool SuccessfullyCreated
        {
            get { return this.successfully_created; }
            set { this.successfully_created = value; }
        }

        // DB
        //private SQLiteConnection m_dbConnection;
        private string connectionString;

        public InstagramPost()
        {
            connectionString = "Data Source=tomesheq_db.db;Version=3;";
            URL = "";
            telegram_message = "";
            liked = false;
            commented = false;
            date_commented = new DateTime();
            date_liked = new DateTime();
            comment_text = "";
            successfully_created = true;
            //picture_path_jpg = "";

            /*try
            {
                //m_dbConnection = new SQLiteConnection("Data Source=tomesheq_db.db;Version=3;");
                //m_dbConnection.Open();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.Message.ToString());
            }*/
        }

        public bool SetTelegramInfo(string url, string telegram_msg, long msg_timestamp)
        {
            try
            {
                this.URL = url;
                //this.picture_path_jpg = pic_path + GetMediaShortcodeFromURL() + ".jpg";
                //this.picture_path_png = pic_path + GetMediaShortcodeFromURL() + ".png";
                this.telegram_message = telegram_msg;
                this.TelegramMessageTimestamp = msg_timestamp;
                System.Diagnostics.Debug.Write("Krok 4.5.1: Przed wpisem do DB\n");
                this.CreateDBRecord();
                System.Diagnostics.Debug.Write("Krok 4.5.2: Po wpisie do DB\n");
                successfully_created = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("ERROR!\n" + ex.Message);
                return false;
            }
        }

        private string GetMediaShortcodeFromURL()
        {
            Regex reg = new Regex(@"\/[\w-]+[^.]*[\/]");
            if(this.URL.Last() != '/')
            {
                this.URL += "/";
            }
            MatchCollection matches1 = reg.Matches(this.URL);
            string tmp = "";

            if(matches1.Count == 1)
            {
                foreach (Match match in matches1)
                {
                    tmp = match.Value.Substring(3, match.Value.Length - 3);
                    if(tmp.Last() == '/')
                    {
                        tmp = tmp.Remove(tmp.Length - 1);
                    }
                }
            }

            return tmp;
        }

        private void CreateDBRecord()
        {
            this.InstaMediaShortcode = this.GetMediaShortcodeFromURL();
            System.Diagnostics.Debug.Write("Krok 4.5.1.1: Przed open DB\n");
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                System.Diagnostics.Debug.Write("Krok 4.5.1.2: Po open DB\n");
                string sql = $"INSERT INTO instagram_posts (id_post, insta_media_shortcode, insta_media_id, commented, comment_text, date_commented, liked, date_liked) VALUES (NULL, '{this.InstaMediaShortcode}', '', 0, '', 0, 0, 0)";
                System.Diagnostics.Debug.Write("Krok 4.5.1.3: Przed zapisaniem do DB\n");
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                System.Diagnostics.Debug.Write("Krok 4.5.1.4: Po zapisaniu do DB (1)\n");
                command.ExecuteNonQuery();
                System.Diagnostics.Debug.Write("Krok 4.5.1.5: Po zapisaniu do DB (1)\n");
                m_dbConnection.Close(); 
            }
            successfully_created = true;
        }

        public bool UpdateLiked(long timestamp)
        {
            liked = true;
            date_liked = UnixTimeStampToDateTime(timestamp);
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE instagram_posts SET liked = 1, date_liked = {timestamp} WHERE insta_media_shortcode = '{this.GetMediaShortcodeFromURL()}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                m_dbConnection.Close(); 
            }

            successfully_created = true;

            if (commented)
            {
                return true;
            }
            return false;
        }

        public bool UpdateCommented(long timestamp, string com_text)
        {
            commented = true;
            comment_text = com_text;
            date_commented = UnixTimeStampToDateTime(timestamp);

            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE instagram_posts SET commented = 1, date_commented = {timestamp}, comment_text = '{com_text}' WHERE insta_media_shortcode = '{this.InstaMediaShortcode}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                m_dbConnection.Close(); 
            }

            successfully_created = true;

            if (liked)
            {
                return true;
            }
            return false;
        }

        public bool UpdateInstaMediaID(string insta_media_id)
        {
            InstaMediaID = insta_media_id;

            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE instagram_posts SET insta_media_id = '{this.InstaMediaID}' WHERE insta_media_shortcode = '{this.InstaMediaShortcode}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                m_dbConnection.Close(); 
            }

            successfully_created = true;

            if (liked)
            {
                return true;
            }
            return false;
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public void SetPostBroken()
        {
            successfully_created = false;
        }
    }

    class InstagramPostInfo
    {
        private string picture_url;
        public string PictureURL
        {
            get { return this.picture_url; }
            set { this.picture_url = value; }
        }
        private int picture_width;
        public int PictureWidth
        {
            get { return this.picture_width; }
            set { this.picture_width = value; }
        }
        private int picture_height;
        public int PictureHeight
        {
            get { return this.picture_height; }
            set { this.picture_height = value; }
        }
        private string post_id;
        public string PostID
        {
            get { return this.post_id; }
            set { this.post_id = value; }
        }
        private bool is_empty;
        public bool IsEmpty
        {
            get { return this.is_empty; }
            set { this.is_empty = value; }
        }

        public InstagramPostInfo()
        {
            is_empty = true;
            // empty
        }

        public InstagramPostInfo(string pic_url, int pic_width, int pic_height, string _post_id)
        {
            is_empty = false;
            PictureURL = pic_url;
            PictureWidth = pic_width;
            PictureHeight = pic_height;
            PostID = _post_id;
        }
    }
}
