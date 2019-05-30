using System.Data.SQLite;

namespace IGTomesheqAutoLiker
{
    public enum StartingDateMethod { NotInitialized = 0, LastPost = 1, ChosenFromCalendar = 2, LastXHours = 3 };
    public class SupportGroupSettings
    {
        private string Name;
        public bool OnlyLike;
        public long StartingDateTimestamp;
        // 3 metody zadawania daty
        public long LastCommentedPostTimestamp;
        public long CalendarDateTimestamp;
        public long LastHoursTimestamp;
        public StartingDateMethod StartingDateMethod;
        public int LastHours;
        public string LastCommentedPostAuthor;
        public bool SkipThisTime;

        // DB
        private string connectionString;

        public SupportGroupSettings(SupportGroup parent)
        {
            connectionString = "Data Source=tomesheq_db.db;Version=3;";
            Name = parent.Name;
            OnlyLike = false;
            StartingDateTimestamp = 0;
            LastCommentedPostTimestamp = 0;
            CalendarDateTimestamp = 0;
            LastHoursTimestamp = 0;
            LastHours = 0;
            StartingDateMethod = StartingDateMethod.ChosenFromCalendar;
            SkipThisTime = false;
        }

        public SupportGroupSettings(SupportGroup parent, StartingDateMethod method, bool only_like, long timestamp_value, int last_hours, string last_post_author)
        {
            connectionString = "Data Source=tomesheq_db.db;Version=3;";
            Name = parent.Name;
            OnlyLike = only_like;

            // wszystkie na zero, wlasciwa jest inicjowana ponizej
            StartingDateTimestamp = 0;
            LastCommentedPostTimestamp = timestamp_value;
            CalendarDateTimestamp = 0;
            LastHoursTimestamp = 0;
            LastHours = last_hours;
            StartingDateMethod = method;
            SkipThisTime = false;
        }

        public bool SaveGroupSettingsToDB()
        {
            // konwersja only_like bool -> int
            int only_like = OnlyLike ? 1 : 0;

            // zapis do bazy
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE support_groups SET only_likes = '{only_like}', last_hours = {LastHours}, starting_date_method = {(int)StartingDateMethod} WHERE group_name = '{Name}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close();
            }
            return true;
        }
    }
}
