using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Data.SQLite;
using TLSharp.Core;
using TeleSharp.TL.Messages;
using TeleSharp.TL;
using System.Threading.Tasks;
using IGTomesheq;
using InstaSharper.API;
using InstaSharper.Classes;
using InstaSharper.API.Builder;
using InstaSharper.Logger;
using System.Text.RegularExpressions;
using System.Drawing;
using System.IO;
using InstaSharper.Classes.Models;
using System.Net;
using Newtonsoft.Json;
using System.Web.Script.Serialization;
using TLSharp.Core.Network;
using System.Threading;

namespace IGTomesheqAutoLiker
{
    public partial class Form1 : Form
    {
        // baza danych
        //private SQLiteConnection m_dbConnection;
        private string connectionString;

        // Telegram
        const int API_ID = 546804;
        const string API_HASH = "da4ce7c1d67eb3a1b1e2274f32d08531";

        TelegramClient client;
        string code;
        string hash;
        string phone_number;
        TeleSharp.TL.TLUser user;
        FileSessionStore store;
        TLDialogs dialogs;
        List<TLChannel> channels;
        List<TLChat> chats;
        List<GroupMessages> dialogs_messages;
        InstagramProcessor insta;
        List<SingleEmoji> complete_emojis;
        List<Label> emoji_labels;

        // ogolne
        int media_to_comment_counter;
        long posts_newer_than_timestamp;
        const int MAX_PHOTO_WIDTH = 376;
        const int MAX_PHOTO_HEIGHT = 376;

        int current_support_group;

        List<SupportGroup> support_groups;

        private List<Label> date_support_groups;
        private List<Label> date_support_groups_last_post_dates;

        private static IInstaApi _instaApi;

        public Form1()
        {
            InitializeComponent();

            // baza danych
            try
            {
                connectionString = "Data Source=tomesheq_db.db;Version=3;";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                throw;
            }

            // Telegram
            code = "";
            hash = "";
            dialogs_messages = new List<GroupMessages>();

            // Instagram
            //insta = new InstagramProcessor();

            // ogolne
            media_to_comment_counter = 0;
            posts_newer_than_timestamp = 0;
            current_support_group = -1;

            support_groups = new List<SupportGroup>();

            // utworzenie obiektu z klasami emoji
            complete_emojis = new List<SingleEmoji>();
            emoji_labels = new List<Label>();

            this.toolStripStatusLabel1.Text = "Program gotowy do działania! Kliknij dalej...";

            // umiejscowienie okna
            this.CenterToScreen();
        }

        /* --- SCREEN POWITALNY --- */
        // button dalej na screen1 (powitanie)
        private void button1_Click(object sender, EventArgs e)
        {
            this.panel_hello.Hide();
            this.panel_comments.Show();

            // zainicjuj formularz danymi
            init_comments_screen();

            // wyswietl podpowiedz na toolStripLabel
            this.toolStripStatusLabel1.Text = "Zarządzaj domyślnymi komentarzami. Gdy gotowe, kliknij dalej...";
        }
        /* --- |SCREEN POWITALNY| --- */

        /* --- SCREEN Z DOMYŚLNYMI KOMENTARZAMI --- */
        private void init_comments_screen()
        {
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                // odczytaj z bazy danych dostepne komentarze
                m_dbConnection.Open();
                string sql = "SELECT * FROM default_comments";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    listView1.Items.Add(reader["comment"].ToString());
                    //MessageBox.Show(reader["comment"].ToString());
                }

                // przy okazji zapisz do bazy API ID i API Hash i phone_number pusty
                // sprawdz czy juz nie ma wpisu

                sql = "SELECT * FROM telegram_data";
                command = new SQLiteCommand(sql, m_dbConnection);
                reader = command.ExecuteReader();

                // jesli nie ma to zrob nowy
                if (!reader.HasRows)
                {
                    sql = $"INSERT INTO telegram_data (api_id, api_hash, phone_number) VALUES ({API_ID}, '{API_HASH}', '')";
                    command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                }
                m_dbConnection.Close(); 
            }
        }

        // button dalej na screenie z domyslnymi komentarzami
        private async void button2_Click(object sender, EventArgs e)
        {
            this.panel_comments.Hide();
            this.panel_telegram_login.Show();
            this.button5.Enabled = false;

            // inicjalizacja okna telegrama
            this.toolStripStatusLabel1.Text = "Trwa inicjalizacja Telegrama...";
            if(await init_telegram())
            {
                // inicjalizacja okna instagrama
                this.toolStripStatusLabel1.Text = "Trwa inicjalizacja Instagrama...";

                if (await init_instagram())
                {
                    // wyswietl info
                    this.toolStripStatusLabel1.Text = "Gotowe! Kliknij dalej...";
                    this.button5.Enabled = true;
                }
                else
                {
                    // Logowanie do Instagrama nieudane - spróbuj jeszcze raz...
                    this.toolStripStatusLabel1.Text = "Logowanie do Instagrama nieudane - wprowadź dane i kliknij Zaloguj...";
                }
            }
            else
            {
                // Logowanie do Telegrama nieudane - spróbuj jeszcze raz...
                this.toolStripStatusLabel1.Text = "Logowanie do Telegrama nieudane - spróbuj jeszcze raz...";
            }
        }

        // button zapisywania komentarza
        private void button3_Click(object sender, EventArgs e)
        {
            // jesli pole jest puste, to nic nie dodawaj
            if (richTextBox1.Text.Length == 0)
                return;
            // dodaj komentarz do listy
            listView1.Items.Add(new ListViewItem(richTextBox1.Text.ToString()));
            // dodaj komentarz do bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"INSERT INTO default_comments (id_comment, comment, used_automatically) VALUES (NULL, '{richTextBox1.Text.ToString()}', 0)";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close(); 
            }
            // wyczysc richtextbox
            richTextBox1.Text = "";
        }

        // button do usuniecia niechcianego komentarza z listView1
        private void button4_Click(object sender, EventArgs e)
        {
            // jesli nic nie zaznaczono to nic nie rob
            if (listView1.SelectedIndices.Count == 0)
                return;
            // usun zaznaczony komentarz z bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"DELETE FROM default_comments WHERE comment='{listView1.SelectedItems[0].Text}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close(); 
            }
            // usun zaznaczony komentarz z listy
            listView1.SelectedItems[0].Remove();
        }
        /* --- |SCREEN Z DOMYŚLNYMI KOMENTARZAMI| --- */

        /* --- SCREEN Z LOGOWANIEM DO TELEGRAMA I INSTAGRAMA --- */
        private async Task<bool> init_telegram()
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
                    MessageBox.Show("Próba zalogowania nr " + connection_attemps_counter.ToString() + " nieudana... Poniżej komunikat błędu, kliknij OK, aby spróbować ponownie.\n" + ex.Message.ToString());
                } 
            }
            if (client.IsUserAuthorized())
            {
                // ukryj pola do wpisania numeru telefonu
                // label30 -> label numer telefonu
                // textbox4 -> textbox z nuemrem telefonu
                // button15 -> button zapisz numer i wyslij kod
                label30.Hide();
                textBox4.Hide();
                button15.Hide();

                // ukryj pola do wpisania kodu
                // label15 -> label kod z telegrama
                // textbox1 -> textbox do wpisania kod z instagrama
                // button6 -> button zapisania kodu telegrama
                label15.Hide();
                textBox1.Hide();
                button6.Hide();

                // pokaz labele z info, ze logowanie pomyslne
                label16.Show();
                label17.Show();

                try
                {
                    // pobierz channele
                    GetAllTelegramChannelsAndChats();
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Problem z pobieraniem channeli!\n" + ex.Message.ToString());
                    return false;
                }
            }
            return false;
        }

        private async void GetAllTelegramChannelsAndChats()
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
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas pobierania danych z Telegrama:\n" + ex.Message.ToString());
            }
        }

        // button zapisz kod potwierdzajacy od Telegrama
        private async void button6_Click(object sender, EventArgs e)
        {
            // informuje uzytkownika co sie dzieje
            this.toolStripStatusLabel1.Text = "Trwa potwierdzanie otrzymanego kodu...";
            // jesli nie wpisano kodu, nic nie rob
            if (textBox1.Text.Length == 0)
                return;
            // jesli kod poprawny (?) to zapisz go
            code = textBox1.Text;

            // dodaj do bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE telegram_data SET phone_number = '{phone_number}' WHERE phone_number = ''";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close(); 
            }

            // ukryj pola do wpisania kodu
            textBox1.Hide();
            label15.Hide();
            button6.Hide();
            // debug purposes
            //MessageBox.Show("Kod: " + code);
            // wyslij do Telegrama kod
            user = await client.MakeAuthAsync(phone_number, hash, code);
            // wyswietl komunikat, gdy logowanie przebieglo pomyslnie
            if (user.Id > 0)
            {
                // informacja o pomyslnym zalogowaniu do telegrama
                this.toolStripStatusLabel1.Text = "Pomyslnie zalogowano do telegrama!";
                label16.Show();
                label17.Show();
            }
        }

        // button telegram zapisz nr telefonu i wyslij kod
        private async void button15_Click(object sender, EventArgs e)
        {
            // zapisz nr telefonu w bazie danych
            if (textBox4.Text.Length == 9)
            {
                this.phone_number = "48";
                this.phone_number = this.phone_number.Insert(2, textBox4.Text);
            }
            else if (textBox4.Text.Length == 11)
            {
                this.phone_number = textBox4.Text;
            }
            else
            {
                MessageBox.Show("Nr telefonu powinien miec długość 11 znaków i zaczynać się od 48");
                return;
            }

            // jesli nr telefonu jest poprawny to go pokaz
            //MessageBox.Show("Nr telefonu: " + this.phone_number);

            // wyslij kod telegrama
            hash = await client.SendCodeRequestAsync(this.phone_number);

            // label30 -> label numer telefonu
            // textbox4 -> textbox z nuemrem telefonu
            // button15 -> button zapisz numer i wyslij kod
            // wszystkie je schowac
            label30.Hide();
            textBox4.Hide();
            button15.Hide();

            // label15 -> label kod z telegrama
            // textbox1 -> textbox do wpisania kod z instagrama
            // button6 -> button zapisania kodu telegrama
            // wszystkie je pokazac
            label15.Show();
            textBox1.Show();
            button6.Show();

            // informacja o oczekiwaniu na kod z Telegrama
            this.toolStripStatusLabel1.Text = $"Na konto Telegram przypisane do numeru {this.phone_number} został wysłany kod potwierdzający - wpisz go w polu powyżej";
        }

        private async Task<bool> init_instagram()
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
                        // schowanie pol do wpisania danych
                        label26.Hide();
                        label27.Hide();
                        textBox2.Hide();
                        textBox3.Hide();
                        button14.Hide();
                        button5.Enabled = false;

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
                MessageBox.Show("Bład podczas sprawdzania loginu w bazie danych:\n" + ex.Message.ToString());
            }

            if((login.Length > 0) && (password.Length > 0))
            {
                /*Action<object> LoginToInsta;
                LoginToInsta = TryInstaLogin;
                InstaLoginData login_data = new InstaLoginData(login, password);

                Task task = new Task(TryInstaLogin, login_data);*/

                IGProc.login = login;
                IGProc.password = password;
                await IGProc.Login(login, password);
            }

            if(IGProc.IsUserAuthenticated())
            {
                // schowanie pol do wpisania danych
                label26.Hide();
                label27.Hide();
                textBox2.Hide();
                textBox3.Hide();
                //button5.Enabled = true;

                // pokaz labele informacyjne
                label28.Show();
                label29.Hide();

                button14.Show();
                button14.Text = "Wyloguj";

                return true;
            }
            else
            {
                // pokaz dane do logowania ponownie
                label26.Show();
                label27.Show();
                textBox2.Show();
                textBox3.Show();
                button14.Show();
                //button5.Enabled = true;

                // pokaz labele informacyjne
                label29.Hide();
                label28.Hide();

                return false;
            }
        }

        class InstaLoginData
        {
            public string login;
            public string password;

            public InstaLoginData(string log, string pas)
            {
                login = log;
                password = pas;
            }
        }

        private async void TryInstaLogin(object _login_data)
        {
            InstaLoginData login_data = _login_data as InstaLoginData;
            await IGProc.Login(login_data.login, login_data.password);
        }

        // button logowania do insta
        private async void button14_Click(object sender, EventArgs e)
        {
            if (!IGProc.IsUserAuthenticated())
            {
                if ((textBox2.Text.Length > 0) && (textBox3.Text.Length > 0))
                {
                    // informacja o logowaniu do instagrama
                    this.toolStripStatusLabel1.Text = $"Witaj {textBox2.Text}! Trwa logowanie do Instagrama...";

                    textBox2.Enabled = false;
                    textBox3.Enabled = false;
                    button14.Enabled = false;
                    //MessageBox.Show($"login: {textBox2.Text}\npassword: {textBox3.Text}");
                    await IGProc.Login(textBox2.Text, textBox3.Text);
                    if (IGProc.IsUserAuthenticated())
                    {
                        using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                        {
                            m_dbConnection.Open();
                            string sql = $"SELECT * FROM instagram_data WHERE login = '{textBox2.Text}' AND password = '{textBox3.Text}'";
                            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                            SQLiteDataReader reader = command.ExecuteReader();
                            if (reader.HasRows)
                            {
                                // wszystko OK - nic nie rob
                            }
                            else
                            {
                                // nie ma takiego loginu w bazie - zapisz go
                                IGProc.login = textBox2.Text;
                                IGProc.password = textBox3.Text;
                                sql = $"INSERT INTO instagram_data (login, password) VALUES ('{textBox2.Text}', '{textBox3.Text}')";
                                command = new SQLiteCommand(sql, m_dbConnection);
                                command.ExecuteNonQuery();
                            }
                            m_dbConnection.Close(); 
                        }

                        // schowanie pol do wpisania danych
                        label26.Hide();
                        label27.Hide();
                        textBox2.Hide();
                        textBox3.Hide();
                        //button14.Hide();

                        // pokaz labele informacyjne
                        label28.Show();
                        label29.Hide();

                        textBox2.Enabled = true;
                        textBox3.Enabled = true;
                        button14.Enabled = true;
                        button14.Text = "Wyloguj";
                        this.button5.Enabled = true;
                        // informacja o logowaniu do instagrama
                        this.toolStripStatusLabel1.Text = $"Gotowe! Kliknij dalej...";
                    }
                    else
                    {
                        textBox2.Enabled = true;
                        textBox3.Enabled = true;
                        button14.Enabled = true;

                        // informacja o logowaniu do instagrama
                        this.toolStripStatusLabel1.Text = $"Logowanie nieudane! Spróbuj ponownie...";

                        // pokaz dane do logowania ponownie
                        label26.Hide();
                        label27.Hide();
                        textBox2.Hide();
                        textBox3.Hide();
                        button14.Hide();

                        // pokaz labele informacyjne
                        label29.Show();
                        label28.Hide();
                    }
                } 
            }
            else
            {
                string login = IGProc.login;
                string password = IGProc.password;

                // informacja o logowaniu do instagrama
                this.toolStripStatusLabel1.Text = $"Wylogowuję konto {login} z Instagrama...";

                textBox2.Enabled = false;
                textBox3.Enabled = false;
                button14.Enabled = false;

                // usun dane konto z bazy danych
                if (await IGProc.Logout())
                {
                    // usun wpis dot uzytkownika z bazy danych
                    using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                    {
                        m_dbConnection.Open();
                        string sql = $"DELETE FROM instagram_data WHERE login = '{login}' AND password = '{password}'";
                        SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                        command.ExecuteNonQuery(); // nic nie zwraca
                        m_dbConnection.Close(); 
                    }

                    MessageBox.Show("Wylogowano - aby zalogować się na nowo, uruchom ponownie aplikację");

                    Application.Exit();

                    /*
                    // pokaz dane do logowania ponownie
                    label26.Show();
                    label27.Show();
                    textBox2.Show();
                    textBox3.Show();
                    button14.Show();

                    // pokaz labele informacyjne
                    label29.Hide();
                    label28.Hide();

                    button14.Text = "Zaloguj";

                    MessageBox.Show(IGProc.IsUserAuthenticated().ToString());*/
                }
                else
                {
                    // informacja o logowaniu do instagrama
                    this.toolStripStatusLabel1.Text = $"Wylogowanie {login} z Instagrama nie powiodło się...";
                    MessageBox.Show("Wylogowanie nie powiodło się...");
                }
            }
            /*else
            {
                await IGProc.Login("martha_farewell", "rooney892");
            }*/
        }

        /* Po tej metodzie w channels zostają tylko grupy wsparcia - inne channele zostają z nich usunięte */
        private void FilterTelegramChannels(bool init_filtering)
        {
            // Usuwa z listy pobranej z Telegrama te channele, ktore nie zostaly wybrane jako grupy wsparcia
            if (channels != null)
            {
                try
                {
                    // dodaj do listy wszystkie chaty
                    int channel_index = 0;
                    List<int> indicies_to_be_deleted = new List<int>();
                    foreach (TLChannel channel in channels)
                    {
                        bool exists = false;
                        foreach (ListViewItem it in listView3.Items)
                        {
                            if (channel.Title.ToString() == it.Text)
                            {
                                exists = true;
                            }
                        }
                        if (!exists)
                        {
                            if (init_filtering)
                            {
                                listView2.Items.Add(channel.Title.ToString()); 
                            }
                            // zapamietaj, ze ten channel nalezy usunac z listy [LIFO, zeby indeksy nie przeskakiwaly po usunieciu]
                            indicies_to_be_deleted.Insert(0, channel_index);
                        }
                        channel_index++;
                    }

                    if (!init_filtering)
                    {
                        // usuwanie nieuzywanych channeli z pamieci [LIFO]
                        foreach (int index in indicies_to_be_deleted)
                        {
                            channels.RemoveAt(index);
                        } 
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("foreach channels " + ex.Message.ToString());
                }
            }
            else
            {
                MessageBox.Show("channels == null");
            }
        }

        /* Po tej metodzie w chats zostają tylko grupy wsparcia - inne chaty zostają z nich usunięte */
        private void FilterTelegramChats(bool init_filtering)
        {
            if (chats != null)
            {
                try
                {
                    // dodaj do listy wszystkie chaty
                    int chat_index = 0;
                    List<int> indicies_to_be_deleted = new List<int>();
                    foreach (TLChat chat in chats)
                    {
                        bool exists = false;
                        foreach (ListViewItem it in listView3.Items)
                        {
                            if (chat.Title.ToString() == it.Text)
                            {
                                exists = true;
                            }
                        }
                        if (!exists)
                        {
                            if (init_filtering)
                            {
                                listView2.Items.Add(chat.Title.ToString()); 
                            }
                            // zapamietaj, ze ten channel nalezy usunac z listy [LIFO, zeby indeksy nie przeskakiwaly po usunieciu]
                            indicies_to_be_deleted.Insert(0, chat_index);
                        }
                        chat_index++;
                    }

                    // usuwanie nieuzywanych channeli z pamieci [LIFO]
                    foreach (int index in indicies_to_be_deleted)
                    {
                        chats.RemoveAt(index);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("foreach chats " + ex.Message.ToString());
                }
            }
            else
            {
                MessageBox.Show("chats == null");
            }
        }

        // button dalej na screenie z logowaniem do telegrama
        private async void button5_Click(object sender, EventArgs e)
        {
            // dodaj grupy wsparcia do drugiej listy
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = "SELECT * FROM support_group_names";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    listView3.Items.Add(reader["group_name"].ToString());
                    //MessageBox.Show(reader["group_name"].ToString());
                }
                m_dbConnection.Close(); 
            }

            // usuwa z pamieci channele i chaty nie bedace grupami wsparcia
            this.toolStripStatusLabel1.Text = "Filtrowanie chatów Telegrama...";
            FilterTelegramChannels(true/*it is initial filtering*/);
            FilterTelegramChats(true/*it is initial filtering*/);

            panel_telegram_login.Hide();
            panel_telegram_chats.Show();

            // pokaz info
            this.toolStripStatusLabel1.Text = "Zarządzaj grupami wsparcia! Gdy gotowe, kliknij dalej...";
        }

        /* --- |SCREEN Z LOGOWANIEM DO TELEGRAMA I INSTAGRAMA| --- */

        /* --- SCREEN Z GRUPAMI WSPARCIA TELEGRAMA --- */

        // button dalej na screenie z grupami wsparcia telegrama
        private async void button7_Click(object sender, EventArgs e)
        {
            if(listView3.Items.Count == 0)
            {
                MessageBox.Show("Hola hola! Wybierz chć jedną grupę wsparcia i kliknij dalej");
            }

            panel_telegram_chats.Hide();

            // usuwa z pamieci channele i chaty nie bedace grupami wsparcia
            this.toolStripStatusLabel1.Text = "Ponowne filtrowanie chatów Telegrama...";
            this.Refresh();
            FilterTelegramChannels(false/*it is NOT initial filtering*/);
            FilterTelegramChats(false/*it is NOT initial filtering*/);

            // inicjalizacja kolejnego ekranu
            InitChooseDate();
            panel_choose_msg_starting_date.Show();

            //InitLikeCommenterPanel(/*true*/);
            //panel_liker_commenter.Show();

            //MessageBox.Show("Przynajmniej jedna z grup wsparcia nie została jeszcze ani razu skomentowana - nad listą grup wsparcia pojawił się kalendarzyk z przyciskiem szukaj. Wybierz datę i godzinę od której mają zostać pobrane posty i kliknij szukaj.");
            this.toolStripStatusLabel1.Text = "Gotowe! Wybierz datę, od której chcesz przeszukać grupy wsparcia - zostaną znalezione linki dodane po tej dacie...";
            this.Refresh();
        }

        /* Po tej metodzie w support_gourps[i].GroupMessages są już tylko wiadomości zawierające linki do zdjęć */
        private async Task GetTelegramChannelsMessages(long timestamp)
        {
            this.Refresh();
            if (channels != null)
            {
                foreach (var channel in channels)
                {
                    this.toolStripStatusLabel1.Text = "Szukam linków w grupie " + channel.Title + "...";
                    this.Refresh();
                    List<TLMessage> TLmessages = new List<TLMessage>();
                    
                    if (channel.AccessHash != null)
                    {
                        // tworzy obiekty klasy SupportGroup dla kazdej grupy wsparcia
                        support_groups.Add(new SupportGroup(channel.Title));
                        // sprawdza jaka wiadomosc z czatu byla obsluzona (like & comment) jako ostatnia
                        long last_done_msg_timestamp = support_groups.Last().GetLastDoneMessage();
                        if(last_done_msg_timestamp <= 0)
                        {
                            last_done_msg_timestamp = timestamp;
                        }
                        //MessageBox.Show("Pobieram wiadomości młodsze niż: " + UnixTimeStampToDateTime(last_done_msg_timestamp).ToLongTimeString() + " " + last_done_msg_timestamp.ToString());
                        // pobiera zbior wiadomosci dla danego kanalu
                        var peer = new TLInputPeerChannel() { ChannelId = channel.Id, AccessHash = (long)channel.AccessHash.Value };
                        var msgs = await client.GetHistoryAsync(peer, 0, 0, 100);
                        if (msgs is TLChannelMessages)
                        {
                            var messag = msgs as TLChannelMessages;
                            //MessageBox.Show("Liczba wiadomości w channelu " + channel.Title + " = " + messag.Count.ToString());
                            if(messag.Count <= 100)
                            {
                                var messages = messag.Messages;

                                foreach (var message in messages)
                                {
                                    if (message is TLMessage)
                                    {
                                        var m = message as TLMessage;
                                        // sprawdzenie czy ta wiadomość już była
                                        if (m.Date <= last_done_msg_timestamp)
                                        {
                                            System.Diagnostics.Debug.Write("\n||| " + channel.Title + ": " + TLmessages.Count.ToString() + " wiadomosci pobrano!|||\n");
                                            break;
                                        }
                                        TLmessages.Add(m);
                                    }
                                    else if (message is TLMessageService)
                                    {
                                        var m1 = message as TLMessageService;
                                    }
                                }
                            }
                            else
                            {
                                bool done = false;
                                int total = 0;
                                while (/*(total < 500) && */!done) // tymczasowo - sprawdzac date wiadomosci
                                {
                                    var messa = msgs as TLChannelMessages;
                                    var messages = messa.Messages;

                                    foreach (var message in messages)
                                    {
                                        if (message is TLMessage)
                                        {
                                            var m = message as TLMessage;
                                            // sprawdzenie czy ta wiadomość już była
                                            if(m.Date <= last_done_msg_timestamp)
                                            {
                                                done = true;
                                                System.Diagnostics.Debug.Write("\n||| " + channel.Title + ": " + TLmessages.Count.ToString() + " wiadomosci pobrano!|||\n");
                                                break;
                                            }
                                            ++total;
                                            TLmessages.Add(m);
                                        }
                                        else if (message is TLMessageService)
                                        {
                                            var mess = message as TLMessageService;
                                            ++total;
                                            done = mess.Action is TLMessageActionChatCreate;
                                            if (done)
                                            {
                                                System.Diagnostics.Debug.Write("\n||| " + channel.Title + ": " + TLmessages.Count.ToString() + " wiadomosci pobrano!|||\n");
                                                break;
                                            }
                                            else
                                                continue;
                                        }
                                    }

                                    //MessageBox.Show("Pobrano już " + total.ToString() + " wiadomości dla dialoga " + channel.Title);
                                    // jesli done = true, czyli znaleziono juz wiadomosci starsze niz data graniczna, nie pobieraj wiecej wiadomosci
                                    if (!done)
                                    {
                                        try
                                        {
                                            msgs = await client.GetHistoryAsync(peer, total, 0, 100);
                                        }
                                        catch (FloodException ex)
                                        {
                                            int seconds_to_wait = (int)ex.TimeToWait.TotalSeconds;
                                            //MessageBox.Show("Próbujemy pobrać zbyt wiele wiadomości w zbyt krótkim czasie - Telegram się przed tym broni i nie udostępni wiadomości przez " + seconds_to_wait.ToString() + " sekund. Poczekaj cierpliwie i nic nie rób, a aplikacja sama wznowi działanie.");
                                            for (int i = 0; i < seconds_to_wait; i++)
                                            {
                                                this.toolStripStatusLabel1.Text = "Próbujemy pobrać zbyt wiele wiadomości w zbyt krótkim czasie - Telegram się broni... Czekam " + (seconds_to_wait - i).ToString() + " sekund...";
                                                this.Refresh();
                                                Thread.Sleep(1000); 
                                            }
                                            this.toolStripStatusLabel1.Text = "Szukam dalej linków w grupie " + channel.Title + "...";
                                            this.Refresh();
                                            try
                                            {
                                                msgs = await client.GetHistoryAsync(peer, total, 0, 100);
                                            }
                                            catch (FloodException ex1)
                                            {
                                                MessageBox.Show("Telegram znowu się broni... Poczekaj cierpliwie :(");
                                            }
                                            //MessageBox.Show("Bład podczas pobierania wiadomości Telegrama:\n" + "Typ wyjatku: " + ex.GetType().ToString() + "\n" + ex.Message.ToString());
                                            //MessageBox.Show("Możliwe, że nie wszystkie wiadomości zostały pobrane... Proszę porównać z aplikacją Telegram na telefonie i zgłosić problem do Bubu!");
                                            //break;
                                        } 
                                    }
                                }
                            }
                        }
                        System.Diagnostics.Debug.Write("\n+++\n");
                        foreach(var msg in TLmessages)
                        {
                            System.Diagnostics.Debug.Write("\n--- " + msg.Message + " ---\n");
                        }
                        System.Diagnostics.Debug.Write("\n+++\n");
                        //MessageBox.Show("Najstarsza wiadomosc dla " + channel.Title + ": " + TLmessages.Min(x => x.Date).ToString() + ", a najmłodsza: " + TLmessages.Max(x => x.Date).ToString());
                        support_groups.Last().GroupMessages = new GroupMessages(channel.Id, channel.Title, channel.GetType());
                        System.Diagnostics.Debug.Write("\nCHANNEL " + support_groups.Last().GroupName + "\n");
                        support_groups.Last().GroupMessages.AddAndFilterMessages(TLmessages);
                        //MessageBox.Show("Channel " + channel.Title + "\nLiczba wiadomosci: " + TLmessages.Count.ToString() + "\nLiczba wiadomosci z linkami: " + support_groups.Last().GroupMessages.GetFilteredMessages().Count.ToString() + "\nLiczba postow: " + support_groups.Last().InstagramPosts.Count.ToString());
                        System.Diagnostics.Debug.Write("\n--- FILTERED MESSAGES ---\n");
                        foreach (var msg in support_groups.Last().GroupMessages.GetFilteredMessages())
                        {
                            System.Diagnostics.Debug.Write("\n---"+msg.Message+"---\n");
                        }
                    }
                }
            }
        }

        /* Po tej metodzie w support_gourps[i].GroupMessages są już tylko wiadomości zawierające linki do zdjęć */
        private async Task GetTelegramChatsMessages(long timestamp)
        {
            if (chats != null)
            {
                foreach (var chat in chats)
                {
                    this.toolStripStatusLabel1.Text = "Szukam linków w grupie " + chat.Title + "...";
                    this.Refresh();
                    List<TLMessage> TLmessages = new List<TLMessage>();
                    try
                    {
                        // tworzy obiekty klasy SupportGroup dla kazdej grupy wsparcia
                        support_groups.Add(new SupportGroup(chat.Title));

                        // sprawdza jaka wiadomosc z czatu byla obsluzona (like & comment) jako ostatnia
                        long last_done_msg_timestamp = support_groups.Last().GetLastDoneMessage();
                        if (last_done_msg_timestamp <= 0)
                        {
                            last_done_msg_timestamp = timestamp;
                        }

                        // pobierz wiadomosci dla danego chatu
                        var peer = new TLInputPeerChat() { ChatId = chat.Id };
                        TLAbsMessages msgs = await client.GetHistoryAsync(peer, 0, -1, 100);

                        if (msgs is TLMessages)
                        {
                            var messages = msgs as TLMessages;

                            foreach (var message in messages.Messages)
                            {
                                if (message is TLMessage)
                                {
                                    var m = message as TLMessage;
                                    // sprawdzenie czy ta wiadomość już była
                                    if (m.Date <= last_done_msg_timestamp)
                                    {
                                        System.Diagnostics.Debug.Write("\n||| " + chat.Title + ": " + TLmessages.Count.ToString() + " wiadomosci pobrano!|||\n");
                                        break;
                                    }
                                    TLmessages.Add(m);
                                }
                                else if (message is TLMessageService)
                                {
                                    var m1 = message as TLMessageService;
                                }
                            }
                        }
                        else if (msgs is TLMessagesSlice)
                        {
                            bool done = false;
                            int total = 0;
                            while (!done/* && (total < 500)*/)
                            {
                                var messages = msgs as TLMessagesSlice;

                                foreach (var message in messages.Messages)
                                {
                                    if (message is TLMessage)
                                    {
                                        var m = message as TLMessage;
                                        // sprawdzenie czy ta wiadomość już była
                                        if (m.Date <= last_done_msg_timestamp)
                                        {
                                            done = true;
                                            System.Diagnostics.Debug.Write("\n||| " + chat.Title + ": " + TLmessages.Count.ToString() + " wiadomosci pobrano!|||\n");
                                            break;
                                        }
                                        ++total;
                                        TLmessages.Add(message as TLMessage);
                                    }
                                    else if (message is TLMessageService)
                                    {
                                        var mess = message as TLMessageService;
                                        ++total;
                                        done = mess.Action is TLMessageActionChatCreate;
                                        if (done)
                                        {
                                            System.Diagnostics.Debug.Write("\n||| " + chat.Title + ": " + TLmessages.Count.ToString() + " wiadomosci pobrano!|||\n");
                                            break;
                                        }
                                        else
                                            continue;
                                    }
                                }

                                // jesli done = true, czyli znaleziono juz wiadomosci starsze niz data graniczna, nie pobieraj wiecej wiadomosci
                                if (!done)
                                {
                                    try
                                    {
                                        msgs = await client.GetHistoryAsync(peer, total, 0, 0);
                                    }
                                    catch (FloodException ex)
                                    {
                                        int seconds_to_wait = (int)ex.TimeToWait.TotalSeconds;
                                        MessageBox.Show("Próbujemy pobrać zbyt wiele wiadomości w zbyt krótkim czasie - Telegram się przed tym broni i nie udostępni wiadomości przez " + seconds_to_wait.ToString() + " sekund. Poczekaj cierpliwie i nic nie rób, a aplikacja sama wznowi działanie.");
                                        this.toolStripStatusLabel1.Text = "Telegram się broni... Czekam " + seconds_to_wait.ToString() + " sekund...";
                                        this.Refresh();
                                        Thread.Sleep((seconds_to_wait + 1) * 1000);
                                        this.toolStripStatusLabel1.Text = "Szukam dalej linków w grupie " + chat.Title + "...";
                                        this.Refresh();
                                        try
                                        {
                                            msgs = await client.GetHistoryAsync(peer, total, 0, 100);
                                        }
                                        catch (FloodException ex1)
                                        {
                                            MessageBox.Show("Telegram znowu się wyjebał... (Chaty)");
                                        }

                                        // tu task z czekaniem
                                        //MessageBox.Show("Bład podczas pobierania wiadomości Telegrama:\n" + "Typ wyjatku: " + ex.GetType().ToString() + "\n" + ex.Message.ToString());
                                        //MessageBox.Show("Możliwe, że nie wszystkie wiadomości zostały pobrane... Proszę porównać z aplikacją Telegram na telefonie i zgłosić problem do Bubu!");
                                        //break;
                                    } 
                                }
                            }
                        }
                        support_groups.Last().GroupMessages = new GroupMessages(chat.Id, chat.Title, chat.GetType());
                        support_groups.Last().GroupMessages.AddAndFilterMessages(TLmessages);
                        //MessageBox.Show("Chat " + chat.Title + "\nLiczba wiadomosci: " + TLmessages.Count.ToString() + "\nLiczba wiadomosci z linkami: " + support_groups.Last().GroupMessages.GetFilteredMessages().Count.ToString() + "\nLiczba postow: " + support_groups.Last().InstagramPosts.Count.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("a " + ex.Message.ToString());
                    }
                }
            }
        }

        private async Task DownloadFirstPhotoFromTelegramDialogs()
        {
            // zamraża interfejs użytkownika
            foreach (Control ctr in this.Controls)
            {
                ctr.Enabled = false;
            }

            try
            {
                if (support_groups != null)
                {
                    if (!support_groups[0].GroupMessages.isEmpty())
                    {
                        if (support_groups[0].GroupMessages.filtered_messages[0].Media != null)
                        {
                            var media_msg = support_groups[0].GroupMessages.filtered_messages[0].Media;
                            if (media_msg.GetType().ToString() == "TeleSharp.TL.TLMessageMediaWebPage")
                            {
                                TeleSharp.TL.TLMessageMediaWebPage web_page_msg = (TeleSharp.TL.TLMessageMediaWebPage)media_msg;
                                if (web_page_msg.Webpage != null)
                                {
                                    TeleSharp.TL.TLWebPage web_page = (TeleSharp.TL.TLWebPage)web_page_msg.Webpage;
                                    if (web_page.Photo != null)
                                    {
                                        try
                                        {
                                            TeleSharp.TL.TLPhoto photo = (TeleSharp.TL.TLPhoto)web_page.Photo;
                                            List<TLPhotoSize> photo_sizes = new List<TLPhotoSize>();
                                            foreach (var size in photo.Sizes.ToList())
                                            {
                                                photo_sizes.Add((TLPhotoSize)size);
                                            }
                                            TLFileLocation file_location = (TLFileLocation)photo_sizes.Last().Location;
                                            TeleSharp.TL.Upload.TLFile resFile = new TeleSharp.TL.Upload.TLFile();
                                            try
                                            {
                                                resFile = await client.GetFile(new TLInputFileLocation
                                                {
                                                    LocalId = file_location.LocalId,
                                                    Secret = file_location.Secret,
                                                    VolumeId = file_location.VolumeId
                                                }, (int)Math.Pow(2, Math.Ceiling(Math.Log(photo_sizes.Last().Size, 2))));
                                            }
                                            catch (Exception ex2)
                                            {
                                                MessageBox.Show("BŁĄD2!\n" + ex2.Message.ToString());
                                            }

                                            using (var ms = new MemoryStream(resFile.Bytes))
                                            {
                                                byte[] byteArr = ms.ToArray();
                                                string base64image = Convert.ToBase64String(byteArr);
                                                MessageBox.Show("Caption = " + web_page.Description);
                                                pictureBox2.Image = Image.FromStream(ms);
                                                //Log.Info(Tag, "Base64 Image = " + base64image);
                                                //await AddNewMessageToDatabase(channelName, ch_id, tLMessageMediaPhoto.caption, base64image, message.from_id.GetValueOrDefault(), message.id);
                                            }
                                        }
                                        catch (Exception ex1)
                                        {
                                            MessageBox.Show("BŁĄD1!\n" + ex1.Message.ToString());
                                        }
                                    }
                                }
                            }
                            else
                            {
                                MessageBox.Show("Ta wiadomość Telegrama nie jest stroną internetową!");
                            }
                        }
                    }
                    else
                    {
                        // w tej grupie nie ma postow do zrobienia w tym okresie czasu!
                        MessageBox.Show("W tej grupie nie ma postow do zrobienia w tym okresie czasu");
                    }
                }
                else
                {
                    MessageBox.Show("Aplikacja nie znalazła grup wsparcia! Zamykam program...");
                    Application.Exit();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("BŁĄD!\n" + ex.Message.ToString());
            }

            // rozmraża interfejs użytkownika
            foreach (Control ctr in this.Controls)
            {
                ctr.Enabled = true;
            }
        }

        private void DownloadPhotosFromTelegramDialogs()
        {
            foreach(Control ctr in this.Controls)
            {
                ctr.Enabled = false;
            }

            //PythonInstance python = new PythonInstance();
            foreach (SupportGroup support_group in support_groups)
            {
                if (!support_group.GroupMessages.isEmpty())
                {
                    //MessageBox.Show("Channel " + support_group.GroupName + ", liczba postow: " + support_group.GroupMessages.GetFilteredMessages()/*.Where(x => (x.Date > timeSpan)).Count().ToString()*/);

                    // przerabia wiadomosci telegrama na posty instagrama
                    support_group.InstagramPosts = support_group.GroupMessages.CreatePostsFromMessages();
                    this.toolStripStatusLabel1.Text = $"Pobieram posty z grupy {support_group.GroupName}... (0/{support_group.InstagramPosts.Count.ToString()})";
                    this.Refresh();

                    int i = 0;
                    foreach (var dialog_msg in support_group.GroupMessages.GetFilteredMessages()/*.Where(x => (x.Date > timeSpan))*/)
                    {
                        //MessageBox.Show("Grupa: " + support_group.GroupName + "\nLiczba wiadomosci: " + support_group.GroupMessages.GetFilteredMessages().Count.ToString());
                        this.toolStripStatusLabel1.Text = $"Pobieram posty z grupy {support_group.GroupName}... ({i}/{support_group.InstagramPosts.Count.ToString()})";
                        this.Refresh();
                        // pobierz zdjęcie do folderu
                        if (dialog_msg.Media.GetType().ToString() == "TeleSharp.TL.TLMessageMediaWebPage")
                        {
                            TLMessageMediaWebPage media_web_page;
                            TLWebPage web_page;
                            try
                            {
                                media_web_page = (TLMessageMediaWebPage)dialog_msg.Media;
                                web_page = (TLWebPage)media_web_page.Webpage;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Błąd konwersji przefiltrowanej wiadomości telegramowej: " + ex.Message.ToString() + "\n");
                                continue;
                            }

                            Regex reg;
                            MatchCollection matches;
                            try
                            {
                                reg = new Regex(@"https\:\/\/[www\.]*instagram\.com\/p\/[\w-]+[\/]*"); // regex linku do zdjecia
                                matches = reg.Matches(web_page.Url);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Błąd Regexa przefiltrowanej wiadomości telegramowej: " + ex.Message.ToString());
                                continue;
                            }

                            if (matches.Count > 0)
                            {
                                try
                                {
                                    InstagramPostInfo post_info = GetPostInfoByUrl(matches[0].Value);
                                    if (support_group.InstagramPosts.Where(x => x.TelegramMessage == dialog_msg.Message).First() != null)
                                    {
                                        if (!post_info.IsEmpty)
                                        {
                                            support_group.InstagramPosts.Where(x => x.TelegramMessage == dialog_msg.Message).First().UpdateInstaMediaID(post_info.PostID);
                                            support_group.InstagramPosts.Where(x => x.TelegramMessage == dialog_msg.Message).First().PicturePathJpg = post_info.PictureURL;  
                                        }
                                        else
                                        {
                                            // nie udalo sie ustalic danych do zdjecia
                                            support_group.InstagramPosts.Where(x => x.TelegramMessage == dialog_msg.Message).First().SetPostBroken();
                                        }
                                    }
                                    else
                                    {
                                        MessageBox.Show("Grupa: " + support_group.GroupName + "\nInstaPosts: " + support_group.InstagramPosts.Count + "\nTelegramMsg: " + dialog_msg.Message);
                                    }
                                    // dodac jeszcze miejsce na wpisanie rozmiarow obrazka
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Błąd pobierania zdjęcia oraz informacji o poście Insta: " + ex.Message.ToString() + "\nGrupa: " + support_group.GroupName + "\nTelegramMsg: " + dialog_msg.Message);
                                    continue;
                                }

                                // zwieksza liczniki
                                media_to_comment_counter++;
                                support_group.PostsToDo++;
                            }
                            else
                            {
                                MessageBox.Show("Link podany we wiadomości jest niepoprawny!\nPodano: " + web_page.Url);
                            }
                        }
                        i++;
                    }

                    // pokazanie wszystkich id dla postów
                    /*foreach (var post in support_group.InstagramPosts)
                    {
                        System.Diagnostics.Debug.Write("\n" + post.InstaMediaShortcode + " -> " + post.InstaMediaID + "\n");
                    }*/

                    // sprawdzenie
                    /*foreach(var post in support_group.InstagramPosts)
                    {
                        System.Diagnostics.Debug.Write("\nShortcode: " + post.InstaMediaShortcode + " | ID: " + post.InstaMediaID + "\n");
                    }*/
                }
            }
            //python = null;

            foreach (Control ctr in this.Controls)
            {
                ctr.Enabled = true;
            }

            // pokazuje w label24 ile zdjec wymaga uwagi
            label24.Text = media_to_comment_counter.ToString();
        }

        // button przenies chat jako grupe wsparcia
        private void button8_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count > 0)
            {
                // wyswietl na liscie grup wsparcia
                listView3.Items.Add(listView2.SelectedItems[0].Text);

                // dodaj do bazy danych
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"INSERT INTO support_group_names (id_group, group_name, last_done_msg) VALUES (NULL, '{listView2.SelectedItems[0].Text}', 0)";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                    m_dbConnection.Close(); 
                }

                // dodaj do listy grup wsparcia w logice programu
                support_groups.Add(new SupportGroup(listView2.SelectedItems[0].Text));

                // usun ze starej listy
                listView2.SelectedItems[0].Remove();
            }
        }

        // button usun chat z grup wsparcia
        private void button9_Click(object sender, EventArgs e)
        {

            if (listView3.SelectedIndices.Count > 0)
            {
                listView2.Items.Add(listView3.SelectedItems[0].Text);

                using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"DELETE FROM support_group_names WHERE group_name = '{listView3.SelectedItems[0].Text}'";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                    m_dbConnection.Close(); 
                }

                listView3.SelectedItems[0].Remove();
            }
        }

        /* --- |SCREEN Z GRUPAMI WSPARCIA TELEGRAMA| --- */

        /* --- SCREEN WYBORU DATY --- */
        
        // button dalej na ekranie z wyborem daty granicznej postu
        private async void button13_Click(object sender, EventArgs e)
        {
            // schowaj biezacy panel
            panel_choose_msg_starting_date.Hide();

            // zainicjuj kolejny screen
            InitLikeCommenterPanel();

            // sprawdzenie daty z kalendarzyka
            posts_newer_than_timestamp = ToUnixTimestamp(dateTimePicker1.Value.ToUniversalTime());
            //MessageBox.Show(posts_newer_than_timestamp.ToString());

            // pobranie wiadomosci ze wszystkich kanalow
            this.toolStripStatusLabel1.Text = "Zaczynam szukanie linków w Telegramie!";
            this.Refresh();
            await GetTelegramChannelsMessages(posts_newer_than_timestamp);

            // pobranie wiadomosci ze wszystkich chatow
            await GetTelegramChatsMessages(posts_newer_than_timestamp);
            this.toolStripStatusLabel1.Text = "Zakończono szukanie linków w Telegramie!";
            this.Refresh();

            this.toolStripStatusLabel1.Text = "Zaczynam pobieranie postów z Instagrama!";
            this.Refresh();
            //DownloadPhotosFromTelegramDialogs();
            await DownloadFirstPhotoFromTelegramDialogs();
            this.toolStripStatusLabel1.Text = "Gotowe! Wszystkie Posty zostały pobrane - możesz teraz komentować!";
            this.Refresh();

            // odblokowanie listBoxa
            this.listBox1.Enabled = true;

            // wybierz pierwszy wpis na liście
            listBox1.SelectedIndex = 0;

            // ukrywa wyszukiwanie po dacie
            label37.Hide();
            dateTimePicker1.Hide();

            // pokaz kolejny screen
            panel_liker_commenter.Show();
        }

        private void InitChooseDate()
        {
            // zainicjuj labele
            date_support_groups = new List<Label>();
            date_support_groups_last_post_dates = new List<Label>();

            // pobierz liste grup wsparcia
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = "SELECT * FROM support_group_names";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                int i = 0;
                bool has_nulls = false;
                while (reader.Read())
                {
                    CreateDateLabels(i, reader.GetString(1), reader.GetInt32(3));

                    if(reader.GetInt32(3) == 0)
                    {
                        has_nulls = true;
                    }

                    i++;
                }

                //MessageBox.Show("Przynajmniej jedna z grup wsparcia nie została jeszcze ani razu skomentowana - w panelu po lewej jest kalendarzyk. Wybierz datę i godzinę od której mają zostać pobrane posty i kliknij dalej.");
                m_dbConnection.Close();
            }
        }

        private void CreateDateLabels(int row_nr, string support_group, int last_commented_post)
        {
            /*
             *  private List<Label> date_support_groups;
             *  private List<Label> date_support_groups_last_post_dates;
             */

            // labele z nazwami grup
            Label tmp_group_name = new Label();
            //tmp_group_name.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            tmp_group_name.Location = new System.Drawing.Point(25, 70 + (row_nr * 20));
            tmp_group_name.Name = "label_support_group_" + row_nr.ToString();
            //tmp_group_name.Size = new System.Drawing.Size(25, 25);
            tmp_group_name.TabIndex = 0;
            tmp_group_name.Text = support_group;
            //tmp_group_name.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            date_support_groups.Add(tmp_group_name);

            // labele z nazwami grup
            Label tmp_group_date = new Label();
            //tmp_group_name.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            tmp_group_date.Location = new System.Drawing.Point(276, 70 + (row_nr * 20));
            tmp_group_date.Name = "label_support_group_date_" + row_nr.ToString();
            //tmp_group_name.Size = new System.Drawing.Size(25, 25);
            tmp_group_date.TabIndex = 0;
            if(last_commented_post > 0)
            {
                tmp_group_date.Text = UnixTimeStampToDateTime(last_commented_post).ToString("dd-MM-yyyy HH:mm");
            }
            else
            {
                tmp_group_date.Text = "Jeszcze nigdy";
            }
            //tmp_group_name.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            date_support_groups_last_post_dates.Add(tmp_group_date);

            groupBox5.Controls.Add(date_support_groups.Last());
            groupBox5.Controls.Add(date_support_groups_last_post_dates.Last());
        }

        // kliknieto na groupBoxa4 od wybory daty recznie
        private void GroupBox4_Click(object sender, System.EventArgs e)
        {
            // odblokuj wszystkie kontrolsy na groupboxie4
            EnableGroupBoxControls(this.groupBox4);

            // zablokuj wszystkie kontrolsy na groupboxie5
            DisableGroupBoxControls(this.groupBox5);


        }

        // kliknieto na groupBoxa5 od pokazania kiedy skomentowano ostatnie posty dla danych grup
        private void GroupBox5_Click(object sender, System.EventArgs e)
        {
            // odblokuj wszystkie kontrolsy na groupboxie4
            EnableGroupBoxControls(this.groupBox5);

            // zablokuj wszystkie kontrolsy na groupboxie5
            DisableGroupBoxControls(this.groupBox4);


        }

        /* --- |SCREEN WYBORU DATY| --- */

        /* --- SCREEN LAJKOWANIA ZDJEC --- */

        private void InitLikeCommenterPanel(/*bool show_calendar*/)
        {
            // pokaz ile zdjec wymaga skomentowania
            label24.Text = media_to_comment_counter.ToString();

            // wypelnij danymi liste grup wsparcia
            foreach (ListViewItem support_group in listView3.Items)
            {
                listBox1.Items.Add(support_group.Text);
            }

            // wypelnij combobox komentarzy komentarzami z bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = "SELECT * FROM default_comments";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    comboBox1.Items.Add(reader["comment"].ToString());
                    //MessageBox.Show(reader["comment"].ToString());
                }
                m_dbConnection.Close(); 
            }

            // pokaz najczesciej uzywane emotki oraz przycisk wiecej
            // pokazuje emojis w labelach
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"SELECT * FROM emojis ORDER BY times_used DESC, id ASC LIMIT 8";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    int j = 0;
                    while (reader.Read())
                    {
                        CreateEmojiLabel(j, reader.GetString(reader.GetOrdinal("emoji")));
                        j++;
                    }
                }
            }

            // pokazuje przycisk wiecej...
            Label more_label = new Label();
            more_label.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            more_label.Location = new System.Drawing.Point(800 + (9 * 25), 330);
            more_label.Name = "more_label";
            more_label.Size = new System.Drawing.Size(35, 25);
            more_label.TabIndex = 0;
            more_label.Text = "...";
            more_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            more_label.Click += More_label_Click;

            emoji_labels.Add(more_label);
            this.panel_liker_commenter.Controls.Add(emoji_labels.Last());

            // jesli jest taka potrzeba - pokaz kalendarzyk
            /*if (show_calendar)
            {
                dateTimePicker1.Show();
                button17.Show();
            }
            else
            {
                dateTimePicker1.Hide();
                button17.Hide();
            }*/

            // pobierz wszystkie zdjecia do skomentowania

        }

        private void More_label_Click(object sender, EventArgs e)
        {
            EmojisWindow ee = new EmojisWindow(this);
            ee.ShowDialog();
        }

        public void InsertEmojiIntoRichTextBox(string emoji)
        {
            var selectionIndex = richTextBox2.SelectionStart;
            richTextBox2.Text = richTextBox2.Text.Insert(selectionIndex, emoji);
            textBox1.SelectionStart = selectionIndex + emoji.Length;
        }

        private void CreateEmojiLabel(int label_nr, string emoji)
        {
            int column = label_nr % 9;

            Label tmp_label = new Label();
            tmp_label.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            tmp_label.Location = new System.Drawing.Point(800 + (column * 25), 330);
            tmp_label.Name = "label" + label_nr.ToString();
            tmp_label.Size = new System.Drawing.Size(25, 25);
            tmp_label.TabIndex = 0;
            tmp_label.Text = emoji;
            tmp_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            tmp_label.Click += new System.EventHandler(this.label_Click);

            complete_emojis.Add(new SingleEmoji(emoji, label_nr));

            emoji_labels.Add(tmp_label);
            this.panel_liker_commenter.Controls.Add(emoji_labels.Last());
        }

        private void label_Click(object sender, EventArgs e)
        {
            Label tmp = (Label)sender;
            UpdateEmojiUsed(complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
            var selectionIndex = richTextBox2.SelectionStart;
            richTextBox2.Text = richTextBox2.Text.Insert(selectionIndex, complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
            textBox1.SelectionStart = selectionIndex + complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon.Length;
            //MessageBox.Show("Kliknieto " + complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
        }

        private void UpdateEmojiUsed(string emoji)
        {
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"SELECT * FROM emojis WHERE emoji = '{emoji}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    int times_used = -1;
                    while (reader.Read())
                    {
                        times_used = reader.GetInt32(reader.GetOrdinal("times_used"));
                    }
                    if (times_used != -1)
                    {
                        sql = $"UPDATE emojis SET times_used = {++times_used} WHERE emoji = '{emoji}'";
                        command = new SQLiteCommand(sql, m_dbConnection);
                        command.ExecuteNonQuery(); // nic nie zwraca 
                    }
                }
                m_dbConnection.Close();
            }
        }

        private InstagramPostInfo GetPostInfoByUrl(string url)
        {
            string urli = "";
            string id = "";
            int width = 0;
            int height = 0;
            InstagramPostInfo info = new InstagramPostInfo();

            try
            {
                var request = WebRequest.Create(url + "?__a=1");
                bool keep_going = true;
                int tries = 3;

                while (keep_going)
                {
                    try
                    {
                        using (var response = request.GetResponse())
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            string json = reader.ReadToEnd();
                            System.Diagnostics.Debug.Write(json);
                            JavaScriptSerializer ser = new JavaScriptSerializer();
                            dynamic jsonData = ser.Deserialize<dynamic>(json);

                            urli = jsonData["graphql"]["shortcode_media"]["display_resources"][0]["src"];
                            width = jsonData["graphql"]["shortcode_media"]["display_resources"][0]["config_width"];
                            height = jsonData["graphql"]["shortcode_media"]["display_resources"][0]["config_height"];
                            id = jsonData["graphql"]["shortcode_media"]["id"];
                            info = new InstagramPostInfo(urli, width, height, id);
                            return info;
                        }
                    }
                    catch (Exception ex1)
                    {
                        if (ex1.Message.Contains("(404)"))// -> mozna to pozniej wykorzystac w podsumowaniu, zeby pokazac mozliwe przyczyny
                        {
                            return new InstagramPostInfo();
                        }
                        else if (ex1.Message.Contains("timed out"))
                        {
                            //MessageBox.Show("Nie udało się pobrać postu - sprawdź czy masz połączenie z internetem i kliknij OK");
                            this.toolStripStatusLabel1.Text = this.toolStripStatusLabel1.Text + "*";
                            tries--;
                            if(tries <= 0)
                            {
                                keep_going = false;
                            }
                        }
                        else if (ex1.Message.Contains("(502)"))
                        {
                            //MessageBox.Show("Nie udało się pobrać postu - Instagram ma jakieś problemy... Kliknij OK aby spróbować pobrać to zdjęcie ponownie");
                            this.toolStripStatusLabel1.Text = this.toolStripStatusLabel1.Text + "^";
                            tries--;
                            if (tries <= 0)
                            {
                                keep_going = false;
                            }
                        }
                        else
                        {
                            MessageBox.Show("Nie udało się pobrać informacji o danym poście...\nLink: " + url + "\nZebrane dane\nURL: " + urli + "\nWidth: " + width.ToString() + "\nHeight: " + height.ToString() + "\nID: " + id + "\nKomunikat błędu: " + ex1.Message.ToString());
                            keep_going = false;
                        }
                    }
                }
                return new InstagramPostInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się wysłać zapytania do instagrama dot. postu o adresie: " + url + "\nKomunikat błędu: " + ex.Message.ToString());
                return new InstagramPostInfo();
            }
        }

        private InstagramPostInfo GetPostInfoByShortcode(string media_shortcode)
        {
            try
            {
                var request = WebRequest.Create("https://www.instagram.com/p/" + media_shortcode + "/?__a=1");

                try
                {
                    using (var response = request.GetResponse())
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        System.Diagnostics.Debug.Write(json);
                        JavaScriptSerializer ser = new JavaScriptSerializer();
                        dynamic jsonData = ser.Deserialize<dynamic>(json);

                        string url = jsonData["graphql"]["shortcode_media"]["display_resources"][0]["src"];
                        int width = jsonData["graphql"]["shortcode_media"]["display_resources"][0]["config_width"];
                        int height = jsonData["graphql"]["shortcode_media"]["display_resources"][0]["config_height"];
                        string id = jsonData["graphql"]["shortcode_media"]["id"];
                        InstagramPostInfo info = new InstagramPostInfo(url, width, height, id);
                        return info;
                    }
                }
                catch (Exception ex1)
                {
                    MessageBox.Show("2\n" + ex1.Message.ToString());
                    return new InstagramPostInfo();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("1\n" + ex.Message.ToString());
                return new InstagramPostInfo();
            }
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        private void ResizePictureBoxForPhoto(string file_path)
        {
            //MessageBox.Show("Resize: " + file_path);

            int photo_width = 0;
            int photo_height = 0;

            try
            {
                Image img = Image.FromFile(file_path);
                photo_width = img.Width;
                photo_height = img.Height;
                img.Dispose();
                img = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd przy sprawdzaniu rozdzielczości obrazka:\n" + ex.Message.ToString());
                return;
            }

            // to ustawic we wlasciwosciach
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;

            int new_photo_width = photo_width;
            int new_photo_height = photo_height;
            double resize_factor = 1.0;

            // jesli zdjecie za szerokie
            if (photo_width > MAX_PHOTO_WIDTH)
            {
                // przeskalowuje, aby calosc zachowala proporcje
                resize_factor = (double)MAX_PHOTO_WIDTH / (double)photo_width;
                new_photo_width = MAX_PHOTO_WIDTH;
                new_photo_height = (int)((double)photo_height * (double)resize_factor);

                // jesli nowa wysokosc (po przeskalowaniu aby szerokosc pasowala) nadal jest za duza, to jeszcze raz skaluje
                // tym razem wysokosc
                if (new_photo_height > MAX_PHOTO_HEIGHT)
                {
                    resize_factor = (double)MAX_PHOTO_HEIGHT / (double)photo_height;
                    new_photo_height = MAX_PHOTO_HEIGHT;
                    new_photo_width = (int)((double)photo_width * (double)resize_factor);
                }
            }
            // jesli szerokosc okej
            else
            {
                // sprawdza wysokosc - jesli za wysokie
                if (photo_height > MAX_PHOTO_HEIGHT)
                {
                    // przeskalowuje na podstawie wysokosci
                    resize_factor = (double)MAX_PHOTO_HEIGHT / (double)photo_height;
                    new_photo_height = MAX_PHOTO_HEIGHT;
                    new_photo_width = (int)((double)photo_width * (double)resize_factor);
                }
            }

            pictureBox2.Width = new_photo_width;
            pictureBox2.Height = new_photo_height;
        }

        private void ResizePictureBoxForPhoto(int photo_width, int photo_height)
        {
            //MessageBox.Show("Resize: " + file_path);

            /*int photo_width = 0;
            int photo_height = 0;

            try
            {
                Image img = Image.FromFile(file_path);
                photo_width = img.Width;
                photo_height = img.Height;
                img.Dispose();
                img = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd przy sprawdzaniu rozdzielczości obrazka:\n" + ex.Message.ToString());
                return;
            }*/

            // to ustawic we wlasciwosciach
            pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;

            int new_photo_width = photo_width;
            int new_photo_height = photo_height;
            double resize_factor = 1.0;

            // jesli zdjecie za szerokie
            if (photo_width > MAX_PHOTO_WIDTH)
            {
                // przeskalowuje, aby calosc zachowala proporcje
                resize_factor = (double)MAX_PHOTO_WIDTH / (double)photo_width;
                new_photo_width = MAX_PHOTO_WIDTH;
                new_photo_height = (int)((double)photo_height * (double)resize_factor);

                // jesli nowa wysokosc (po przeskalowaniu aby szerokosc pasowala) nadal jest za duza, to jeszcze raz skaluje
                // tym razem wysokosc
                if (new_photo_height > MAX_PHOTO_HEIGHT)
                {
                    resize_factor = (double)MAX_PHOTO_HEIGHT / (double)photo_height;
                    new_photo_height = MAX_PHOTO_HEIGHT;
                    new_photo_width = (int)((double)photo_width * (double)resize_factor);
                }
            }
            // jesli szerokosc okej
            else
            {
                // sprawdza wysokosc - jesli za wysokie
                if (photo_height > MAX_PHOTO_HEIGHT)
                {
                    // przeskalowuje na podstawie wysokosci
                    resize_factor = (double)MAX_PHOTO_HEIGHT / (double)photo_height;
                    new_photo_height = MAX_PHOTO_HEIGHT;
                    new_photo_width = (int)((double)photo_width * (double)resize_factor);
                }
            }

            pictureBox2.Width = new_photo_width;
            pictureBox2.Height = new_photo_height;
        }

        // po wybraniu komentarza z listy
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // jesli to nie pusty tekst
            if (comboBox1.Items[comboBox1.SelectedIndex].ToString() != "")
            {
                // wyczysc pole wpisania komentarza
                richTextBox2.Text = "";
                // wpisz w pole komentarza tekst komentarza z listy
                richTextBox2.Text = comboBox1.Items[comboBox1.SelectedIndex].ToString();
            }
        }

        public static long ToUnixTimestamp(DateTime target)
        {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, target.Kind);
            var unixTimestamp = System.Convert.ToInt64((target - date).TotalSeconds);

            return unixTimestamp;
        }

        // zmieniono date od kiedy sprawdzac komentarze
        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            //button17.Enabled = true;
        }

        // label38 -> nazwa uzytkownika, ktorego zdj jest wyswietlane
        // label39 -> pokazuje ile jeszcze postow w tej grupie zostalo do skomentowania

        // button pobrania zdjec dla okreslonej daty
        private async void button17_Click(object sender, EventArgs e)
        {
            // nothing now...
        }

        // button "zrob to" na ekranie lajkowania zdjec
        private void button10_Click(object sender, EventArgs e)
        {

        }

        // po kliknieciu na label z opisem postu na insta
        private void Label40_Click(object sender, System.EventArgs e)
        {
            //MessageBox.Show("Kliknieto na opis obrazka");
            int index = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex;
            PhotoInfo info_window = new PhotoInfo(false,  support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].Description);
            info_window.Show();
        }

        // button polajkuj zdjecie insta - tylko gdy wyłaczone autolikowanie
        private async void button11_Click(object sender, EventArgs e)
        {
            this.button11.Enabled = false;
            // zapisz w bazie danych, ze to zdjecie zostalo polajkowane
            int index = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex;
            support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].IsLiked = true;
            // polajkuj przez api
            await IGProc.Like(support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].InstaMediaID);
            // jesli wczesniej dodano rowniez komentarz - wyswietl kolejne zdjecie
            long tmstmp = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].TelegramMessageTimestamp;
            if (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].UpdateLiked(tmstmp))
            {
                // przesuniecie indeksu na kolejne zdjecie z grupy
                support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex++;
                // uaktualnienie w bazie danych dla tej grupy date ostatnio obrobionej wiadomosci
                support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().UpdateLastDoneMessage(tmstmp);
                media_to_comment_counter--;
                await PostDoneShowNext();
            } // jesli nie - ukryj przycisk lajkowania
            else
            {
                button11.Hide();
                button11.Enabled = true;
            }
        }

        // button dodaj komentarz insta
        private async void button12_Click(object sender, EventArgs e)
        {
            // zapisz w bazie danych, ze to zdjecie zostalo skomentowane
            int index = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex;
            support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].IsCommented = true;
            // dodaj komentarz przez instaAPI
            await IGProc.Comment(support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].InstaMediaID, richTextBox2.Text);
            // jesli wczesniej dodano rowniez komentarz - wyswietl kolejne zdjecie
            long tmstmp = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].TelegramMessageTimestamp;
            if (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].UpdateCommented(tmstmp, richTextBox2.Text))
            {
                // przesuniecie indeksu na kolejne zdjecie z grupy
                support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex++;
                // uaktualnienie w bazie danych dla tej grupy date ostatnio obrobionej wiadomosci
                support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().UpdateLastDoneMessage(tmstmp);
                media_to_comment_counter--;
                await PostDoneShowNext();
            } // jesli nie - ukryj przycisk lajkowania
            else
            {
                button12.Hide();
            }
        }

        // zaznaczono element na liscie -> pokaz zdjecia z tej grupy
        private async void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(listBox1.SelectedIndex != -1)
            {
                current_support_group = listBox1.SelectedIndex;
            }
        }

        private async Task PostDoneShowNext()
        {
            // blokuje interfejs
            this.listBox1.Enabled = false;
            this.Refresh();

            // pokaz ile zdjec wymaga skomentowania
            label24.Text = media_to_comment_counter.ToString();

            // wykasowanie starego obrazka z pictureboxa
            if (pictureBox2.Image != null)
            {
                pictureBox2.Image.Dispose();
            }

            // odblokowanie przycisków lajkowania i komentowania
            button11.Show();
            button12.Show();

            if (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).First() == null)
            {
                // nie ma takiej grupy wsparcia!
                MessageBox.Show("Nie ma takiej grupy wsparcia!");
                return;
            }

            if (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).First() != null)
            {
                if (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).First().InstagramPosts != null)
                {
                    // pokaz w labelu na dole ile zdjec w tej grupie wymaga uwagi, pod warunkiem, ze zainicjowano
                    if (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts.Count > 0)
                    {
                        int index = 0;
                        try
                        {
                            index = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().LastDonePostIndex;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Wystąpił błąd podczas pobierania indeksu ostatnio zrobionego postu\nKomunikat błędu:\n" + ex.Message.ToString());
                            return;
                        }

                        if (index > (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts.Count - 1))
                        {
                            label39.Text = "Wszystkie posty w tej grupie zrobione!";
                            pictureBox2.Image.Dispose();
                            pictureBox2.Image = null;
                        }
                        else
                        {
                            // jesli post istnieje i zostal poprawnie stworzony
                            if (support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[index].SuccessfullyCreated)
                            {
                                InstaMedia media;
                                try
                                {
                                    // pobranie obrazka
                                    media = await IGProc.GetInstaPost(support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[index].InstaMediaID);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Wystąpił błąd podczas pobierania obrazka (GetInstaPost)\nKomunikat błędu:\n" + ex.Message.ToString());
                                    return;
                                }

                                try
                                {
                                    // wpis nazwy uzytkownika wlasciciela postu
                                    support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[index].Description = media.Caption.Text;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Wystąpił błąd podczas uaktualniania opisu (Description)\nKomunikat błędu:\n" + ex.Message.ToString());
                                    return;
                                }

                                try
                                {
                                    // wpis opisu pod postem na insta
                                    support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[index].Owner = "@" + media.User.UserName;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Wystąpił błąd podczas uaktualniania autora postu (Owner)\nKomunikat błędu:\n" + ex.Message.ToString());
                                    return;
                                }

                                try
                                {
                                    // pokazanie pobranych danych
                                    label39.Text = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().GetPostsToDoCounter();
                                    label38.Text = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex].Owner;
                                    label40.Text = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex].Description;
                                    label22.Text = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex].TelegramMessage;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Wystąpił błąd podczas wyswietlania danych dot postu (ile zostalo postow do zrobienia, autor postu, opis, wiadomosc Telegrama)\nKomunikat błędu:\n" + ex.Message.ToString());
                                    return;
                                }
                                // wyswietl pierwsze zdjecie w pictureboxie
                                //string pic_path_jpg = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts.Where(x => (!x.IsLiked || !x.IsCommented)).FirstOrDefault().PicturePathJpg;

                                if (media.Images.Count > 0)
                                {
                                    var request = WebRequest.Create(media.Images[0].URI);

                                    using (var response = request.GetResponse())
                                    using (var stream = response.GetResponseStream())
                                    {
                                        try
                                        {
                                            ResizePictureBoxForPhoto(media.Images[0].Width, media.Images[0].Height);
                                            pictureBox2.Image = Bitmap.FromStream(stream);
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show("Błąd przy pokazywaniu zdjęcia!\n" + ex.Message.ToString());
                                        }
                                    }
                                }
                                else
                                {
                                    //MessageBox.Show("Nie udało się pobrać obrazka za pierwszym razem, ale próbuję jeszcze raz...");
                                    try
                                    {
                                        //ResizePictureBoxForPhoto(media.Images[0].Width, media.Images[0].Height);
                                        pictureBox2.Load(support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[index].PicturePathJpg);
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("Błąd przy pokazywaniu zdjęcia!\n" + ex.Message.ToString());
                                    }
                                }

                                // automatyczne polubienie postu
                                // ukryj przycisk lajkowania
                                button11.Hide();
                                try
                                {
                                    // zapisz, ze juz polajkowany w pamieci
                                    support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[index].IsLiked = true;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Wystąpił błąd podczas zapisu w pamieci RAM, że dany post jest już zrobiony\nKomunikat błędu:\n" + ex.Message.ToString());
                                    return;
                                }
                                // polajkuj przez api
                                await IGProc.Like(support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).FirstOrDefault().InstagramPosts[index].InstaMediaID);

                                //string pic_path_jpg = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).Single().InstagramPosts[index].PicturePathJpg;
                                //string pic_path_png = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).Single().InstagramPosts[index].PicturePathPng;

                                /*try
                                {
                                    //MessageBox.Show(pic_path_jpg);
                                    //ResizePictureBoxForPhoto(pic_path_jpg);
                                    pictureBox2.Image = new Bitmap(pic_path_jpg);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Nie udalo sie zaladowac obrazka...\n" + ex.Message.ToString());
                                    try
                                    {
                                        pictureBox2.Image = new Bitmap(pic_path_png);
                                    }
                                    catch (Exception ex1)
                                    {
                                        MessageBox.Show("Nie udalo sie zaladowac obrazka...\n" + ex1.Message.ToString());
                                    }
                                }*/ 
                            }
                        }
                    }
                    else
                    {
                        label39.Text = "Brak postów w tej grupie";
                        /*int selected_index = listBox1.SelectedIndex;
                        if (listBox1.SelectedIndex < (listBox1.Items.Count - 1))
                        {
                            listBox1.SelectedIndex++; 
                        }
                        else
                        {
                            if(listBox1.Items.Count > 1)
                            {
                                listBox1.SelectedIndex--;
                            }
                            else
                            {
                                listBox1.SelectedIndex = -1;
                            }
                        }
                        listBox1.Items.RemoveAt(selected_index);*/
                        label38.Text = "";
                        label40.Text = "Brak opisu";
                        label22.Text = "Brak wiadomości";
                        pictureBox2.Image = null;
                    }
                }
                else
                {
                    try
                    {
                        for (int i = 0; i < listBox1.Items.Count; i++)
                        {
                            if (listBox1.Items[i].ToString() == support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).Single().GroupName)
                            {
                                listBox1.Items.RemoveAt(i);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Jedna z grup nie utworzyla listy postow Instagrama i wystapil blad\n" + ex.Message.ToString());
                    }
                } 
            }
            // blokuje interfejs
            this.listBox1.Enabled = true;
            this.Refresh();
        }

        // button pomin zdjęcie
        private async void button16_Click(object sender, EventArgs e)
        {
            // oznacz to zdjecie jako polajkowane i skomentowane
            int index = 0;
            try
            {
                index = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas pobierania indeksu ostatnio polubionego/skomentowanego zdjęcia\nKomunikat błędu:\n" + ex.Message.ToString());
                return;
            }

            try
            {
                support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].UpdateLiked(0);
            }
            catch (Exception ex)
            {

                MessageBox.Show("Wystąpił błąd podczas uaktualniania statusu (polubiono zdjęcie)\nKomunikat błędu:\n" + ex.Message.ToString());
                return;
            }

            try
            {
                support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].UpdateCommented(0, "");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas uaktualniania statusu (skomentowano zdjęcie)\nKomunikat błędu:\n" + ex.Message.ToString());
                return;
            }

            try
            {
                support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex++;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas zwiększania indeksu ostatnio polubionego/skomentowanego zdjęcia\nKomunikat błędu:\n" + ex.Message.ToString());
                return;
            }

            long tmstmp = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].TelegramMessageTimestamp;
            support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().UpdateLastDoneMessage(tmstmp);
            // pokaz nastepne zdjecie z grupy
            // uaktualnienie w bazie danych dla tej grupy date ostatnio obrobionej wiadomosci
            media_to_comment_counter--;
            await PostDoneShowNext();
        }

        // kliknieto na zdjecie
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            // nothing...
        }

        // zaznaczono, ze grupa typu tylko like
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            // wyswietla okno "czy jestes pewien?"
            DialogResult result = MessageBox.Show("Jeśli oznaczysz tę grupę jako tylko like, nie będziesz miał możliwości skomentowania postów z tej grupy. Dopiero po ponownym uruchomieniu aplikacji, przed pobraniem postów, będziesz mógł zmienić te ustawienia.", "Jesteś pewien?", MessageBoxButtons.YesNo);
            switch (result)
            {
                case DialogResult.No:
                    // odznacz checkboxa
                    checkBox3.Checked = false;
                    break;

                case DialogResult.Yes:
                    // zablokuj checkboxa
                    checkBox3.Enabled = false;
                    break;
            }
        }
        /* --- |SCREEN LAJKOWANIA ZDJEC| --- */

        public async static Task MainAsync()
        {
            var userSession = new UserSessionData
            {
                UserName = "martha_farewell",
                Password = "rooney892"
            };

            _instaApi = InstaApiBuilder.CreateBuilder()
                    .SetUser(userSession)
                    .UseLogger(new DebugLogger(LogLevel.All)) // use logger for requests and debug messages
                    .SetRequestDelay(RequestDelay.Empty())
                    .Build();

            if(!_instaApi.IsUserAuthenticated)
            {
                // login
                Console.WriteLine($"Logging in as {userSession.UserName}");
                var logInResult = await _instaApi.LoginAsync();
                if (!logInResult.Succeeded)
                {
                    Console.WriteLine($"Unable to login: {logInResult.Info.Message}");
                }
                else
                {
                    MessageBox.Show("OMG! ZALOGOWANO!");
                    //await _instaApi.GetCurrentUserAsync();
                }
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // wyczysc pobrane obrazki
            if (pictureBox2.Image != null)
            {
                pictureBox2.Image.Dispose();
                pictureBox2.Image = null; 
            }

            /*foreach (SupportGroup group in support_groups)
            {
                try
                {
                    Directory.Delete(group.GetMediaFolderPath(), true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message.ToString());
                }
            }*/
        }

        // dodaje komentarz do Instagrama po wcisnieciu entera na richtextboxie do wpisania komentarza
        private void richTextBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button12.PerformClick();
            }
        }

        // dodaje komentarz  po wcisnieciu entera na comboboxie wyboru domyslnego komentarza
        private void comboBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button8.PerformClick();
            }
        }

        // dodaje domyslny komentarz do bazy danych po wcisnieciu entera
        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button3.PerformClick();
            }
        }

        // wysyla smsa z potwierdzajacym kodem Telegrama po wcisnieciu entera
        private void textBox4_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button15.PerformClick();
            }
        }

        // ostatecznie loguje do Telegrama po wcisnieciu entera
        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button6.PerformClick();
            }
        }

        // loguje do Instagrama po wcisnieciu entera na polu loginu
        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button14.PerformClick();
            }
        }

        // loguje do Instagrama po wcisnieciu entera na polu hasla
        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button14.PerformClick();
            }
        }

        // przenosi chat Telegrama do grup wsparcia
        private void listView2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button8.PerformClick();
            }
        }

        // przenosi grupe wsparcia z powrotem do chatow Telegrama 
        private void listView3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button9.PerformClick();
            }
        }

        // po kliknieciu na label z wiadomoscia instagrama
        private void label22_Click(object sender, EventArgs e)
        {
            if (pictureBox2.Image != null)
            {
                // wyswietl okienko z wiadomoscia ne telegramie
                // i podpisem zdjecia na insta
                int index = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().LastDonePostIndex;
                PhotoInfo info_window = new PhotoInfo(true, support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().InstagramPosts[index].TelegramMessage);
                //string message = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().GroupMessages.GetFilteredMessages().Where(x => x.)
                info_window.Show();
            }
        }

        private void dateTimePicker1_MouseDown(object sender, MouseEventArgs e)
        {
            //MessageBox.Show("Clicked!");
        }

        private void dateTimePicker1_KeyDown_1(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button13.PerformClick();
            }
        }
    }
}
