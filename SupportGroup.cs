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
        private string name;
        public string Name
        {
            get { return this.name; }
            set { this.name = value; }
        }

        private int msg_index;
        public int MessageIndex
        {
            get { return this.msg_index; }
            set { this.msg_index = value; }
        }

        public SupportGroupSettings Settings;

        // wszystkie, gole wiadomosci pobrane z telegrama
        private TLMessages all_messages;
        // te dwie listy powinny miec zawsze taka sama dlugosc
        public List<PostData> MessagesWithInstaPosts;

        // DB
        private string connectionString;

        // konstruktor używany, gdy zostaje recznie dodana grupa wsparcia i nie ma dla niej ustawien
        public SupportGroup(string name)
        {
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            all_messages = new TLMessages();
            MessagesWithInstaPosts = new List<PostData>();

            this.name = name;
            msg_index = -1;

            Settings = new SupportGroupSettings(this);
        }

        // konstruktor używany, gdy zostaje dodana grupa wsparcia z bazy danych
        public SupportGroup(string name, StartingDateMethod method, bool only_likes, long last_done_msg, int last_hours, string last_post_author)
        {
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            all_messages = new TLMessages();
            MessagesWithInstaPosts = new List<PostData>();

            this.Name = name;
            msg_index = -1;

            Settings = new SupportGroupSettings(this, method, only_likes, last_done_msg, last_hours, last_post_author);
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
                            this.Settings.LastCommentedPostTimestamp = (reader["last_done_msg"] as long?).Value;
                            if (reader["last_done_msg"] != null)
                            {
                                return this.Settings.LastCommentedPostTimestamp;
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
                this.Settings.LastCommentedPostTimestamp = timestamp;
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