using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;

namespace IGTomesheqAutoLiker
{
    public class SupportGroup
    {
        public enum DownloadMessagesFrom {
            LastCommentedPost, /* Posty dodane po ostatnio skomentowanym poście */
            CustomDate, /* Posty dodane po ręcznie wpisanej dacie (w kalendarzyku) */
            GivenPeriod /* Posty dodane w ciągu podanego czasu (np. z ostatnich 24h) */
        };

        private string name;
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        private bool only_likes;
        public bool OnlyLikes
        {
            get { return this.only_likes; }
            set { this.only_likes = value; }
        }

        private int msg_index;
        public int MessageIndex
        {
            get { return this.msg_index; }
            set { this.msg_index = value; }
        }

        private long last_done_msg_date;
        public long LastDoneMsgDate
        {
            get { return this.last_done_msg_date; }
            set { this.last_done_msg_date = value; }
        }

        private long starting_point_timestamp;
        public long StartingPointTimestamp
        {
            get { return this.starting_point_timestamp; }
            set { this.starting_point_timestamp = value; }
        }

        public SupportGroupSettings Settings;

        // wszystkie, gole wiadomosci pobrane z telegrama
        private TLMessages all_messages;
        // te dwie listy powinny miec zawsze taka sama dlugosc
        public List<PostData> MessagesWithInstaPosts;

        // DB
        private string connectionString;

        // konstruktor
        public SupportGroup(string name)
        {
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            all_messages = new TLMessages();
            MessagesWithInstaPosts = new List<PostData>();

            this.name = name;
            only_likes = false;
            msg_index = -1;

            Settings = new SupportGroupSettings();
        }

        public SupportGroup(string name, bool only_likes, long last_done_msg)
        {
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            all_messages = new TLMessages();
            MessagesWithInstaPosts = new List<PostData>();

            this.Name = name;
            this.OnlyLikes = only_likes;
            this.LastDoneMsgDate = last_done_msg;

            msg_index = -1;

            Settings = new SupportGroupSettings();
        }

        // Tutaj odbywa się filtracja wiadomości - te, zawierające linki do zdjęć instagrama zostają dodane do odpowiedniej listy
        public void AddAndFilterMessages(List<TLMessage> msgs)
        {
            try
            {
                Regex reg = new Regex(@"https\:\/\/[www\.]*instagram\.com\/p\/[\w-]+[\/]*"); // regex linku do zdjecia
                MatchCollection matches;
                foreach (TLMessage msg in msgs)
                {
                    if (msg.Media != null)
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
                                        MessagesWithInstaPosts.Add(new PostData(msg));
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
                if(MessagesWithInstaPosts.Count > 0)
                {
                    msg_index = 0;
                    //return true;
                }
                else
                {
                    //return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write("Filtrowanie wiadomosci telegrama nie powiodlo sie: " + ex.Message.ToString());
                //return false;
            }
        }

        // sprawdza czy w tej grupie wsparcia są jakieś wiadomości z linkami do Instagrama
        public bool areThereAnyMessages()
        {
            return (MessagesWithInstaPosts.Count != 0);
        }

        // sprawdza ile postów zostało do zrobienia
        public string GetPostsToDoCounter()
        {
            int counter = this.msg_index + 1;
            if(MessagesWithInstaPosts.Count > 0)
            {
                return ("Post " + counter.ToString() + "/" + MessagesWithInstaPosts.Count.ToString() + " w tej grupie");
            }
            else
            {
                return ("Brak postów w tej grupie");
            }
        }

        // zwieksza numer wiadomosci do wyswietlenia i jesli to koniec zbioru wiadomosci, zwraca false
        public bool IncrementMessageIndex()
        {
            this.MessageIndex++;
            if((MessagesWithInstaPosts.Count - 1) >= MessageIndex)
            {
                return true;
            }
            else
            {
                this.MessageIndex = -2;
                return false;
            }
        }

        // zwraca pobraną z DB datę ostatnio skomentowanego (lub dla grup tylko like - polajkowanego) zdjęcia
        public long GetLastDoneMessageDate()
        {
            try
            {
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"SELECT * FROM support_groups WHERE group_name = '{this.Name}'";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    SQLiteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        if (reader.HasRows)
                        {
                            System.Diagnostics.Debug.Write("\nlast_done_msg: " + reader["last_done_msg"] + "\n");
                            this.last_done_msg_date = (reader["last_done_msg"] as long?).Value;
                            if (reader["last_done_msg"] != null)
                            {
                                return this.last_done_msg_date;
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

        // aktualizuje w DB wpis dot. daty ostatnio skomentowanego (lub dla grup tylko like - polajkowanego) zdjęcia
        public bool UpdateLastDoneMessage(long timestamp)
        {
            try
            {
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"UPDATE support_groups SET last_done_msg = {timestamp} WHERE group_name = '{this.Name}'";
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

        // ustawia datę od kiedy mają zostać pobrane wiadomości
        // wersja dla ostatnio polubionego postu
        public void SetStartingPoint()
        {
            this.StartingPointTimestamp = this.LastDoneMsgDate;
        }

        // wersja dla kalendarzyka
        public void SetStartingPoint(DateTime date_and_time)
        {
            this.StartingPointTimestamp = ToUnixTimestamp(date_and_time);
        }

        // wersja dla ilości godzin wstecz
        public void SetStartingPoint(int hours)
        {
            this.StartingPointTimestamp = ToUnixTimestamp(DateTime.Now) - (hours * 60 * 60);
        }

        public static long ToUnixTimestamp(DateTime target)
        {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, target.Kind);
            var unixTimestamp = System.Convert.ToInt64((target - date).TotalSeconds);

            return unixTimestamp;
        }
    }
}