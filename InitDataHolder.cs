using InstaSharper.Classes;
using InstaSharper.Classes.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;

namespace IGTomesheqAutoLiker
{
    public class InitDataHolder
    {
        // Telegram
        public const int API_ID = 546804;
        public const string API_HASH = "da4ce7c1d67eb3a1b1e2274f32d08531";

        public List<DefaultComment> default_comments;
        public FileSessionStore store;
        public string code;
        public string hash;
        public string phone_number;
        public TelegramClient client;
        public TeleSharp.TL.TLUser user;
        public TLDialogs dialogs;
        public List<TLChannel> channels;
        public List<TLChat> chats;
        public List<SupportGroup> initial_support_groups;

        public string connectionString;

        public enum ShowTelegramPanelWith { InsertPhoneNumber, InsertSecurityCode, LoginSuccess, LoginFailed };
        public enum ShowInstagramPanelWith { InsertLoginData, InsertSecurityCode, LoginSuccess, LoginFailed };

        public ShowInstagramPanelWith InstaLoginStatus;

        public bool telegram_ok;
        public bool instagram_ok;
        public bool default_comments_ok;
        public bool support_groups_ok;
        public bool db_support_groups_ok;

        public int likes_limit;
        public int min_time_betw_likes;
        public int max_time_betw_likes;

        public InitDataHolder()
        {
            // baza danych
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            // flagi
            telegram_ok = false;
            instagram_ok = false;
            default_comments_ok = false;
            support_groups_ok = false;
            db_support_groups_ok = false;

            default_comments = new List<DefaultComment>();
            initial_support_groups = new List<SupportGroup>();

            bool ok = false;
            int attemps_counter = 0;
            while (!ok && attemps_counter < 3)
            {
                try
                {
                    store = new FileSessionStore();
                    ok = true;
                }
                catch (Exception ex)
                {
                    attemps_counter++;
                    System.Diagnostics.Debug.Write(ex.Message);
                } 
            }

            InitSettings();

            InstaLoginStatus = ShowInstagramPanelWith.InsertLoginData;
        }

        public void InitSettings()
        {
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(this.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"SELECT * FROM settings";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    reader.Read();
                    // wszystko OK - nic nie rob
                    min_time_betw_likes = reader.GetInt32(0);
                    max_time_betw_likes = reader.GetInt32(1);
                    likes_limit = reader.GetInt32(2);
                }
                m_dbConnection.Close();
            }
        }

        public async Task<bool> InitTelegram()
        {
            // inicjalizuje polaczenie telegramowe
            bool connection_succesfull = false;
            int connection_attemps_counter = 0;

            while (!connection_succesfull && connection_attemps_counter < 3)
            {
                try
                {
                    store = new FileSessionStore();
                    client = new TelegramClient(API_ID, API_HASH, store);
                    await client.ConnectAsync();
                    connection_succesfull = true;
                }
                catch (Exception ex)
                {
                    connection_attemps_counter++;
                    // wyswietlic na statusbarze, ze pierwsze logowanie nieudane
                }
            }
            if (client.IsUserAuthorized())
            {
                telegram_ok = true;
                return true;
            }

            return false;
        }

        public async Task<bool> GetAllTelegramChannelsAndChats()
        {
            // pobiera wszystkie chaty uzytkownika
            dialogs = (TLDialogs)await client.GetUserDialogsAsync();
            try
            {
                channels = dialogs.Chats
                        .OfType<TLChannel>()
                        .ToList();
                chats = dialogs.Chats
                        .OfType<TLChat>()
                        .ToList();
                // pobrano pomyslnie - zwraca true, wszystko OK
                return true;
            }
            catch (Exception ex)
            {
                // bedzie trzeba ponownie zalogowac sie do telegrama
                return false;
                //MessageBox.Show("Błąd podczas pobierania danych z Telegrama:\n" + ex.Message.ToString());
            }
        }

        public async Task<bool> InitInstagram()
        {
            string login = "";
            string password = "";
            string sql = $"SELECT * FROM instagram_data LIMIT 1";
            try
            {
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    SQLiteDataReader reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            login = reader["login"].ToString();
                            password = reader["password"].ToString();
                        }
                    }
                    m_dbConnection.Close();
                }
            }
            catch (Exception ex)
            {
                return false;
                // bedzie konieczne reczne wpisanie danych logowania
                //MessageBox.Show("Bład podczas sprawdzania loginu w bazie danych:\n" + ex.Message.ToString());
            }

            if ((login.Length > 0) && (password.Length > 0))
            {
                try
                {
                    IGProc.login = login;
                    IGProc.password = password;
                    InstaSharper.Classes.InstaLoginResult result = await IGProc.Login(login, password);
                    if (result == InstaSharper.Classes.InstaLoginResult.BadPassword || result == InstaSharper.Classes.InstaLoginResult.InvalidUser || result == InstaSharper.Classes.InstaLoginResult.Exception)
                    {
                        InstaLoginStatus = ShowInstagramPanelWith.LoginFailed;
                    }
                    else if(result == InstaSharper.Classes.InstaLoginResult.ChallengeRequired)
                    {
                        InstaLoginStatus = ShowInstagramPanelWith.InsertSecurityCode;
                    }
                    else if(result == InstaSharper.Classes.InstaLoginResult.TwoFactorRequired)
                    {
                        // nieobslugiwane!
                        InstaLoginStatus = ShowInstagramPanelWith.LoginFailed;
                    }
                    else
                    {
                        InstaLoginStatus = ShowInstagramPanelWith.LoginSuccess;
                    }
                }
                catch (Exception ex)
                {
                    return false;
                    // bedzie konieczne reczne wpisanie danych do logowania
                }
            }

            if (IGProc.IsUserAuthenticated())
            {
                instagram_ok = true;
                return true;
                // uzytkownik zalogowany - wszystko ok
            }
            else
            {
                return false;
                // bedzie trzeba zalogowac sie ponownie
            }
        }

        public async Task GetBagdadFollowers()
        {
            List<InstaUserShort> followers = new List<InstaUserShort>();
            followers = await IGProc.GetFollowersList(PaginationParameters.Empty, null);

            List<InstaUserShort> bagdad_users = new List<InstaUserShort>();
            foreach(var user in followers)
            {
                List<InstaMedia> media = await IGProc.GetMediaList(user, PaginationParameters.Empty);
                bool is_from_bagdad = false;
                foreach(var post in media)
                {
                    if (post.Location.City == "Bagdad" || post.Location.City == "Baghdad")
                    {
                        is_from_bagdad = true;
                    }
                }

                if(is_from_bagdad)
                {
                    bagdad_users.Add(user);
                }
            }

            System.Diagnostics.Debug.WriteLine(bagdad_users.Count.ToString());
            return;
        }

        public bool GetSupportGroups()
        {
            string sql = $"SELECT * FROM support_groups";
            try
            {
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    SQLiteDataReader reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        int i = 0;
                        while (reader.Read())
                        {
                            initial_support_groups.Add(new SupportGroup(reader.GetString(1), (StartingDateMethod)reader.GetInt32(6), (reader.GetInt32(2) == 1) ? true : false, reader.GetInt64(3), reader.GetInt32(4), reader.GetString(5)));
                            i++;
                        }
                        i++;
                    }
                    m_dbConnection.Close();
                }
                db_support_groups_ok = true;
                return true;
            }
            catch (Exception ex)
            {
                return false;
                // blad polaczenia z baza danych
            }
        }

        public bool GetDefaultComments()
        {
            string sql = $"SELECT * FROM default_comments";
            try
            {
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    SQLiteDataReader reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            default_comments.Add(new DefaultComment(reader.GetString(1), reader.GetInt32(2)));
                        }
                    }
                    m_dbConnection.Close();
                }
                default_comments_ok = true;
                return true;
            }
            catch (Exception ex)
            {
                return false;
                // blad polaczenia z baza danych
            }
        }
    }
}
