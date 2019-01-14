using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGTomesheq
{
    class SupportGroup
    {
        private int id;
        public int DialogId
        {
            get { return this.id; }
            set { this.id = value; }
        }
        private string group_name;
        public string GroupName
        {
            get { return this.group_name; }
            set { this.group_name = value; }
        }
        private Type dialog_type;
        public Type DialogType
        {
            get { return this.dialog_type; }
            set { this.dialog_type = value; }
        }

        private int posts_to_do;
        public int PostsToDo
        {
            get { return this.posts_to_do; }
            set { this.posts_to_do = value; }
        }

        private bool only_likes;
        public bool OnlyLikes
        {
            get { return this.only_likes; }
            set { this.only_likes = value; }
        }

        private int last_done_post_index;
        public int LastDonePostIndex
        {
            get { return this.last_done_post_index; }
            set { this.last_done_post_index = value; }
        }

        private long last_done_msg_timestamp;
        public long LastDoneMsgTimestamp
        {
            get { return this.last_done_msg_timestamp; }
            set { this.last_done_msg_timestamp = value; }
        }

        private bool already_got_messages;
        public bool AlreadyGotMessages
        {
            get { return this.already_got_messages; }
            set { this.already_got_messages = value; }
        }

        public GroupMessages GroupMessages;
        public List<InstagramPost> InstagramPosts;

        // DB
        //private SQLiteConnection m_dbConnection;
        private string connectionString;

        public SupportGroup(string name)
        {
            InstagramPosts = new List<InstagramPost>();
            group_name = name;
            posts_to_do = 0;
            only_likes = false;
            last_done_post_index = -1;
            GroupMessages = new GroupMessages(this.id, group_name, dialog_type);
            connectionString = "Data Source=tomesheq_db.db;Version=3;";
            //current_index = 0;
        }

        public string GetPostsToDoCounter()
        {
            int counter = this.last_done_post_index + 1;
            if(InstagramPosts.Count > 0)
            {
                return ("Post " + counter.ToString() + "/" + InstagramPosts.Count.ToString() + " w tej grupie");
            }
            else
            {
                return ("Brak postów w tej grupie");
            }
        }

        public long GetLastDoneMessage()
        {
            try
            {
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"SELECT * FROM support_group_names WHERE group_name = '{this.GroupName}'";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    SQLiteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        if (reader.HasRows)
                        {
                            System.Diagnostics.Debug.Write("\nlast_done_msg: " + reader["last_done_msg"] + "\n");
                            this.last_done_msg_timestamp = (reader["last_done_msg"] as long?).Value;
                            if (reader["last_done_msg"] != null)
                            {
                                return this.last_done_msg_timestamp;
                            }
                            else
                            {
                                return 0;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.Write("\nNie ma wynikow dla zapytania: " + sql + "\n");
                        }
                    }
                    m_dbConnection.Close(); 
                }
                return -1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("Blad przy aktualizacji daty najnowszego zrobionego postu Telegrama: " + ex.Message.ToString());
                return -1;
            }
        }

        public bool UpdateLastDoneMessage(long timestamp)
        {
            try
            {
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"UPDATE support_group_names SET last_done_msg = {timestamp} WHERE group_name = '{this.GroupName}'";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery();
                    m_dbConnection.Close(); 
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("Blad przy aktualizacji daty najnowszego zrobionego postu Telegrama: " + ex.Message.ToString());
                return false;
            }
        }
    }
}