using InstaSharper.Classes.Models;
using System;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace IGTomesheqAutoLiker
{
    public class InstagramPost
    {
        private string URL;
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
        private string picture_path_jpg;
        public string PicturePathJpg
        {
            get { return this.picture_path_jpg; }
            set { this.picture_path_jpg = value; }
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
        private string base64image;
        public string Base64Image
        {
            get { return this.base64image; }
            set { this.base64image = value; }
        }
        private Image image;
        public Image Image
        {
            get { return this.image; }
            set { this.image = value; }
        }

        private InstaMedia instagram_post;
        public InstaMedia InstaPost
        {
            get { return this.instagram_post; }
            set { this.instagram_post = value; }
        }

        private DateTime date_commented;
        private DateTime date_liked;
        private string comment_text;

        // DB
        private string connectionString;

        public InstagramPost(string base64img)
        {
            base64image = base64img;
            connectionString = "Data Source=tomesheq_db.db;Version=3;";
            URL = "";
            liked = false;
            commented = false;
            date_commented = new DateTime();
            date_liked = new DateTime();
            comment_text = "";
            successfully_created = false;
            picture_path_jpg = "";
        }

        public InstagramPost(Image image)
        {
            Image = image;
            connectionString = "Data Source=tomesheq_db.db;Version=3;";
            URL = "";
            liked = false;
            commented = false;
            date_commented = new DateTime();
            date_liked = new DateTime();
            comment_text = "";
            successfully_created = false;
            picture_path_jpg = "";
        }

        public InstagramPost(InstaMedia post)
        {
            InstaPost = post;
        }

        private string GetMediaShortcodeFromURL()
        {
            string ret = "";

            Regex reg = new Regex(@"\/[\w-]+[^.]*[\/]");
            if (this.URL.Last() != '/')
            {
                this.URL += "/";
            }
            MatchCollection matches1 = reg.Matches(this.URL);

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

        public string ShortcodeToID(string shortcode)
        {
            char character;
            long id = 0;
            var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
            for (var i = 0; i < shortcode.Length; i++)
            {
                character = shortcode[i];
                id = (id * 64) + alphabet.IndexOf(character);
            }
            return id.ToString();
        }

        public bool SetTelegramInfo(string url, string telegram_msg, long msg_timestamp)
        {
            try
            {
                this.URL = url;
                this.CreateDBRecord();
                successfully_created = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("ERROR!\n" + ex.Message);
                return false;
            }
        }

        private void CreateDBRecord()
        {
            this.InstaMediaShortcode = this.GetMediaShortcodeFromURL();
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"INSERT INTO instagram_posts (id_post, author, insta_media_shortcode, insta_media_id, commented, comment_text, date_commented, liked, date_liked) VALUES (NULL, '{this.InstaMediaShortcode}', '', 0, '', 0, 0, 0)";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();
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
        private string author;
        public string Author
        {
            get { return this.author; }
            set { this.author = value; }
        }
        private string description;
        public string Description
        {
            get { return this.description; }
            set { this.description = value; }
        }
        private bool did_i_like_it_already;
        public bool DidILikeItAlready
        {
            get { return this.did_i_like_it_already; }
            set { this.did_i_like_it_already = value; }
        }
        private bool is_it_photo_of_me;
        public bool IsItPhotoOfMe
        {
            get { return this.is_it_photo_of_me; }
            set { this.is_it_photo_of_me = value; }
        }
        private bool is_empty;
        public bool IsEmpty
        {
            get { return this.is_empty; }
            set { this.is_empty = value; }
        }

        // konstruktor używany, gdy nie udało się pobrać postu (404, 502 itd.) - wówczas post jest pomijany i nie jest wyswietlany
        public InstagramPostInfo()
        {
            is_empty = true;
        }

        // konstruktor używany, gdy udało się pobrać wszystkie dane dot. postu i ma być on wyświetlony
        public InstagramPostInfo(string pic_url, int pic_width, int pic_height, string _post_id)
        {
            PictureURL = pic_url;
            PictureWidth = pic_width;
            PictureHeight = pic_height;
            PostID = _post_id;
            IsEmpty = false;
        }
    }
}
