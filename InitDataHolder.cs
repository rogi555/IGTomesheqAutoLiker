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

        public InitDataHolder()
        {
            // baza danych
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            default_comments = new List<DefaultComment>();
            initial_support_groups = new List<SupportGroup>();
            store = new FileSessionStore();

            InstaLoginStatus = ShowInstagramPanelWith.InsertLoginData;
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
                return true;
                // uzytkownik zalogowany - wszystko ok
            }
            else
            {
                return false;
                // bedzie trzeba zalogowac sie ponownie
            }
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
                        while (reader.Read())
                        {
                            initial_support_groups.Add(new SupportGroup(reader.GetString(1), (reader.GetInt32(2) == 1) ? true : false, reader.GetInt64(3)));
                        }
                    }
                    m_dbConnection.Close();
                }
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
