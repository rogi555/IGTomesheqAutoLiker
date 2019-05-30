using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Data.SQLite;
using TeleSharp.TL.Messages;
using TeleSharp.TL;
using System.Threading.Tasks;
using IGTomesheq;
using InstaSharper.Classes;
using System.Drawing;
using System.IO;
using InstaSharper.Classes.Models;
using System.Net;
using System.Web.Script.Serialization;
using TLSharp.Core.Network;
using InstaSharper.Classes.ResponseWrappers;

namespace IGTomesheqAutoLiker
{
    public partial class Form1 : Form
    {
        List<SingleEmoji> complete_emojis;
        List<Label> emoji_labels;

        int media_to_comment_counter;
        const int MAX_PHOTO_WIDTH = 376;
        const int MAX_PHOTO_HEIGHT = 376;

        int support_group_index;

        List<SupportGroup> all_support_groups;
        List<SupportGroup> comment_support_groups;

        EmojisWindow ee;

        InitDataHolder data_holder;
        bool is_form_initialized;

        int total_posts_liked;
        int total_posts_to_like;
        bool last_like_skipped; // jesli z jakiegos powodu, ostatniego postu nie trzeba bylo likowac

        // dla listBox2 z nazwami grup wsparcia
        //global brushes with ordinary/selected colors
        private SolidBrush backgroundBrushSelected = new SolidBrush(Color.FromKnownColor(KnownColor.Highlight));
        private SolidBrush backgroundBrushOnlyLike = new SolidBrush(Color.Aqua);
        private SolidBrush backgroundBrush = new SolidBrush(Color.White);

        List<Panel> followers_panels;
        List<Label> followers_labels;
        List<InstaFollower> insta_bad_followers;
        int followers_page;
        bool followers_manager_initialized;

        public Form1(InitDataHolder holder)
        {
            InitializeComponent();
            data_holder = holder;

            // ogolne
            media_to_comment_counter = 0;
            support_group_index = -1;
            followers_page = 1;

            // utworzenie obiektu z klasami emoji
            complete_emojis = new List<SingleEmoji>();
            emoji_labels = new List<Label>();
            ee = new EmojisWindow(this);

            all_support_groups = new List<SupportGroup>();
            comment_support_groups = new List<SupportGroup>();

            this.toolStripStatusLabel1.Text = "Program gotowy do działania! Kliknij dalej...";

            followers_manager_initialized = false;

            // umiejscowienie okna
            this.CenterToScreen();
            is_form_initialized = false;
            total_posts_liked = 0;
            total_posts_to_like = 0;
            last_like_skipped = false;
        }

        public bool Initialize()
        {
            if (!is_form_initialized)
            {
                // telegram
                if (data_holder.client.IsUserAuthorized() && data_holder.telegram_ok && data_holder.telegram_channels_and_chats_ok)
                {
                    SetUpTelegramPanel(InitDataHolder.ShowTelegramPanelWith.LoginSuccess);
                }
                else
                {
                    SetUpTelegramPanel(InitDataHolder.ShowTelegramPanelWith.InsertPhoneNumber);
                }

                // instagram
                SetUpInstagramPanel(data_holder.InstaLoginStatus);              

                // domyślne komentarze
                if (data_holder.default_comments.Count > 0)
                {
                    InitDefaultCommentsList(data_holder.default_comments);
                }

                // grupy wsparcia
                if ((data_holder.channels != null) && (data_holder.chats != null))
                {
                    InitSupportGroupsPanel(data_holder.channels, data_holder.chats, data_holder.initial_support_groups);
                    CheckIfSupportGroupsExist();
                }
                else
                {
                    // nie udalo sie pobrac danych z telegrama - co robic?!
                }

                if(listBox2.SelectedIndex == -1)
                {
                    SetUpSupportGroupsSettingsPanel(-1);
                }

                InitSettingsPanel();

                HideAllSettingsPanels(true); // wywolanie z true, zeby zaznaczyc, ze to inicjalizacja

                is_form_initialized = true;
            }
            return true;
        }

        private void ResetAllBadFollowersPanels(List<InstaFollower> bad_followers)
        {
            foreach(var bad_follower in bad_followers)
            {
                bad_follower.SetPanelNr(-1);
            }
        }

        private void SetBadFollowersPanelNumbers(int page, List<InstaFollower> bad_followers)
        {
            int i_panels = 0; // zawsze 0-39
            int i_bad_followers = (page - 1) * 40;

            for (int i = 0; i < bad_followers.Count; i++)
            {
                // gdy i nalezy do strony
                if (i >= ((page - 1) * 40) && (i < (page) * 40))
                {
                    // ustaw numer panela na odpowiedni
                    bad_followers[i].SetPanelNr(i_panels);

                    // zwieksz numer
                    i_panels++; 
                }
                else
                {
                    // ustaw numer panela na pusty
                    bad_followers[i].SetPanelNr(-1);
                }
            }
        }

        private async Task GenerateBadFollowersList(List<InstaUserShort> bad_followers)
        {
            // utworz liste
            insta_bad_followers = new List<InstaFollower>();

            // dla kazdego uzytkownika, ktorego followujesz, a on cb nie
            foreach (InstaUserShort bad_follower in bad_followers)
            {
                // sprawdz w bazie danych czy juz ta osoba istnieje
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                {
                    m_dbConnection.Open();

                    string sql = $"SELECT * FROM instagram_followers WHERE user_name = '{bad_follower.UserName}' AND status <> 0";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    SQLiteDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        // odczytaj
                        reader.Read();
                        // dodaj do listy
                        insta_bad_followers.Add(new InstaFollower(data_holder, await DownloadPhotoFromUrl(bad_follower.ProfilePicture), bad_follower.UserName, bad_follower.Pk, (FollowerState)reader.GetInt32(2)));
                    }
                    else
                    {
                        // dodaj do listy
                        insta_bad_followers.Add(new InstaFollower(data_holder, await DownloadPhotoFromUrl(bad_follower.ProfilePicture), bad_follower.UserName, bad_follower.Pk));

                        // dodaj do bazy danych
                        sql = $"INSERT INTO instagram_followers (id, user_name, status) VALUES (NULL, '{bad_follower.UserName}', 1)";
                        command = new SQLiteCommand(sql, m_dbConnection);
                        command.ExecuteNonQuery(); // nic nie zwraca
                    }

                    m_dbConnection.Close();
                }
            }
        }

        private void CreateEmptyFollowersPanels()
        {
            followers_panels = new List<Panel>();
            followers_labels = new List<Label>();

            // panele w rzedzie
            for(int i = 0; i < 4; i++)
            {
                // panele w kolumnie
                for(int j = 0; j < 10; j++)
                {
                    int element_nr = i * 10 + j;

                    Label new_label = new Label();
                    new_label.BackColor = System.Drawing.Color.White;
                    new_label.Location = new System.Drawing.Point(3, 84);
                    new_label.Name = "label_"+element_nr.ToString();
                    new_label.Size = new System.Drawing.Size(92, 13);
                    //new_label.TabIndex = 8;
                    new_label.Text = "...";
                    new_label.TextAlign = ContentAlignment.MiddleCenter;

                    followers_labels.Add(new_label);

                    Panel new_panel = new Panel();
                    //this.panel10.BackgroundImage = global::IGTomesheq.Properties.Resources.Instagram_icon;
                    //this.panel10.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
                    new_panel.BackColor = Color.White;
                    new_panel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                    new_panel.Controls.Add(followers_labels.Last());
                    new_panel.Location = new System.Drawing.Point(48 + j*105, 135 + i*103);
                    new_panel.Name = "panel_"+element_nr.ToString();
                    new_panel.Size = new System.Drawing.Size(100, 100);

                    new_panel.Click += new System.EventHandler(this.follower_panel_Click);
                    new_panel.Paint += new System.Windows.Forms.PaintEventHandler(this.follower_panel_Paint);

                    //System.Diagnostics.Debug.WriteLine(element_nr.ToString()+"/n");

                    followers_panels.Add(new_panel);

                    panel_followers_manager.Controls.Add(followers_panels.Last());
                }
            }
        }

        private void CreateBadFollowerPanel(int page, List<InstaFollower> users)
        {
            int i_panel = 0;

            for (int i = 0; i < users.Count; i++)
            {
                if (i >= ((page - 1) * 40) && (i < (page * 40)))
                {
                    // jesli numer kafelka nie jest wiekszy niz liczba uzytkownikow
                    if (users.Count > i_panel)
                    {
                        if (users[i] != null)
                        {
                            // znajduje panel odpowiedzialny za tego followersa
                            //Control ctr = this.Controls.Find(users[i].panel_name, true).FirstOrDefault();
                            Control ctr_panel = this.Controls.Find("panel_"+i_panel.ToString(), true).FirstOrDefault();
                            if (ctr_panel != null)
                            {
                                ctr_panel.Show();
                                ctr_panel.BackgroundImage = users[i].profile_pic;
                                ctr_panel.BackgroundImageLayout = ImageLayout.Zoom;

                                // znajduje label odpowiedzialny za tego followersa
                                //ctr = this.Controls.Find(users[i].label_name, true).FirstOrDefault();
                                Control ctr_label = this.Controls.Find("label_" + i_panel.ToString(), true).FirstOrDefault();
                                if (ctr_label != null)
                                {
                                    ctr_label.Text = users[i].user_name;
                                }

                                ctr_panel.Refresh();
                            }
                        }
                    }
                    i_panel++;
                }
            }

            // jesli nie wszystkie panele dostaly zdjecie, ukryj przycisk przewijania dalej
            if(i_panel < 39)
            {
                panel_right_arrow.Hide();
            }
        }

        private void HideAllTiles()
        {
            for (int i = 0; i < 40; i++)
            {
                Control ctr = this.Controls.Find("panel_" + i.ToString(), true).FirstOrDefault();

                if(ctr != null)
                {
                    ctr.Hide();
                }
            }
        }

        private void PleaseWaitShowSinglePost()
        {
            // pokaz panel
            this.panel_please_wait.Show();

            // pokaz w labelu, ze laduje post
            this.label_wait_info.Text = "Przygotowuję post...";

            // ukryj label_progress - w tym przypadku nie jest potrzebny
            this.label_progress.Hide();
        }

        private void PleaseWaitLikeInLoop(SupportGroup support_group)
        {
            // pokaz panel
            this.panel_please_wait.Show();

            // pokaz w labelu, ze laduje post
            this.label_wait_info.Text = $"Lajkuje wszystkie posty z grupy {support_group.Name}";

            // ukryj label_progress - w tym przypadku nie jest potrzebny
            this.label_progress.Show();
            this.label_progress.Text = SetupProgressLabel(0, support_group.MessagesWithInstaPosts.Count);
        }

        // Zwraca tekst do labela z progresem likowania/komentowania
        private string SetupProgressLabel(int current_index, int no_of_posts)
        {
            int percent = (int)(((double)current_index / (double)no_of_posts) * 100);
            string ret = "";
            ret = $"{current_index}/{no_of_posts} ({percent}%)";

            return ret;
        }

        // pobiera domyslne komentarze do listy do wyswietlenia
        private bool InitDefaultCommentsList(List<DefaultComment> default_comments)
        {
            try
            {
                foreach (DefaultComment comment in default_comments)
                {
                    listView1.Items.Add(comment.Comment); 
                }
                return true;
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("\n" + ex.Message);
                return false;
            }
        }

        // inicjalizuje panel grup wsparcia
        private bool InitSupportGroupsPanel(List<TLChannel> channels, List<TLChat> chats, List<SupportGroup> initial_support_groups)
        {
            try
            {
                foreach(TLChannel channel in channels)
                {
                    listView2.Items.Add(channel.Title);
                }
                foreach (TLChat chat in chats)
                {
                    listView2.Items.Add(chat.Title);
                }
                foreach (SupportGroup group in initial_support_groups)
                {
                    listView2.Items.Remove(listView2.FindItemWithText(group.Name));
                    listView3.Items.Add(group.Name);
                    listBox2.Items.Add(group.Name);
                    if(group.Settings.OnlyLike)
                    {
                        //listBox2.Items[listBox2.Items.Count - 1].BackColor = Color.Aqua;
                    }
                    all_support_groups.Add(group);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("\n" + ex.Message);
                return false;
            }
        }

        // inicjalizuje panel ustawien
        private void InitSettingsPanel()
        {
            textBox_likes_limit.Text = data_holder.likes_limit.ToString();
            textBox_min_time_betw_likes.Text = data_holder.min_time_betw_likes.ToString();
            textBox_max_time_betw_likes.Text = data_holder.max_time_betw_likes.ToString();
            textBox_keep_old_entries.Text = data_holder.keep_old_entries.ToString();
        }

        // odpowiada za wyswietlanie okienka z zarzadzaniem kontem inagrama
        private void SetUpTelegramPanel(InitDataHolder.ShowTelegramPanelWith show_option)
        {
            // panel6 -> panel z labelem z info o nieudanym logowaniu
            // panel5 -> panel z labele z info o pomyślnym logownai
            // panel4 -> panel z kodem bezpieczeństwa
            // panel3 -> panel z numerem telefonu

            switch(show_option)
            {
                case InitDataHolder.ShowTelegramPanelWith.InsertPhoneNumber:
                    panel6.Hide();
                    panel5.Hide();
                    panel4.Hide();
                    panel3.Show();
                    break;

                case InitDataHolder.ShowTelegramPanelWith.InsertSecurityCode:
                    panel6.Hide();
                    panel5.Hide();
                    panel4.Show();
                    panel3.Hide();
                    break;

                case InitDataHolder.ShowTelegramPanelWith.LoginFailed:
                    panel6.Show();
                    panel5.Hide();
                    panel4.Hide();
                    panel3.Show();
                    break;

                case InitDataHolder.ShowTelegramPanelWith.LoginSuccess:
                    panel6.Hide();
                    panel5.Show();
                    panel4.Hide();
                    panel3.Hide();
                    break;

                default:
                    panel6.Hide();
                    panel5.Hide();
                    panel4.Hide();
                    panel3.Show();
                    break;
            }
        }

        // odpowiada za wyswietlanie okienka z zarzadzaniem kontem telegrama
        private void SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith show_option)
        {
            // panel1 -> panel loginu i hasła w panelu insta
            // panel2 -> panel kodu bezpieczeństwa w panelu insta
            // panel7 -> panel logowanie udane
            // panel8 -> panel logowanie nieudane

            switch (show_option)
            {
                case InitDataHolder.ShowInstagramPanelWith.InsertLoginData:
                    panel_insta_login_data.Show();
                    panel_insta_challenge_code.Hide();
                    panel_insta_login_success.Hide();
                    panel_insta_2FactAuth.Hide();
                    panel_insta_login_failed_info.Hide();
                    break;

                case InitDataHolder.ShowInstagramPanelWith.InsertSecurityCode:
                    label62.Text = $"Witaj @{IGProc.login}!";
                    panel_insta_login_data.Hide();
                    panel_insta_challenge_code.Show();
                    panel_insta_login_success.Hide();
                    panel_insta_2FactAuth.Hide();
                    panel_insta_login_failed_info.Hide();
                    break;

                case InitDataHolder.ShowInstagramPanelWith.LoginFailed:
                    panel_insta_login_data.Show();
                    panel_insta_challenge_code.Hide();
                    panel_insta_login_success.Hide();
                    panel_insta_2FactAuth.Hide();
                    panel_insta_login_failed_info.Show();
                    break;

                case InitDataHolder.ShowInstagramPanelWith.LoginSuccess:
                    label56.Text = $"Witaj @{IGProc.login}!";
                    panel_insta_login_data.Hide();
                    panel_insta_challenge_code.Hide();
                    panel_insta_login_success.Show();
                    panel_insta_2FactAuth.Hide();
                    panel_insta_login_failed_info.Hide();
                    break;

                case InitDataHolder.ShowInstagramPanelWith.Insert2FactCode:
                    label_insta_2FactAuth_welcome.Text = $"Witaj @{IGProc.login}!";
                    panel_insta_login_data.Hide();
                    panel_insta_challenge_code.Hide();
                    panel_insta_2FactAuth.Show();
                    panel_insta_login_success.Hide();
                    panel_insta_login_failed_info.Hide();
                    break;

                default:
                    panel_insta_login_data.Show();
                    panel_insta_challenge_code.Hide();
                    panel_insta_login_success.Hide();
                    panel_insta_login_failed_info.Hide();
                    panel_insta_2FactAuth.Hide();
                    break;
            }
        }

        // button zapisz kod potwierdzajacy od Telegrama
        private async void button6_Click(object sender, EventArgs e)
        {
            // informuje uzytkownika co sie dzieje
            this.toolStripStatusLabel1.Text = "Trwa potwierdzanie otrzymanego kodu...";
            // jesli nie wpisano kodu, nic nie rob
            // if (textBox1.Text.Length == 0)
            //    return;
            // jesli kod poprawny (?) to zapisz go
            //code = textBox1.Text;

            // dodaj do bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE telegram_data SET phone_number = '{data_holder.phone_number}' WHERE phone_number = ''";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close(); 
            }


            data_holder.user = await data_holder.client.MakeAuthAsync(data_holder.phone_number, data_holder.hash, data_holder.code);
            // wyswietl komunikat, gdy logowanie przebieglo pomyslnie
            if (data_holder.user.Id > 0)
            {
                // informacja o pomyslnym zalogowaniu do telegrama
                this.toolStripStatusLabel1.Text = "Pomyslnie zalogowano do telegrama!";
                
            }
        }

        // button telegram zapisz nr telefonu i wyslij kod
        private async void button15_Click(object sender, EventArgs e)
        {
            // zapisz nr telefonu w bazie danych
            /*if (textBox4.Text.Length == 9)
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
            }*/

            // jesli nr telefonu jest poprawny to go pokaz
            //MessageBox.Show("Nr telefonu: " + this.phone_number);

            // wyslij kod telegrama
            data_holder.hash = await data_holder.client.SendCodeRequestAsync(data_holder.phone_number);

            // informacja o oczekiwaniu na kod z Telegrama
            this.toolStripStatusLabel1.Text = $"Na konto Telegram przypisane do numeru {data_holder.phone_number} został wysłany kod potwierdzający - wpisz go w polu powyżej";
        }

        // sprawdza czy zapamiętana w DB grupa wsparcia faktycznie istnieje w Telegramie
        private void CheckIfSupportGroupsExist()
        {
            foreach (ListViewItem item in listView3.Items)
            {
                bool exists = false;
                foreach (var channel in data_holder.channels)
                {
                    if (channel.Title == item.Text)
                    {
                        exists = true;
                    }
                }
                foreach (var chat in data_holder.chats)
                {
                    if (chat.Title == item.Text)
                    {
                        exists = true;
                    }
                }

                if (!exists)
                {
                    MessageBox.Show($"Wygląda na to, że nie należysz już do grupy wsparcia \"{item.Text}\".\nUsuwam tę grupę z listy. Aby zarządzać grupami wsparcia wybierz w menu \"WYBÓR GRUP WSPARCIA\"");
                    listView3.Items.Remove(item);
                    for (int i = 0; i < listBox2.Items.Count; i++)
                    {
                        if (listBox2.Items[i].ToString() == item.Text)
                        {
                            listBox2.Items.RemoveAt(i);
                        }
                    }
                    using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                    {
                        m_dbConnection.Open();
                        string sql = $"DELETE FROM support_groups WHERE group_name = '{item.Text}'";
                        SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                        command.ExecuteNonQuery(); // nic nie zwraca
                        m_dbConnection.Close();
                    }
                }
            }
        }

        /* --- SCREEN Z GRUPAMI WSPARCIA TELEGRAMA --- */

        private async Task<List<TLMessage>> GetTelegramMessagesFromChannel(SupportGroup support_group)
        {
            // informacja 
            this.toolStripStatusLabel1.Text = "Szukam linków w grupie " + support_group.Name + "...";
            this.Refresh();

            // utworzenie zmiennej z wiadomosciami
            List<TLMessage> TLmessages = new List<TLMessage>();

            // sprawdza jaka wiadomosc z czatu byla obsluzona (like & comment) jako ostatnia
            long download_timestamp = 0;
            switch (support_group.Settings.StartingDateMethod)
            {
                case StartingDateMethod.ChosenFromCalendar:
                    download_timestamp = support_group.Settings.CalendarDateTimestamp;
                    break;

                case StartingDateMethod.LastPost:
                    download_timestamp = support_group.Settings.LastCommentedPostTimestamp;
                    break;

                case StartingDateMethod.LastXHours:
                    download_timestamp = ToUnixTimestamp(DateTime.Now.Subtract(TimeSpan.FromHours(support_group.Settings.LastHours)));
                    break;

                case StartingDateMethod.NotInitialized:
                    download_timestamp = support_group.Settings.CalendarDateTimestamp;
                    break;
            }

            if (download_timestamp > 0)
            {
                if (data_holder.channels != null)
                {
                    if (data_holder.channels.Where(x => x.Title == support_group.Name).Count() > 0)
                    {
                        TLChannel channel = data_holder.channels.Where(x => x.Title == support_group.Name).First();

                        if (channel != null)
                        {
                            if (channel.AccessHash != null)
                            {
                                // pobiera zbior wiadomosci dla danego kanalu
                                try
                                {
                                    var peer = new TLInputPeerChannel() { ChannelId = channel.Id, AccessHash = (long)channel.AccessHash.Value };
                                    var msgs = await data_holder.client.GetHistoryAsync(peer, 0, 0, 100);
                                    if (msgs is TLChannelMessages)
                                    {
                                        var messag = msgs as TLChannelMessages;
                                        //MessageBox.Show("Liczba wiadomości w channelu " + channel.Title + " = " + messag.Count.ToString());
                                        if (messag.Count <= 100)
                                        {
                                            var messages = messag.Messages;

                                            foreach (var message in messages)
                                            {
                                                if (message is TLMessage)
                                                {
                                                    var m = message as TLMessage;
                                                    // sprawdzenie czy ta wiadomość już była
                                                    if (m.Date <= download_timestamp)
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
                                                        if (m.Date <= download_timestamp)
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
                                                        msgs = await data_holder.client.GetHistoryAsync(peer, total, 0, 100);
                                                    }
                                                    catch (FloodException ex)
                                                    {
                                                        int seconds_to_wait = (int)ex.TimeToWait.TotalSeconds + 1;
                                                        for (int i = 0; i < seconds_to_wait; i++)
                                                        {
                                                            this.toolStripStatusLabel1.Text = "Próbujemy pobrać zbyt wiele wiadomości w zbyt krótkim czasie - Telegram się broni... Czekam " + (seconds_to_wait - i).ToString() + " sekund...";
                                                            this.Refresh();
                                                            await Wait1Second();
                                                        }
                                                        this.toolStripStatusLabel1.Text = "Szukam dalej linków w grupie " + channel.Title + "...";
                                                        this.Refresh();
                                                        try
                                                        {
                                                            msgs = await data_holder.client.GetHistoryAsync(peer, total, 0, 100);
                                                        }
                                                        catch (FloodException)
                                                        {
                                                            MessageBox.Show("Telegram znowu się broni... Poczekaj cierpliwie :(");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Wystąpił błąd podczas pobierania wiadomości! Problem spowodowany jest przez niedziałające serwery Telegrama - aplikacja zostanie zamknięta. Uruchom ją ponownie i spróbuj jeszcze raz\n" + ex.Message.ToString());
                                }
                            }
                        }
                        else // sprawdza chaty, jesli nie znalazlo w channelach
                        {
                            if (data_holder.chats != null)
                            {
                                if (data_holder.chats.Where(x => x.Title == support_group.Name).Count() > 0)
                                {
                                    TLChat chat = data_holder.chats.Where(x => x.Title == support_group.Name).First();

                                    if (chat != null)
                                    {
                                        // pobierz wiadomosci dla danego chatu
                                        var peer = new TLInputPeerChat() { ChatId = chat.Id };
                                        TLAbsMessages msgs = await data_holder.client.GetHistoryAsync(peer, 0, -1, 100);

                                        if (msgs is TLMessages)
                                        {
                                            var messages = msgs as TLMessages;

                                            foreach (var message in messages.Messages)
                                            {
                                                if (message is TLMessage)
                                                {
                                                    var m = message as TLMessage;
                                                    // sprawdzenie czy ta wiadomość już była
                                                    if (m.Date <= download_timestamp)
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
                                                        if (m.Date <= download_timestamp)
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
                                                        msgs = await data_holder.client.GetHistoryAsync(peer, total, 0, 0);
                                                    }
                                                    catch (FloodException ex)
                                                    {
                                                        int seconds_to_wait = (int)ex.TimeToWait.TotalSeconds;
                                                        MessageBox.Show("Próbujemy pobrać zbyt wiele wiadomości w zbyt krótkim czasie - Telegram się przed tym broni i nie udostępni wiadomości przez " + seconds_to_wait.ToString() + " sekund. Poczekaj cierpliwie i nic nie rób, a aplikacja sama wznowi działanie.");
                                                        this.toolStripStatusLabel1.Text = "Telegram się broni... Czekam " + seconds_to_wait.ToString() + " sekund...";
                                                        this.Refresh();
                                                        while (seconds_to_wait > 0)
                                                        {
                                                            toolStripStatusLabel1.Text = $"Telegram się broni... Czekam " + seconds_to_wait.ToString() + " sekund...";
                                                            await Wait1Second();
                                                            seconds_to_wait--;
                                                        }
                                                        this.toolStripStatusLabel1.Text = "Szukam dalej linków w grupie " + chat.Title + "...";
                                                        this.Refresh();
                                                        try
                                                        {
                                                            msgs = await data_holder.client.GetHistoryAsync(peer, total, 0, 100);
                                                        }
                                                        catch (FloodException)
                                                        {
                                                            MessageBox.Show("Telegram znowu się wyjebał... (Chaty)");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    TLmessages = TLmessages.OrderBy(x => x.Date).ToList();
                                    return TLmessages;
                                }
                            }
                        } 
                    }
                    else
                    {
                        if (data_holder.chats != null)
                        {
                            if (data_holder.chats.Where(x => x.Title == support_group.Name).Count() > 0)
                            {
                                TLChat chat = data_holder.chats.Where(x => x.Title == support_group.Name).First();

                                if (chat != null)
                                {
                                    // pobierz wiadomosci dla danego chatu
                                    var peer = new TLInputPeerChat() { ChatId = chat.Id };
                                    TLAbsMessages msgs = await data_holder.client.GetHistoryAsync(peer, 0, -1, 100);

                                    if (msgs is TLMessages)
                                    {
                                        var messages = msgs as TLMessages;

                                        foreach (var message in messages.Messages)
                                        {
                                            if (message is TLMessage)
                                            {
                                                var m = message as TLMessage;
                                                // sprawdzenie czy ta wiadomość już była
                                                if (m.Date <= download_timestamp)
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
                                                    if (m.Date <= download_timestamp)
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
                                                    msgs = await data_holder.client.GetHistoryAsync(peer, total, 0, 0);
                                                }
                                                catch (FloodException ex)
                                                {
                                                    int seconds_to_wait = (int)ex.TimeToWait.TotalSeconds;
                                                    MessageBox.Show("Próbujemy pobrać zbyt wiele wiadomości w zbyt krótkim czasie - Telegram się przed tym broni i nie udostępni wiadomości przez " + seconds_to_wait.ToString() + " sekund. Poczekaj cierpliwie i nic nie rób, a aplikacja sama wznowi działanie.");
                                                    this.toolStripStatusLabel1.Text = "Telegram się broni... Czekam " + seconds_to_wait.ToString() + " sekund...";
                                                    this.Refresh();
                                                    while (seconds_to_wait > 0)
                                                    {
                                                        toolStripStatusLabel1.Text = $"Telegram się broni... Czekam " + seconds_to_wait.ToString() + " sekund...";
                                                        await Wait1Second();
                                                        seconds_to_wait--;
                                                    }
                                                    this.toolStripStatusLabel1.Text = "Szukam dalej linków w grupie " + chat.Title + "...";
                                                    this.Refresh();
                                                    try
                                                    {
                                                        msgs = await data_holder.client.GetHistoryAsync(peer, total, 0, 100);
                                                    }
                                                    catch (FloodException)
                                                    {
                                                        MessageBox.Show("Telegram znowu się wyjebał... (Chaty)");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                TLmessages = TLmessages.OrderBy(x => x.Date).ToList();
                                return TLmessages;
                            }
                        }
                    }
                } 
            }

            TLmessages = TLmessages.OrderBy(x => x.Date).ToList();
            return TLmessages;
        }

        /* TA METODA BEDZIE USUNIETA */
        private async Task DownloadPhotoFromSupportGroupMessages(int support_group_index, int msg_index)
        {
            if (support_group_index > -1)
            {
                try
                {
                    if (all_support_groups != null)
                    {
                        if (all_support_groups[support_group_index].areThereAnyMessages())
                        {
                            // pobiera obrazek z wiadomości Telegrama
                            string base64img = "";

                            base64img = await DownloadPhotoFromTelegramWebPageMessage(all_support_groups[support_group_index].MessagesWithInstaPosts[msg_index].TelegramMessage);

                            // sprawdza czy nie wystąpił błąd przy pobieraniu
                            if (base64img.Contains("Ta wiadomość") || base64img.Contains("Nie udało"))
                            {
                                MessageBox.Show("Wystąpił błąd:\n" + base64img);
                            }
                            else
                            {
                                // wyswietlenie zdjecia w pictureBox2
                                var pic = Convert.FromBase64String(base64img);
                                pictureBox2.Image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(pic));
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
            }
            else // nie ma zadnej grupy na liscie
            {
                MessageBox.Show("Brak grup wsparcia!");
            }
        }

        private async Task<string> DownloadPhotoFromTelegramWebPageMessage(TLMessage message)
        {
            string ret = "";
            if (message.Media != null)
            {
                // zapisuje obiekt Media z wiadomosci
                var media_msg = message.Media;

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
                        if (web_page.Photo != null)
                        {
                            // zapisuje zdjecie jako obiekt typu TLPhoto do dalszej obrobki
                            TeleSharp.TL.TLPhoto photo = (TeleSharp.TL.TLPhoto)web_page.Photo;
                            // tworzy liste z rozmiarami dostepnych zdjec
                            List<TLPhotoSize> photo_sizes = new List<TLPhotoSize>();
                            // pobiera rozmiary dostepne z Telegrama do utworzonej listy
                            foreach (var size in photo.Sizes.ToList())
                            {
                                photo_sizes.Add((TLPhotoSize)size);
                            }
                            // pobiera informacje o lokalizacji zdjecia do pobrania
                            TLFileLocation file_location = (TLFileLocation)photo_sizes.Last().Location;
                            // tworzy wydmuszkę pliku zdjęcia
                            TeleSharp.TL.Upload.TLFile resFile = new TeleSharp.TL.Upload.TLFile();
                            // probuje pobrac zdjecie z serwera
                            try
                            {
                                resFile = await data_holder.client.GetFile(new TLInputFileLocation
                                {
                                    LocalId = file_location.LocalId,
                                    Secret = file_location.Secret,
                                    VolumeId = file_location.VolumeId
                                    }, (int)Math.Pow(2, Math.Ceiling(Math.Log(photo_sizes.Last().Size, 2))));
                            }
                            catch (Exception)
                            {
                                return "Nie udało się pobrać zdjęcia z serwera";
                            }

                            // zapisuje pobrany ciag znakow jako zdj w formacie base64
                            using (var ms = new MemoryStream(resFile.Bytes))
                            {
                                byte[] byteArr = ms.ToArray();
                                string base64image = Convert.ToBase64String(byteArr);
                                //pictureBox2.Image = Image.FromStream(ms);
                                return base64image;
                            }
                        }
                        else
                        {
                            return "Ta wiadomość nie zawiera zdjęcia";
                        }
                    }
                    else
                    {
                        return "Ta wiadomość nie zawiera odnośnika do strony internetowej";
                    }
                }
                else
                {
                    return "Ta wiadomość Telegrama nie zawiera poprawnego odnośnika do Instagrama";
                }
            }
            return ret;
        }

        private InstagramPostInfo GetInstaPostInfoByShortcode(string media_shortcode)
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

        private InstagramPostInfo GetInstaPostInfoByUrli(string url)
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
                            //this.toolStripStatusLabel1.Text = this.toolStripStatusLabel1.Text + "*";
                            tries--;
                            if (tries <= 0)
                            {
                                keep_going = false;
                            }
                        }
                        else if (ex1.Message.Contains("(502)"))
                        {
                            //MessageBox.Show("Nie udało się pobrać postu - Instagram ma jakieś problemy... Kliknij OK aby spróbować pobrać to zdjęcie ponownie");
                            //this.toolStripStatusLabel1.Text = this.toolStripStatusLabel1.Text + "^";
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

        private async Task<Image> DownloadPhotoFromUrl(string url)
        {
            try
            {
                var request = WebRequest.Create(url);

                using (var response = await request.GetResponseAsync())
                using (var stream = response.GetResponseStream())
                {
                    try
                    {
                        return Bitmap.FromStream(stream);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Błąd przy pokazywaniu zdjęcia!\n" + ex.Message.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd pobierania zdjęcia!\n" + ex.Message);
            }

            return new Bitmap(10,10);
        }

        private string GetDescriptionFromTelegramWebPageMessage(TLMessage message)
        {
            string ret = "";
            if (message.Media != null)
            {
                // zapisuje obiekt Media z wiadomosci
                var media_msg = message.Media;

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
                        if (web_page.Description != null)
                        {
                            return web_page.Description;
                        }
                        else
                        {
                            return "Ta wiadomość nie zawiera opisu";
                        }
                    }
                    else
                    {
                        return "Ta wiadomość nie zawiera odnośnika do strony internetowej";
                    }
                }
                else
                {
                    return "Ta wiadomość Telegrama nie zawiera poprawnego odnośnika do Instagrama";
                }
            }
            return ret;
            // webpage.description
        }

        // button przenies chat jako grupe wsparcia
        private void button8_Click(object sender, EventArgs e)
        {
            if (listView2.SelectedIndices.Count > 0)
            {
                // wyswietl na liscie grup wsparcia w panelu wyboru grup
                listView3.Items.Add(listView2.SelectedItems[0].Text);
                listBox2.Items.Add(listView2.SelectedItems[0].Text);

                // dodaj grupe wsparcia w logice
                all_support_groups.Add(new SupportGroup(listView2.SelectedItems[0].Text));

                // dodaj do bazy danych
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"INSERT INTO support_groups (id_group, group_name, only_likes, last_done_msg, last_hours, last_done_msg_author, starting_date_method) VALUES (NULL, '{listView2.SelectedItems[0].Text}', 0, 0, 0, '', 0)";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                    m_dbConnection.Close(); 
                }

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

                using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"DELETE FROM support_groups WHERE group_name = '{listView3.SelectedItems[0].Text}'";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                    m_dbConnection.Close(); 
                }

                //var tmp_listview_item = listBox2.Items.Find(listView3.SelectedItems[0].Text, false);
                int tmp1 = listBox2.Items.IndexOf(listView3.SelectedItems[0].Text);
                int tmp2 = all_support_groups.FindIndex(x => x.Name == listView3.SelectedItems[0].Text);
                listBox2.Items.RemoveAt(tmp1);
                all_support_groups.RemoveAt(tmp2);
                listView3.SelectedItems[0].Remove();
            }
        }

        /* --- |SCREEN Z GRUPAMI WSPARCIA TELEGRAMA| --- */

        /* --- SCREEN WYBORU DATY --- */
        
        // button dalej na ekranie z wyborem daty granicznej postu
        private async void button13_Click(object sender, EventArgs e)
        {
            // sprawdza czy jest zalogowany do wszystkich portali - jesli nie, nie pozwoli przejsc do likowania
            if(data_holder.instagram_ok == false || data_holder.telegram_ok == false || data_holder.telegram_channels_and_chats_ok == false || all_support_groups.Count == 0)
            {
                MessageBox.Show("Nie mogę rozpocząć lajkowania - za mało danych... Sprawdź czy:\n1. Jesteś zalogowany do Instagrama\n2. Jesteś zalogowany do Telegrama\n3. Wybrałeś co najmniej jedną grupę wsparcia\nPo poprawnym zalogowaniu i wybraniu grup wsparcia kliknij przycisk Dalej ponownie.");
                return;
            }

            // ukrywa biezacy 
            panel_settings.Hide();

            // ukrywa przycisk przejdz dalej do komentowania
            panel_liker_button_go_on.Show();

            // pokazuje panel likera
            panel_liker.Show();

            // sortuje grupy wsparcia
            all_support_groups = all_support_groups.OrderByDescending(x => x.Settings.OnlyLike).ToList();

            // wyswietla grupy wsparcia na liscie
            foreach(SupportGroup group in all_support_groups)
            {
                AddSupportGroupLabel(group);
            }

            // dla kazdej grupy wsparcia, dla ktorej zostalo wybrane ustawienie "data z kalendarzyka" przepisuje date z kalendarza
            AssignCalendarDateToSupportGroups(dateTimePicker1.Value);

            // pobranie wiadomosci ze wszystkich kanalow
            this.toolStripStatusLabel1.Text = "Zaczynam szukanie linków w Telegramie!";
            this.Refresh();

            // pobiera wiadomosci z dialogow
            foreach (var group in all_support_groups)
            {
                if (!group.Settings.SkipThisTime)
                {
                    ActivateSupportGroupLabel(group);
                    var tmp = await GetTelegramMessagesFromChannel(group);
                    group.AddAndFilterMessages(tmp);
                    UpdateSupportGroupLabel(group, group.MessagesWithInstaPosts.Count);
                    total_posts_to_like += group.MessagesWithInstaPosts.Count;
                    DeactivateSupportGroupLabel(group); 
                }
            }

            // sprawdzenie czy liczba postow nie przekracza limitu likow
            if(total_posts_to_like > data_holder.likes_limit)
            {
                DialogResult box = MessageBox.Show("Ustawiłeś limit lajków na " + data_holder.likes_limit.ToString() + ", a do polajkowania jest " + total_posts_to_like.ToString() + " wiadomości\n\nCzy chcesz pominąć limit?\nJeśli klikniesz TAK, wszystkie posty ("+ total_posts_to_like.ToString() +") zostaną polajkowane, bez limitu.\nJeśli klikniesz NIE, tylko "+ data_holder.likes_limit.ToString() +" postów zostanie polajkowanych, zgodnie z limitem.", "Zbyt dużo wiadomości do polajkowania... Co robić?", MessageBoxButtons.YesNo);
                if(box == DialogResult.Yes)
                {
                    data_holder.likes_limit = total_posts_to_like;
                }
            }

            // lajkuje
            foreach (var group in all_support_groups)
            {
                // pogrubia grupe na panelu, zeby zwrocic uwage
                ActivateSupportGroupLabel(group);

                // jesli grupa jest do polajkowania
                if (group.Settings.OnlyLike && !group.Settings.SkipThisTime)
                {
                    // licznik lajkow
                    int likes_count = 0;

                    // pokazuje ile lajkow zrobiono
                    UpdateSupportGroupLabel(group, -1, likes_count);
                    likes_count++;

                    // lajkuje w petli
                    while (await OnlyLikePost(group))
                    {
                        // pokazuje ile lajkow zrobiono
                        UpdateSupportGroupLabel(group, -1, likes_count);
                        ReportLikingProgress();

                        if (total_posts_liked > data_holder.likes_limit)
                        {
                            MessageBox.Show("Osiągnięto limit lajków (1000) - przerywam lajkowanie");
                            break;
                        }

                        if (!last_like_skipped)
                        {
                            Random random = new Random();
                            int wait_t = random.Next(data_holder.min_time_betw_likes, data_holder.max_time_betw_likes);
                            while (wait_t > 0)
                            {
                                toolStripStatusLabel1.Text = $"Czekam {wait_t} sekund...";
                                await Wait1Second();
                                wait_t--;
                            } 
                        }

                        likes_count++;
                        last_like_skipped = false;
                    }

                    // po skonczonym lajkowaniu male oszustwo ;)
                    UpdateSupportGroupLabel(group, -1, group.MessagesWithInstaPosts.Count);
                }
                
                // usuwa pogrubienie grupy na panelu
                DeactivateSupportGroupLabel(group);
            }

            // pokazuje przycisk przejscia do ekranu komentowania
            panel_liker_button_go_on.Show();

            // pokazuje przycisk przejdz dalej do komentowania
            panel_liker_button_go_on.Show();
        }

        private async Task Wait1Second()
        {
            await Task.Delay(1000);
        }

        /* --- |SCREEN WYBORU DATY| --- */

        /* --- SCREEN LAJKOWANIA ZDJEC --- */

        private void InitLikeCommenterPanel(/*bool show_calendar*/)
        {
            // pokaz ile zdjec wymaga skomentowania
            label24.Text = media_to_comment_counter.ToString();

            // odsianie tylko grup wsparcia do skomentowania
            comment_support_groups = all_support_groups.Where(x => !x.Settings.OnlyLike && !x.Settings.SkipThisTime).ToList();

            // wypelnij danymi liste grup wsparcia typu "tylko like"
            foreach(var group in comment_support_groups)
            {
                listBox1.Items.Add(group.Name);
            }

            // wypelnij combobox komentarzy komentarzami z bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
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
            UpdateMostPopularEmojis();
        }

        // uaktualnia najbardziej popularne emojis na panelu liker komenter
        private void UpdateMostPopularEmojis()
        {
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
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
                    this.panel_commenter.Controls.Add(emoji_labels.Last());
                }
            }
        }

        private void More_label_Click(object sender, EventArgs e)
        {
            //EmojisWindow ee = new EmojisWindow(this);
            ee.ShowDialog();
        }

        public void InsertEmojiIntoRichTextBox(string emoji)
        {
            var selectionIndex = richTextBox2.SelectionStart;
            richTextBox2.Text = richTextBox2.Text.Insert(selectionIndex, emoji);
            richTextBox2.SelectionStart = selectionIndex + emoji.Length;
            UpdateMostPopularEmojis();
        }

        private void CreateEmojiLabel(int label_nr, string emoji)
        {
            // jesli labele z najczęściej używanymi emojis zostały już utworzone, podmienia tylko tekst
            if (this.panel_commenter.Controls.Find("label_emoji_" + label_nr.ToString(), true).Count() > 0)
            {
                this.panel_commenter.Controls.Find("label_emoji_" + label_nr.ToString(), true).FirstOrDefault().Text = emoji;
            }
            else // jesli labele z najczęściej używanymi emojis nie zostały jeszcze utworzone, tworzy je
            {
                int column = label_nr % 9;

                Label tmp_label = new Label();
                tmp_label.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
                tmp_label.Location = new System.Drawing.Point(800 + (column * 25), 330);
                tmp_label.Name = "label_emoji_" + label_nr.ToString();
                tmp_label.Size = new System.Drawing.Size(25, 25);
                tmp_label.TabIndex = 0;
                tmp_label.Text = emoji;
                tmp_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
                tmp_label.Click += new System.EventHandler(this.label_Click);

                complete_emojis.Add(new SingleEmoji(emoji, label_nr));

                emoji_labels.Add(tmp_label);
                this.panel_commenter.Controls.Add(emoji_labels.Last());
            }
            this.Refresh();
        }

        /* LABELE Z EMOJIS */
        private void label_Click(object sender, EventArgs e)
        {
            Label tmp = (Label)sender;
            UpdateEmojiUsed(complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
            var selectionIndex = richTextBox2.SelectionStart;
            richTextBox2.Text = richTextBox2.Text.Insert(selectionIndex, complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
            richTextBox2.SelectionStart = selectionIndex + complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon.Length;
            UpdateMostPopularEmojis();
            //MessageBox.Show("Kliknieto " + complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
        }

        // zapisuje w bazie danych, ze dana emoji zostala uzyta
        private void UpdateEmojiUsed(string emoji)
        {
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
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

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }

        public static long ToUnixTimestamp(DateTime target)
        {
            var date = new DateTime(1970, 1, 1, 0, 0, 0, target.Kind);
            var unixTimestamp = System.Convert.ToInt64((target - date).TotalSeconds);

            return unixTimestamp;
        }

        // DO POPRAWKI
        private void ResizePictureBoxForPhoto(int photo_width, int photo_height)
        {
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

        // label38 -> nazwa uzytkownika, ktorego zdj jest wyswietlane
        // label39 -> pokazuje ile jeszcze postow w tej grupie zostalo do skomentowania

        private async Task<bool> ShowPost()
        {
            // zamraża interfejs użytkownika
            FreezeUI();

            // pobiera dla danej grupy wsparcia index wiadomosci do pokazania
            int current_message_index = all_support_groups[this.support_group_index].MessageIndex;

            // jesli w tej grupie istnieją posty do pokazania
            if (current_message_index > -1)
            {
                // ukrywa label o braku postow w grupie
                label43.Hide();

                // jesli jeszcze nie utworzono postu Instagrama dla tej wiadomosci, to go tworzy
                if (all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost == null)
                {
                    // pobranie zdjecia oraz opisu z TelegramMessage
                    try
                    {
                        string media_id = await IGProc.GetMediaIdFromUrl(all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].Url);
                        IResult<InstaMedia> tmp_post = await IGProc.GetInstaPost(media_id);
                        all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].CreateInstagramPost(tmp_post.Value);
                        // jesli foto juz skomentowane lub to twoje zdjecie pomin
                        bool already_commented = await IGProc.FindMyComment(all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.InstaIdentifier, IGProc.login);
                        if (already_commented || all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.PhotoOfYou)
                        {
                            // zwiększ indeks wiadomości
                            all_support_groups[this.support_group_index].IncrementMessageIndex();
                            // rozmroź UI
                            DefrostUI();
                            // zwróć false, żeby z miejsca wywołania mogło kolejny raz wywołać tę funkcję
                            return false;
                        }
                        // polajkuj post
                        await IGProc.Like(all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.InstaIdentifier);
                        total_posts_liked++;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("TO WAŻNE! Wystąpił błąd! Zrób screena tego okienka i wyślij do Bubu 8)\n" + ex.Message);
                        // zwiększ indeks wiadomości
                        all_support_groups[this.support_group_index].IncrementMessageIndex();
                        // rozmroź UI
                        DefrostUI();
                        // zwróć false, żeby z miejsca wywołania mogło kolejny raz wywołać tę funkcję
                        return true;
                    }
                }

                // opis postu na insta
                label40.Text = all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.Caption.Text;
                // wiadomosc telegrama
                label22.Text = all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].TelegramMessage.Message;
                // autor postu
                label38.Text = "@" + all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.User.UserName;
                // liczba postow w grupie
                label39.Text = all_support_groups[this.support_group_index].GetPostsToDoCounter();
                // pokazuje zdjecie w pictureboxie
                Image img;
                try
                {
                    // jesli post typu carousel
                    if (all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.Carousel != null)
                    {
                        img = await DownloadPhotoFromUrl(all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.Carousel[0].Images[0].URI);
                    }
                    else
                    {
                        img = await DownloadPhotoFromUrl(all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.Images[0].URI);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("TO WAŻNE! Wystąpił błąd! Wyślij screena tego okienka do Bubu\nDownloadPhotoFromUrl\n" + ex.Message);
                    DefrostUI();
                    return false;
                }
                if (img.Size.Height == 10 && img.Size.Width == 10)
                {
                    MessageBox.Show("Nie udało się załadować zdjęcia...");
                    if (pictureBox2.Image != null)
                    {
                        pictureBox2.Image.Dispose();
                        pictureBox2.Image = null;
                    }
                }
                else
                {
                    pictureBox2.Image = img;
                }
            }
            // gdy w tej grupie wsparcia nie ma wgl postów (i nie było)
            else if(current_message_index == -1)
            {
                // wyswietla label z informacja o braku postow w tej grupie
                label43.Text = "Brak postów do skomentowania w tej grupie!";
                label43.Show();
                // opis postu na insta
                label40.Text = "Brak";
                // wiadomosc telegrama
                label22.Text = "Brak";
                // autor postu
                label38.Text = "@noname";
                // liczba postow w grupie
                label39.Text = "Brak postów w tej grupie";
                // pokazuje zdjecie w pictureboxie
                if (pictureBox2.Image != null)
                {
                    pictureBox2.Image.Dispose();
                    pictureBox2.Image = null;
                }
            }
            // gdy w tej grupie wsparcia nie ma wgl postów (i nie było)
            else
            {
                // wyswietla label z informacja o braku postow w tej grupie
                label43.Text = "Wszystkie posty w tej grupie zostały skomentowane!";
                label43.Show();
                // opis postu na insta
                label40.Text = "Brak";
                // wiadomosc telegrama
                label22.Text = "Brak";
                // autor postu
                label38.Text = "@noname";
                // liczba postow w grupie
                label39.Text = "Wszystkie posty w tej grupie zostały skomentowane!";
                // pokazuje zdjecie w pictureboxie
                if (pictureBox2.Image != null)
                {
                    pictureBox2.Image.Dispose();
                    pictureBox2.Image = null; 
                }
            }

            // rozmraża interfejs użytkownika
            DefrostUI();

            return true;
        }

        private bool IsPostAlreadyLiked(string insta_media_id)
        {
            bool ret_val = false;
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"SELECT * FROM instagram_posts WHERE insta_media_id = '{insta_media_id}' AND liked = 1";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    // wszystko OK - nic nie rob
                    ret_val = true;
                }
                else
                {
                    ret_val = false;
                }
                m_dbConnection.Close();
            }

            return ret_val;
        }

        private async Task<bool> OnlyLikePost(SupportGroup group)
        {
            // pobiera dla danej grupy wsparcia index wiadomosci do pokazania
            int current_message_index = group.MessageIndex;

            // jesli w tej grupie istnieją posty do pokazania
            if (current_message_index > -1)
            {
                // pobranie zdjecia oraz opisu z TelegramMessage
                try
                {
                    // znajdz media ID
                    string media_id = await IGProc.GetMediaIdFromUrl(group.MessagesWithInstaPosts[current_message_index].Url);

                    // sprawdz czy post nie zostal juz czasem polajkowany - jesli tak, pomin tym razem
                    if (IsPostAlreadyLiked(media_id))
                    {
                        group.IncrementMessageIndex();
                        total_posts_liked++;
                        last_like_skipped = true;
                        return true;
                    }

                    // pobierz post
                    IResult<InstaMedia> tmp_post = await IGProc.GetInstaPost(media_id);
                    
                    // polajkuj post
                    await IGProc.Like(tmp_post.Value.InstaIdentifier);
                    
                    // zwieksz indeks wiadomosci
                    group.IncrementMessageIndex();
                    total_posts_liked++;

                    // dodaj do bazy danych info o polajkowaniu postu
                    using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                    {
                        m_dbConnection.Open();
                        string sql = $"INSERT INTO instagram_posts (id_post, author, insta_media_id, insta_media_shortcode, date_published, commented, comment_text, date_commented, liked, date_liked) VALUES (NULL, '{tmp_post.Value.User.UserName}', '{tmp_post.Value.InstaIdentifier}', '{tmp_post.Value.Code}', {ToUnixTimestamp(tmp_post.Value.TakenAt)}, 0, '', NULL, 1, {ToUnixTimestamp(DateTime.Now)})";
                        SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                        command.ExecuteNonQuery(); // nic nie zwraca
                        m_dbConnection.Close();
                    }
                    
                    // zapisz w bazie danych, kiedy ostatnio polajkowano posta w tej grupie
                    group.UpdateLastDoneMessage(ToUnixTimestamp(tmp_post.Value.TakenAt));
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Błąd podczas likownaia: {ex.Message}");
                    group.IncrementMessageIndex();
                    last_like_skipped = true;

                    return true;
                }
            }
            // gdy w tej grupie wsparcia nie ma wgl postów (i nie było)
            else if (current_message_index == -1)
            {
                return false;
            }
            // gdy w tej grupie wsparcia nie ma wgl postów (i nie było)
            else
            {
                return false;
            }

            return true;
        }

        private void ReportLikingProgress()
        {
            panel_liker_label_progress_count.Text = total_posts_liked.ToString() + " / " + total_posts_to_like.ToString();
            double tmp = ((double)total_posts_liked / (double)total_posts_to_like) * 100.0;
            int perc = Convert.ToInt32(tmp);
            panel_liker_label_progress_percent.Text = perc.ToString() + "%";
        }

        // zamraża interfejs użytkownika, żeby nie mógł nic kliknąć
        private void FreezeUI()
        {
            // zamraża wszystkie kontrolki
            this.panel_commenter.Enabled = false;
            // pokazuje animowane kółeczko "loading"
            this.pictureBox3.Show();
            // odświeża interfejs
            this.Refresh();
        }

        // rozmraża interfejs użytkownika, żeby znowu mógł klikać
        private void DefrostUI()
        {
            // rozmraża wszystkie kontrolki
            this.panel_commenter.Enabled = true;
            // chowa animowane kółeczko "loading"
            this.pictureBox3.Hide();
            // odświeża interfejs
            this.Refresh();
        }

        // button dodaj komentarz insta
        private async void button12_Click(object sender, EventArgs e)
        {
            
        }

        // button pomin zdjęcie
        private async void button16_Click(object sender, EventArgs e)
        {
            // pobierz index postu
            int current_message_index = all_support_groups[this.support_group_index].MessageIndex;
            // zmniejsz licznik liczby postow do zrobienia
            media_to_comment_counter--;
            // zwiększ index grupy wsparcia
            if (all_support_groups[this.support_group_index].IncrementMessageIndex())
            {
                while (!(await ShowPost())) ;
            }
        }

        // zaznaczono element na liscie -> pokaz zdjecia z tej grupy
        private async void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(listBox1.SelectedIndex != -1)
            {
                this.support_group_index = listBox1.SelectedIndex;
                if (!comment_support_groups[this.support_group_index].Settings.SkipThisTime)
                {
                    // wyswietla odpowiedni post
                    if (!all_support_groups[support_group_index].Settings.OnlyLike)
                    {
                        while (!(await ShowPost()));


                        // po skonczonym likowaniu przechodzi do kolejnej grupy
                        if ((listBox1.SelectedIndex + 1) < listBox1.Items.Count)
                        {
                            listBox1.SelectedIndex++;
                        }
                    }
                    else
                    {
                        
                    } 
                }
                else
                {
                    // gdy zaznaczono omijanie tej grupy, przechodzi do nastepnej
                    if ((listBox1.SelectedIndex + 1) < listBox1.Items.Count)
                    {
                        listBox1.SelectedIndex++;
                    }
                }
            }
        }

        // button "zrob to" na ekranie lajkowania zdjec
        private void button10_Click(object sender, EventArgs e)
        {

        }

        // tymczasowo tutaj
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

        // po kliknieciu na label z opisem postu na insta
        private void Label40_Click(object sender, System.EventArgs e)
        {
            int index = all_support_groups.Where(x => (x.Name == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().MessageIndex;
            PhotoInfo info_window = new PhotoInfo(false, all_support_groups.Where(x => (x.Name == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().MessagesWithInstaPosts[index].InstagramPost.Description);
            info_window.Show();
        }

        // po kliknieciu na label z wiadomoscia instagrama
        private void label22_Click(object sender, EventArgs e)
        {
            if (pictureBox2.Image != null)
            {
                // wyswietl okienko z wiadomoscia ne telegramie
                // i podpisem zdjecia na insta
                int index = all_support_groups.Where(x => (x.Name == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().MessageIndex;
                PhotoInfo info_window = new PhotoInfo(true, all_support_groups.Where(x => (x.Name == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().MessagesWithInstaPosts[index].TelegramMessage.Message);
                //string message = support_groups.Where(x => (x.GroupName == listBox1.Items[listBox1.SelectedIndex].ToString())).SingleOrDefault().GroupMessages.GetFilteredMessages().Where(x => x.)
                info_window.Show();
            }
        }

        /* --- |SCREEN LAJKOWANIA ZDJEC| --- */

        private async void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            // wyczysc pobrane obrazki
            if (pictureBox2.Image != null)
            {
                pictureBox2.Image.Dispose();
                pictureBox2.Image = null; 
            }

            // wyloguj z insta
            if (IGProc.IsUserAuthenticated())
            {
                await IGProc.Logout(); 
            }
        }

        // dodaje komentarz do Instagrama po wcisnieciu entera na richtextboxie do wpisania komentarza
        private async void richTextBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                FreezeUI();
                // pobierz index postu
                int current_message_index = all_support_groups[this.support_group_index].MessageIndex;
                // dodaj komentarz
                await IGProc.Comment(all_support_groups[this.support_group_index].MessagesWithInstaPosts[current_message_index].InstagramPost.InstaPost.InstaIdentifier, richTextBox2.Text);
                // zmniejsz licznik liczby postow do zrobienia
                //media_to_comment_counter--;
                // zwiększ index grupy wsparcia
                if (all_support_groups[this.support_group_index].IncrementMessageIndex())
                {
                    while (!(await ShowPost())) ;
                }
                else
                {
                    // skomentowano już ostatni post - pokaż tylko raz
                    await ShowPost();
                }
                DefrostUI();
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
                //button15.PerformClick();
            }
        }

        // ostatecznie loguje do Telegrama po wcisnieciu entera
        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                //button6.PerformClick();
            }
        }

        // loguje do Instagrama po wcisnieciu entera na polu loginu
        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                //button14.PerformClick();
            }
        }

        // loguje do Instagrama po wcisnieciu entera na polu hasla
        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                //button14.PerformClick();
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

        private void dateTimePicker1_KeyDown_1(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button13.PerformClick();
            }
        }

        // button pobrania zdjec dla okreslonej daty
        private void button17_Click(object sender, EventArgs e)
        {
            // nothing now...
        }

        // zmieniono date od kiedy sprawdzac komentarze
        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            // ??
        }

        // kliknieto na zdjecie
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            // nothing...
        }

        private void label_settings_menu_choose_date_Click(object sender, EventArgs e)
        {
            HideAllSettingsPanels();
            panel_choose_starting_date.Show();
            label_settings_menu_choose_date.BackColor = System.Drawing.Color.DarkSalmon;
            label_settings_menu_choose_date.Font = new Font(label_settings_menu_choose_date.Font, FontStyle.Italic);
        }

        private void label_settings_menu_choose_support_groups_Click(object sender, EventArgs e)
        {
            HideAllSettingsPanels();
            panel_settings_choose_support_groups.Show();
            label_settings_menu_choose_support_groups.BackColor = System.Drawing.Color.DarkSalmon;
            label_settings_menu_choose_support_groups.Font = new Font(label_settings_menu_choose_support_groups.Font, FontStyle.Italic);
        }

        private void label_settings_menu_support_groups_Click(object sender, EventArgs e)
        {
            HideAllSettingsPanels();
            panel_settings_support_groups.Show();
            label_settings_menu_support_groups.BackColor = System.Drawing.Color.DarkSalmon;
            label_settings_menu_support_groups.Font = new Font(label_settings_menu_support_groups.Font, FontStyle.Italic);
            // odswieza widok
            label64.Text = GetDateFromCalendar(); 
        }

        private void label_settings_menu_instagram_Click(object sender, EventArgs e)
        {
            HideAllSettingsPanels();
            panel_settings_instagram.Show();
            label_settings_menu_instagram.BackColor = System.Drawing.Color.DarkSalmon;
            label_settings_menu_instagram.Font = new Font(label_settings_menu_instagram.Font, FontStyle.Italic);
        }

        private void label_settings_menu_telegram_Click(object sender, EventArgs e)
        {
            HideAllSettingsPanels();
            panel_settings_telegram.Show();
            label_settings_menu_telegram.BackColor = System.Drawing.Color.DarkSalmon;
            label_settings_menu_telegram.Font = new Font(label_settings_menu_telegram.Font, FontStyle.Italic);
        }

        private void label_settings_menu_default_comments_Click(object sender, EventArgs e)
        {
            HideAllSettingsPanels();
            panel_settings_default_comments.Show();
            label_settings_menu_default_comments.BackColor = System.Drawing.Color.DarkSalmon;
            label_settings_menu_default_comments.Font = new Font(label_settings_menu_default_comments.Font, FontStyle.Italic);
        }

        private void label_settings_menu_settings_Click(object sender, EventArgs e)
        {
            HideAllSettingsPanels();
            panel_settings_settings.Show();
            label_settings_menu_settings.BackColor = System.Drawing.Color.DarkSalmon;
            label_settings_menu_settings.Font = new Font(label_settings_menu_settings.Font, FontStyle.Italic);
        }

        private void HideAllSettingsPanels(bool init = false)
        {
            // ukryj wszystkie panele
            panel_settings_choose_support_groups.Hide();
            panel_settings_default_comments.Hide();
            panel_settings_instagram.Hide();
            panel_settings_support_groups.Hide();
            panel_settings_telegram.Hide();
            panel_choose_starting_date.Hide();
            panel_settings_settings.Hide();

            // pokoloruj tło wszystkich labeli w menu na szaro
            if (data_holder.instagram_ok)
            {
                label_settings_menu_instagram.BackColor = System.Drawing.Color.Green;

                label_settings_menu_followers_manager.BackColor = System.Drawing.SystemColors.AppWorkspace;
                label_settings_menu_followers_manager.Font = new Font(label_settings_menu_settings.Font, FontStyle.Bold);
                label_settings_menu_followers_manager.ForeColor = System.Drawing.Color.DarkBlue;
            }
            else
            {
                label_settings_menu_instagram.BackColor = System.Drawing.Color.Red;

                label_settings_menu_followers_manager.BackColor = System.Drawing.SystemColors.GrayText;
                label_settings_menu_followers_manager.Font = new Font(label_settings_menu_settings.Font, FontStyle.Regular);
                label_settings_menu_followers_manager.ForeColor = System.Drawing.SystemColors.AppWorkspace;
            }
            label_settings_menu_instagram.Font = new Font(label_settings_menu_instagram.Font, FontStyle.Regular);

            if (data_holder.telegram_ok)
            {
                label_settings_menu_telegram.BackColor = System.Drawing.Color.Green;
            }
            else
            {
                label_settings_menu_telegram.BackColor = System.Drawing.Color.Red;
            }
            label_settings_menu_telegram.Font = new Font(label_settings_menu_telegram.Font, FontStyle.Regular);

            label_settings_menu_choose_date.BackColor = System.Drawing.SystemColors.AppWorkspace;
            label_settings_menu_choose_date.Font = new Font(label_settings_menu_choose_date.Font, FontStyle.Regular);

            if (all_support_groups.Count > 0)
            {
                label_settings_menu_choose_support_groups.BackColor = System.Drawing.SystemColors.AppWorkspace;
                label_settings_menu_choose_support_groups.Font = new Font(label_settings_menu_choose_support_groups.Font, FontStyle.Regular);
            }
            else
            {
                label_settings_menu_choose_support_groups.BackColor = System.Drawing.Color.Red;
                label_settings_menu_choose_support_groups.Font = new Font(label_settings_menu_choose_support_groups.Font, FontStyle.Regular);
            }

            label_settings_menu_default_comments.BackColor = System.Drawing.SystemColors.AppWorkspace;
            label_settings_menu_default_comments.Font = new Font(label_settings_menu_default_comments.Font, FontStyle.Regular);

            label_settings_menu_support_groups.BackColor = System.Drawing.SystemColors.AppWorkspace;
            label_settings_menu_support_groups.Font = new Font(label_settings_menu_support_groups.Font, FontStyle.Regular);

            label_settings_menu_settings.BackColor = System.Drawing.SystemColors.AppWorkspace;
            label_settings_menu_settings.Font = new Font(label_settings_menu_settings.Font, FontStyle.Regular);

            if (init)
            {
                panel_settings_support_groups.Show();
                label_settings_menu_support_groups.BackColor = System.Drawing.Color.DarkSalmon;
                label_settings_menu_support_groups.Font = new Font(label_settings_menu_support_groups.Font, FontStyle.Italic);
            }
        }

        // button wyloguj z konta insta
        private async void button20_Click(object sender, EventArgs e)
        {
            if(!IGProc.IsUserAuthenticated())
            {
                SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith.InsertLoginData);
                return;
            }
            else
            {
                string login = IGProc.login;
                string password = IGProc.password;

                if (await IGProc.Logout())
                {
                    // usun wpis dot uzytkownika z bazy danych
                    using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                    {
                        m_dbConnection.Open();
                        string sql = $"DELETE FROM instagram_data WHERE login = '{login}' AND password = '{password}'";
                        SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                        command.ExecuteNonQuery(); // nic nie zwraca
                        m_dbConnection.Close();
                    }
                    SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith.InsertLoginData);
                    data_holder.instagram_ok = false;
                    return;
                }
                else
                {
                    MessageBox.Show("Błąd podczas wylogowania! Spróbuj się zalogować...");
                    SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith.InsertLoginData);
                    return;
                }
            }
        }

        // button logowanie do insta
        private async void button_insta_zaloguj_Click(object sender, EventArgs e)
        {
            if ((textBox_insta_login.Text.Length > 0) && (textBox_insta_password.Text.Length > 0))
            {
                // informacja o logowaniu do instagrama
                this.toolStripStatusLabel1.Text = $"Witaj {textBox_insta_login.Text}! Trwa logowanie do Instagrama...";

                //MessageBox.Show($"login: {textBox2.Text}\npassword: {textBox3.Text}");
                IResult<InstaSharper.Classes.InstaLoginResult> result;
                try
                {
                    result = await IGProc.Login(textBox_insta_login.Text, textBox_insta_password.Text);
                    //MessageBox.Show("Odpowiedz serwera: \n" + result.Info.ResponseRaw);

                    if (result.Value == InstaSharper.Classes.InstaLoginResult.BadPassword || result.Value == InstaSharper.Classes.InstaLoginResult.InvalidUser || result.Value == InstaSharper.Classes.InstaLoginResult.Exception)
                    {
                        if (result.Value == InstaSharper.Classes.InstaLoginResult.BadPassword)
                        {
                            MessageBox.Show("Złe hasło!");
                        }
                        else if (result.Value == InstaSharper.Classes.InstaLoginResult.InvalidUser)
                        {
                            MessageBox.Show("Zły user!");
                        }
                        else if (result.Value == InstaSharper.Classes.InstaLoginResult.Exception)
                        {
                            MessageBox.Show("Exception...!");
                        }
                        data_holder.InstaLoginStatus = InitDataHolder.ShowInstagramPanelWith.LoginFailed;
                        data_holder.instagram_ok = false;
                    }
                    else if (result.Value == InstaSharper.Classes.InstaLoginResult.ChallengeRequired)
                    {
                        // zapamietaj dane logowania
                        IGProc.login = textBox_insta_login.Text;
                        IGProc.password = textBox_insta_password.Text;

                        // zapisz status
                        data_holder.InstaLoginStatus = InitDataHolder.ShowInstagramPanelWith.InsertSecurityCode;
                        data_holder.instagram_ok = false;
                    }
                    else if (result.Value == InstaSharper.Classes.InstaLoginResult.TwoFactorRequired)
                    {
                        // zapamietaj dane logowania
                        IGProc.login = textBox_insta_login.Text;
                        IGProc.password = textBox_insta_password.Text;

                        // zapisz status
                        data_holder.InstaLoginStatus = InitDataHolder.ShowInstagramPanelWith.LoginFailed;
                        data_holder.instagram_ok = false;
                    }
                    else
                    {
                        //MessageBox.Show("Logowanie zakończone pomyślnie");
                        data_holder.InstaLoginStatus = InitDataHolder.ShowInstagramPanelWith.LoginSuccess;
                        data_holder.instagram_ok = true;

                        // zapamietaj dane logowania
                        IGProc.login = textBox_insta_login.Text;
                        IGProc.password = textBox_insta_password.Text;

                        // dodaj do bazy danych
                        using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                        {
                            m_dbConnection.Open();
                            string sql = $"INSERT INTO instagram_data (login, password) VALUES ('{IGProc.login}', '{IGProc.password}')";
                            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                            command.ExecuteNonQuery(); // nic nie zwraca
                            m_dbConnection.Close();
                        }

                        // uaktualnij wyglad menu
                        HideAllSettingsPanels();
                        panel_choose_starting_date.Show();
                        label_settings_menu_choose_date.BackColor = System.Drawing.Color.DarkSalmon;
                        label_settings_menu_choose_date.Font = new Font(label_settings_menu_choose_date.Font, FontStyle.Italic);
                    }
                    SetUpInstagramPanel(data_holder.InstaLoginStatus);
                    return;
                }
                catch (Exception ex)
                {
                    data_holder.InstaLoginStatus = InitDataHolder.ShowInstagramPanelWith.LoginFailed;
                    data_holder.instagram_ok = false;
                    MessageBox.Show("Błąd!\n" + ex.Message.ToString());
                }

                SetUpInstagramPanel(data_holder.InstaLoginStatus);
            }
            else
            {
                MessageBox.Show("Pola LOGIN i HASŁO nie mogą pozostać puste...\n\nWpisz swoje login i hasło i spróbuj ponownie");
            }
        }

        // po wcisnieciu entera
        private async void textBox7_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (textBox_challenge_code.Text.Length > 4)
                {
                    MessageBox.Show("Wysyłam kod!");
                    try
                    {
                        MessageBox.Show(textBox_challenge_code.Text);
                        InstaResetChallenge res3 = await IGProc.SendVerifyCode(textBox_challenge_code.Text);

                        MessageBox.Show(res3.LoggedInUser + "\n" + res3.Status + "\n" + res3.Action + "\n" + res3.StepName);
                        if (res3.UserId > 0)
                        {
                            SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith.LoginSuccess);
                        }
                        else
                        {
                            SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith.LoginFailed);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message.ToString());
                    }
                }
            }
        }

        // wyszarza pola pod radiobuttonem "Po ostatnio zrobionym poscie"
        private void HideFields1()
        {
            radioButton1.Checked = false;
            label42.Enabled = false;
            label61.Enabled = false;
        }

        // wyszarza pola pod radiobuttonem "Po dacie z kalendarza"
        private void HideFields2()
        {
            radioButton2.Checked = false;
            label64.Enabled = false;
            label65.Enabled = false;
            label66.Enabled = false;
        }

        // wyszarza pola pod radiobuttonem "Od x godzin"
        private void HideFields3()
        {
            radioButton3.Checked = false;
            textBox1.Enabled = false;
            label67.Enabled = false;
        }

        // odblokowuje pola pod radiobuttonem "Po ostatnio zrobionym poscie"
        private void ShowFields1()
        {
            label42.Enabled = true;
            label61.Enabled = true;
        }

        // odblokowuje pola pod radiobuttonem "Po dacie z kalendarza"
        private void ShowFields2()
        {
            label64.Enabled = true;
            label65.Enabled = true;
            label66.Enabled = true;
        }

        // odblokowuje pola pod radiobuttonem "Od x godzin"
        private void ShowFields3()
        {
            textBox1.Enabled = true;
            label67.Enabled = true;
        }

        // zaznaczono "po ostatnio skomentowanym poście"
        private void radioButton1_Click(object sender, EventArgs e)
        {
            // odznacz pozostałe radiobutton
            if (radioButton1.Checked)
            {
                ShowFields1();
                HideFields2();
                HideFields3();
            }

            // zmien metode pobierania daty w ustawieniach grupy
            int selected_group_index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());
            all_support_groups[selected_group_index].Settings.StartingDateMethod = StartingDateMethod.LastPost;
            all_support_groups[selected_group_index].Settings.SaveGroupSettingsToDB();
        }

        // zaznaczono "po dacie wpisanej w kalendarzyku"
        private void radioButton2_Click(object sender, EventArgs e)
        {
            // odznacz pozostałe radiobutton
            if (radioButton2.Checked)
            {
                HideFields1();
                ShowFields2();
                HideFields3();
            }

            // zmien metode pobierania daty w ustawieniach grupy
            int selected_group_index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());
            all_support_groups[selected_group_index].Settings.StartingDateMethod = StartingDateMethod.ChosenFromCalendar;
            all_support_groups[selected_group_index].Settings.SaveGroupSettingsToDB();
        }

        // zaznaczono "dodane w ciągu ostatnich x godzin"
        private void radioButton3_Click(object sender, EventArgs e)
        {
            // odznacz pozostałe radiobutton
            if (radioButton3.Checked)
            {
                HideFields1();
                HideFields2();
                ShowFields3();
            }

            // zmien metode pobierania daty w ustawieniach grupy
            int selected_group_index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());
            all_support_groups[selected_group_index].Settings.StartingDateMethod = StartingDateMethod.LastXHours;
            all_support_groups[selected_group_index].Settings.SaveGroupSettingsToDB();
        }

        // wybrano grupe wsparcia na liscie w panelu z ustawieniami grup
        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = 0;
            
            if (listBox2.SelectedIndex > -1)
            {
                index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());
                SetUpSupportGroupsSettingsPanel(index);

                if(index >= 0)
                {
                    if(all_support_groups[index].Settings.OnlyLike)
                    {
                        radioButton1.Text = "Dodane po ostatnio polajkowanym poście";
                    }
                    else
                    {
                        radioButton1.Text = "Dodane po ostatnio skomentowanym poście";
                    }
                }
            }
            else
            {
                SetUpSupportGroupsSettingsPanel(-1);
            }
        }

        // po wybraniu grupy wsparcia, wypelnia panel ustawien tej grupy odpowiednimi danymi
        void SetUpSupportGroupsSettingsPanel(int support_group_index)
        {
            if(support_group_index >= 0)
            {
                checkBox1.Enabled = true;
                label41.Enabled = true;
                label42.Enabled = true;
                label61.Enabled = true;
                label64.Enabled = true;
                label65.Enabled = true;
                label66.Enabled = true;
                label67.Enabled = true;
                textBox1.Enabled = true;
                radioButton1.Enabled = true;
                radioButton2.Enabled = true;
                radioButton3.Enabled = true;
                checkBox_skip_this_group_once.Enabled = true;

                // zaznaczenie checkboxa czy grupa typu tylko like
                checkBox1.Checked = all_support_groups[support_group_index].Settings.OnlyLike;
                // ustawienie radioButtonow
                switch(all_support_groups[support_group_index].Settings.StartingDateMethod)
                {
                    case StartingDateMethod.LastPost:
                        radioButton1.Checked = true;
                        radioButton2.Checked = false;
                        radioButton3.Checked = false;
                        ShowFields1();
                        HideFields2();
                        HideFields3();
                        break;

                    case StartingDateMethod.ChosenFromCalendar:
                        radioButton1.Checked = false;
                        radioButton2.Checked = true;
                        radioButton3.Checked = false;
                        HideFields1();
                        ShowFields2();
                        HideFields3();
                        break;

                    case StartingDateMethod.LastXHours:
                        radioButton1.Checked = false;
                        radioButton2.Checked = false;
                        radioButton3.Checked = true;
                        HideFields1();
                        HideFields2();
                        ShowFields3();
                        break;

                    default:
                        // nothing
                        radioButton1.Checked = false;
                        radioButton2.Checked = true;
                        radioButton3.Checked = false;
                        HideFields1();
                        ShowFields2();
                        HideFields3();
                        break;
                }
                // jesli wybrano ilosc godzin wstecz - pokazuje ile godzin wybrano
                textBox1.Text = all_support_groups[support_group_index].Settings.LastHours.ToString();

                // wpisanie dat do labeli
                // label61 - data -> ostatnio skomentowany post
                label61.Text = GetDateLastCommentedPost();

                // wpisanie daty z kalendarzyka do labela
                // label64 - data -> data wybrana w kalendarzyku
                label64.Text = GetDateFromCalendar();

                // za/odznaczenie checkboxa - pomin tym razem
                checkBox_skip_this_group_once.Checked = all_support_groups[support_group_index].Settings.SkipThisTime;
            }
            else
            {
                //MessageBox.Show("Wystąpił błąd - wybrana grupa wsparcia nie istnieje!");
                checkBox1.Enabled = false;
                label41.Enabled = false;
                label42.Enabled = false;
                label61.Enabled = false;
                label64.Enabled = false;
                label65.Enabled = false;
                label66.Enabled = false;
                label67.Enabled = false;
                textBox1.Enabled = false;
                radioButton1.Enabled = false;
                radioButton2.Enabled = false;
                radioButton3.Enabled = false;
                checkBox_skip_this_group_once.Enabled = false;
            }
        }

        private string GetDateLastCommentedPost()
        {
            int selected_group_index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());
            long timestamp = all_support_groups[selected_group_index].Settings.LastCommentedPostTimestamp;
            string my_format = "";

            // jesli istnieje wartosc
            if (timestamp > 0)
            {
                radioButton1.Enabled = true;
                label42.Enabled = true;
                label61.Enabled = true;

                DateTime dt = UnixTimeStampToDateTime(timestamp);
                string month = "";
                switch (dt.Month)
                {
                    case 1:
                        month = "stycznia";
                        break;
                    case 2:
                        month = "lutego";
                        break;
                    case 3:
                        month = "marca";
                        break;
                    case 4:
                        month = "kwietnia";
                        break;
                    case 5:
                        month = "maja";
                        break;
                    case 6:
                        month = "czerwca";
                        break;
                    case 7:
                        month = "lipca";
                        break;
                    case 8:
                        month = "sierpnia";
                        break;
                    case 9:
                        month = "września";
                        break;
                    case 10:
                        month = "października";
                        break;
                    case 11:
                        month = "listopada";
                        break;
                    case 12:
                        month = "grudnia";
                        break;
                }

                my_format = String.Concat(dt.Hour.ToString(), ":", dt.Minute.ToString("00.##"), ", ", dt.Day.ToString(), " ", month, " ", dt.Year.ToString());
            }
            else // jesli nie istnieje
            {
                my_format = "jeszcze nigdy";
                radioButton1.Enabled = false;
                label42.Enabled = false;
                label61.Enabled = false;
                if(radioButton1.Checked)
                {
                    radioButton1.Checked = false;
                    radioButton2.Checked = true;
                    all_support_groups[selected_group_index].Settings.StartingDateMethod = StartingDateMethod.ChosenFromCalendar;
                }
            }
            
            return my_format;
        }

        private string GetDateFromCalendar()
        {
            long timestamp = ToUnixTimestamp(dateTimePicker1.Value);
            string my_format = "";

            // jesli istnieje wartosc
            /// !!! dodać sprawdzanie czy data jest bardzo bliska aktualnej godzinie i wyswietlic powiadomienie
            if (timestamp > 0)
            {
                DateTime dt = UnixTimeStampToDateTime(timestamp);
                string month = "";
                switch (dt.Month)
                {
                    case 1:
                        month = "stycznia";
                        break;
                    case 2:
                        month = "lutego";
                        break;
                    case 3:
                        month = "marca";
                        break;
                    case 4:
                        month = "kwietnia";
                        break;
                    case 5:
                        month = "maja";
                        break;
                    case 6:
                        month = "czerwca";
                        break;
                    case 7:
                        month = "lipca";
                        break;
                    case 8:
                        month = "sierpnia";
                        break;
                    case 9:
                        month = "września";
                        break;
                    case 10:
                        month = "października";
                        break;
                    case 11:
                        month = "listopada";
                        break;
                    case 12:
                        month = "grudnia";
                        break;
                }

                my_format = String.Concat(dt.Hour.ToString(), ":", dt.Minute.ToString("00.##"), ", ", dt.Day.ToString(), " ", month, " ", dt.Year.ToString());
            }
            else // jesli nie istnieje
            {
                my_format = "jeszcze nigdy";
            }

            return my_format;
        }

        // zmieniono liczbę godzin do przeszukania wiadomości (o ile godzin od teraz się cofnąć)
        private void textBox1_ValueChanged(object sender, EventArgs e)
        {
            if(textBox1.Focused)
            {
                int selected_group_index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());

                int tmp = 0;
                if (Int32.TryParse(textBox1.Text, out tmp) || textBox1.Text.Length == 0)
                {
                    all_support_groups[selected_group_index].Settings.LastHours = tmp;
                }
                else
                {
                    MessageBox.Show("W to pole należy wpisać liczbę");
                    return;
                }

                Double hours_value = Decimal.ToDouble(tmp);
                all_support_groups[selected_group_index].Settings.LastHours = (int)hours_value;
                all_support_groups[selected_group_index].Settings.LastHoursTimestamp = ToUnixTimestamp(DateTime.Now.AddHours(hours_value)); // to raczej niepotrzebne
                all_support_groups[selected_group_index].Settings.SaveGroupSettingsToDB();
            }
            //MessageBox.Show("Teraz: " + ToUnixTimestamp(DateTime.Now) + "\nUstawiona: " + ToUnixTimestamp(DateTime.Now.AddHours(hours_value)));
        }

        // zapisuje datę z kalendarzyka tylko do grup wsparcia, ktore maja wybrana te opcje
        private bool AssignCalendarDateToSupportGroups(DateTime calendar_date)
        {
            try
            {
                foreach (SupportGroup group in all_support_groups)
                {
                    if (group.Settings.StartingDateMethod == StartingDateMethod.ChosenFromCalendar)
                    {
                        group.Settings.CalendarDateTimestamp = ToUnixTimestamp(calendar_date);
                        group.Settings.StartingDateTimestamp = ToUnixTimestamp(calendar_date);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("\n" + ex.Message);
                return false;
            }
            return true;
        }

        // zmienila sie data w kalendarzyku
        private void dateTimePicker1_ValueChanged_1(object sender, EventArgs e)
        {
            foreach(SupportGroup group in all_support_groups)
            {
                group.Settings.CalendarDateTimestamp = ToUnixTimestamp(dateTimePicker1.Value);
            }
        }

        // kliknieto zapisz zmiany
        private void button_settings_save_Click(object sender, EventArgs e)
        {
            Int32.TryParse(textBox_likes_limit.Text, out data_holder.likes_limit);
            Int32.TryParse(textBox_min_time_betw_likes.Text, out data_holder.min_time_betw_likes);
            Int32.TryParse(textBox_max_time_betw_likes.Text, out data_holder.max_time_betw_likes);
            Int32.TryParse(textBox_keep_old_entries.Text, out data_holder.keep_old_entries);

            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE settings SET min_time_betw_likes = '{data_holder.min_time_betw_likes}', max_time_betw_likes = '{data_holder.max_time_betw_likes}', likes_limit = '{data_holder.likes_limit}', keep_old_entries = {data_holder.keep_old_entries}";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close();
            }
        }

        // zaznaczono, zeby te grupe pominac
        private void checkBox_skip_this_group_once_Click(object sender, EventArgs e)
        {
            // zmien ustawienie w grupie wsparcia
            if (listBox2.SelectedIndex != -1)
            {
                int selected_group_index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());
                all_support_groups[selected_group_index].Settings.SkipThisTime = checkBox_skip_this_group_once.Checked;
            }
        }

        // zaznaczono, ze grupa typu tylko like
        private void checkBox1_Click(object sender, EventArgs e)
        {
            if (listBox2.SelectedIndex != -1)
            {
                int index = all_support_groups.FindIndex(x => x.Name == listBox2.SelectedItem.ToString());
                all_support_groups[index].Settings.OnlyLike = checkBox1.Checked;
                all_support_groups[index].Settings.SaveGroupSettingsToDB();
                this.listBox2.Refresh();
            }
        }

        // zapisz domyslny komentarz
        private void button3_Click(object sender, EventArgs e)
        {
            // sprawdzenie czy pole nie jest puste
            if (richTextBox1.Text.Length != 0)
            {
                // dodaj do bazy danych
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"INSERT INTO default_comments (id_comment, comment, used_automatically) VALUES (NULL, '{richTextBox1.Text}', 0)";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                    m_dbConnection.Close();
                }
                // dodaj do listy
                listView1.Items.Add(richTextBox1.Text);
                // usun tekst z richtextboxa
                richTextBox1.Text = "";
            }
            else
            {
                MessageBox.Show("Nie można dodać pustego komentarza! Wpisz treść komentarza w polu powyżej.");
            }
        }

        // usun z domyslnych komentarzy
        private void button4_Click(object sender, EventArgs e)
        {
            // pobierz tresc komentarza
            string comment = listView1.SelectedItems[0].Text;
            // usun z bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"DELETE FROM default_comments WHERE comment = '{comment}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close();
            }
            // usun z listy domyslnych komentarzy
            listView1.Items.Remove(listView1.FindItemWithText(comment));
        }

        private void AddSupportGroupLabel(SupportGroup support_group)
        {
            int group_nr = (panel_liker_panel_main.Controls.OfType<Label>().Count() / 3); // rozp od 0
            string support_group_name = support_group.Name;

            Label support_group_label = new Label();
            //support_group_label.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            support_group_label.Font = new System.Drawing.Font("Segoe UI", 12F);
            support_group_label.Location = new System.Drawing.Point(5, 5 + (group_nr * 40));
            support_group_label.Name = "label_support_group_name_" + group_nr.ToString();
            support_group_label.Size = new System.Drawing.Size(300, 40);
            support_group_label.TabIndex = 0;
            support_group_label.Text = support_group_name;
            support_group_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            if (support_group.Settings.OnlyLike && !support_group.Settings.SkipThisTime)
            {
                support_group_label.ForeColor = Color.Black; 
            }
            else
            {
                support_group_label.ForeColor = Color.Gray;
            }

            Label support_group_messages_count = new Label();
            //support_group_messages_count.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            support_group_messages_count.Font = new System.Drawing.Font("Segoe UI", 12F);
            support_group_messages_count.Location = new System.Drawing.Point(304, 5 + (group_nr * 40));
            support_group_messages_count.Name = "label_support_group_messages_count_" + group_nr.ToString();
            support_group_messages_count.Size = new System.Drawing.Size(136, 40);
            support_group_messages_count.TabIndex = 1;
            support_group_messages_count.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            if (support_group.Settings.OnlyLike && !support_group.Settings.SkipThisTime)
            {
                support_group_messages_count.ForeColor = Color.Black;
                support_group_messages_count.Text = "Liczę...";
            }
            else
            {
                support_group_messages_count.ForeColor = Color.Gray;
                support_group_messages_count.Text = "N/D";
            }

            Label support_group_likes_count = new Label();
            //support_group_likes_count.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            support_group_likes_count.Font = new System.Drawing.Font("Segoe UI", 12F);
            support_group_likes_count.Location = new System.Drawing.Point(439, 5 + (group_nr * 40));
            support_group_likes_count.Name = "label_support_group_likes_count_" + group_nr.ToString();
            support_group_likes_count.Size = new System.Drawing.Size(150, 40);
            support_group_likes_count.TabIndex = 2;
            support_group_likes_count.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            if (support_group.Settings.OnlyLike && !support_group.Settings.SkipThisTime)
            {
                support_group_likes_count.ForeColor = Color.Black;
                support_group_likes_count.Text = "Czekam...";
            }
            else
            {
                support_group_likes_count.ForeColor = Color.Gray;
                support_group_likes_count.Text = "N/D";
            }

            panel_liker_panel_main.Controls.Add(support_group_label);
            panel_liker_panel_main.Controls.Add(support_group_messages_count);
            panel_liker_panel_main.Controls.Add(support_group_likes_count);

            this.Refresh();
        }

        private void UpdateSupportGroupLabel(SupportGroup support_group, int messages_count = -1, int done_likes = -1)
        {
            // sprawdza numer labela, dot grupy
            int label_nr = ExtractIntFromString(panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Text == support_group.Name).SingleOrDefault().Name);

            // jesli podano liczbe wiadomosci, aktualizuje ja
            if (messages_count > -1)
            {
                panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_messages_count_" + label_nr.ToString()).FirstOrDefault().Text = messages_count.ToString();
            }

            // jesli podano liczbe likow, aktualizuje ja
            if(done_likes > -1)
            {
                panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_likes_count_" + label_nr.ToString()).FirstOrDefault().Text = done_likes.ToString();
            }

            // jesli oba parametry nie zostaly ustawione, wpisuje N/D do labela
            if(((messages_count == -1) && (done_likes == -1)) || !support_group.Settings.OnlyLike)
            {
                panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_messages_count_" + label_nr.ToString()).FirstOrDefault().Text = "N/D";
                panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_likes_count_" + label_nr.ToString()).FirstOrDefault().Text = "N/D";
            }
        }

        private void ActivateSupportGroupLabel(SupportGroup support_group)
        {
            int label_nr = ExtractIntFromString(panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Text == support_group.Name).FirstOrDefault().Name);
            panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_name_" + label_nr.ToString()).FirstOrDefault().Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_messages_count_" + label_nr.ToString()).FirstOrDefault().Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_likes_count_" + label_nr.ToString()).FirstOrDefault().Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            panel_liker_panel_main.Refresh();
        }

        private void DeactivateSupportGroupLabel(SupportGroup support_group)
        {
            int label_nr = ExtractIntFromString(panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Text == support_group.Name).SingleOrDefault().Name);
            panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_name_" + label_nr.ToString()).FirstOrDefault().Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_messages_count_" + label_nr.ToString()).FirstOrDefault().Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            panel_liker_panel_main.Controls.OfType<Label>().Where(x => x.Name == "label_support_group_likes_count_" + label_nr.ToString()).FirstOrDefault().Font = new Font("Segoe UI", 12F, FontStyle.Regular);
            panel_liker_panel_main.Refresh();
        }

        private void listBox2_DrawItem(object sender, DrawItemEventArgs e)
        {
            /*
            private SolidBrush backgroundBrushSelected = new SolidBrush(Color.FromKnownColor(KnownColor.Highlight));
            private SolidBrush backgroundBrushOnlyLike = new SolidBrush(Color.Aqua);
            private SolidBrush backgroundBrush = new SolidBrush(Color.White);
             */
            e.DrawBackground();
            bool selected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);

            int index = e.Index;
            if (index >= 0 && index < listBox2.Items.Count)
            {
                string text = listBox2.Items[index].ToString();
                Graphics g = e.Graphics;

                //background:
                SolidBrush _backgroundBrush;
                if (selected)
                    _backgroundBrush = backgroundBrushSelected;
                else if (all_support_groups.Where(x => x.Name == listBox2.Items[index].ToString()).FirstOrDefault().Settings.OnlyLike)
                    _backgroundBrush = backgroundBrushOnlyLike;
                else
                    _backgroundBrush = backgroundBrush;
                e.Bounds.Inflate(new Size(0, 5));
                g.FillRectangle(_backgroundBrush, e.Bounds);

                //text:
                Color color = (selected) ? Color.White : Color.Black;
                SizeF size = e.Graphics.MeasureString(listBox2.Items[index].ToString(), e.Font);
                TextRenderer.DrawText(g, text, e.Font, new Point((int)(e.Bounds.Left + (e.Bounds.Width / 2 - size.Width / 2)), (int)(e.Bounds.Top + (e.Bounds.Height / 2 - size.Height / 2))), Color.Black);
            }

            e.DrawFocusRectangle();
        }

        private void listBox2_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 26;
        }

        // wyciaga int ze stringa (np label_10 -> 10)
        private int ExtractIntFromString(string str)
        {
            int i = 0; // return value
            string new_str = "";
            foreach (Char ch in str)
            {
                if ((ch >= '0') && (ch <= '9'))
                {
                    new_str += ch;
                }
            }
            Int32.TryParse(new_str, out i);
            return i;
        }

        // Ustawienia Telegrama - button wyslij kod na nr telefonu (po wpisaniu numeru)
        private async void button11_Click(object sender, EventArgs e)
        {
            // zapisz nr telefonu w bazie danych
            if (textBox8.Text.Length == 9)
            {
                data_holder.phone_number = "48";
                data_holder.phone_number = data_holder.phone_number.Insert(2, textBox8.Text);
            }
            else if (textBox8.Text.Length == 11)
            {
                data_holder.phone_number = textBox8.Text;
            }
            else
            {
                MessageBox.Show("Nr telefonu powinien miec długość 11 znaków i zaczynać się od 48");
                return;
            }

            // jesli nr telefonu jest poprawny to go pokaz
            //MessageBox.Show("Nr telefonu: " + this.phone_number);

            // wyslij kod telegrama
            data_holder.hash = await data_holder.client.SendCodeRequestAsync(data_holder.phone_number);

            // informacja o oczekiwaniu na kod z Telegrama
            this.toolStripStatusLabel1.Text = $"Na konto Telegram przypisane do numeru {data_holder.phone_number} został wysłany kod potwierdzający - wpisz go w polu powyżej";

            // zmien panel Telegram - pokaz pole do wpisania kodu
            SetUpTelegramPanel(InitDataHolder.ShowTelegramPanelWith.InsertSecurityCode);
        }

        // Ustawienia Telegrama - button zaloguj (po wpisaniu otrzymanego kodu bezpieczenstwa)
        private async void button17_Click_1(object sender, EventArgs e)
        {
            //textbox9 - stad wziac kod
            // informuje uzytkownika co sie dzieje
            this.toolStripStatusLabel1.Text = "Trwa potwierdzanie otrzymanego kodu...";
            // jesli nie wpisano kodu, nic nie rob
            if (textBox9.Text.Length == 0)
                return;

            // jesli kod poprawny (?) to zapisz go
            data_holder.code = textBox9.Text;

            // dodaj do bazy danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE telegram_data SET phone_number = '{data_holder.phone_number}' WHERE phone_number = ''";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close();
            }

            try
            {
                data_holder.user = await data_holder.client.MakeAuthAsync(data_holder.phone_number, data_holder.hash, data_holder.code);
            }
            catch (Exception ex)
            {
                SetUpTelegramPanel(InitDataHolder.ShowTelegramPanelWith.LoginFailed);
                MessageBox.Show("Wystąpił bład!\n" + ex.Message.ToString());
                return;
            }
            
            // wyswietl komunikat, gdy logowanie przebieglo pomyslnie
            if (data_holder.user.Id > 0)
            {
                // informacja o pomyslnym zalogowaniu do telegrama
                this.toolStripStatusLabel1.Text = "Pomyslnie zalogowano do telegrama!";
                SetUpTelegramPanel(InitDataHolder.ShowTelegramPanelWith.LoginSuccess);
                data_holder.telegram_ok = true;

                // pobierz chaty i channele
                await data_holder.GetAllTelegramChannelsAndChats();

                // pokazuje grupy wsparcia
                InitSupportGroupsPanel(data_holder.channels, data_holder.chats, data_holder.initial_support_groups);
                // sprawdza czy nadal do wszystkich nalzey
                CheckIfSupportGroupsExist();
            }
            else
            {
                SetUpTelegramPanel(InitDataHolder.ShowTelegramPanelWith.LoginFailed);
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if(radioButton2.Checked)
            {
                label64.Enabled = true;
                label65.Enabled = true;
                label66.Enabled = true;
            }
        }

        /// <summary>
        /// Przechodzi z likera do commentera
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void panel_liker_button_go_on_Click(object sender, EventArgs e)
        {
            // Inicjalizacja panela komentowania zdjęć
            InitLikeCommenterPanel();

            // ukrycie panela Liker
            panel_liker.Hide();

            // pokazanie panela commenter
            panel_commenter.Show();
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            /*
            private SolidBrush backgroundBrushSelected = new SolidBrush(Color.FromKnownColor(KnownColor.Highlight));
            private SolidBrush backgroundBrushOnlyLike = new SolidBrush(Color.Aqua);
            private SolidBrush backgroundBrush = new SolidBrush(Color.White);
             */
            e.DrawBackground();
            bool selected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);

            int index = e.Index;
            if (index >= 0 && index < listBox2.Items.Count)
            {
                string text = listBox2.Items[index].ToString();
                Graphics g = e.Graphics;

                //background:
                SolidBrush _backgroundBrush;
                if (selected)
                    _backgroundBrush = backgroundBrushSelected;
                else if (all_support_groups.Where(x => x.Name == listBox2.Items[index].ToString()).FirstOrDefault().Settings.OnlyLike)
                    _backgroundBrush = backgroundBrushOnlyLike;
                else
                    _backgroundBrush = backgroundBrush;
                e.Bounds.Inflate(new Size(0, 5));
                g.FillRectangle(_backgroundBrush, e.Bounds);

                //text:
                Color color = (selected) ? Color.White : Color.Black;
                SizeF size = e.Graphics.MeasureString(listBox2.Items[index].ToString(), e.Font);
                TextRenderer.DrawText(g, text, e.Font, new Point((int)(e.Bounds.Left + (e.Bounds.Width / 2 - size.Width / 2)), (int)(e.Bounds.Top + (e.Bounds.Height / 2 - size.Height / 2))), Color.Black);
            }

            e.DrawFocusRectangle();
        }

        private void listBox1_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 26;
        }

        private void follower_panel_Click(object sender, EventArgs e)
        {
            Panel panel = (Panel)sender;
            Label lbl = panel.Controls.OfType<Label>().FirstOrDefault();

            if (lbl != null)
            {
                if (insta_bad_followers.Where(x => x.user_name == lbl.Text).FirstOrDefault() != null)
                {
                    switch (insta_bad_followers.Where(x => x.user_name == lbl.Text).FirstOrDefault().follower_state)
                    {
                        case FollowerState.KeepFollowing:
                            insta_bad_followers.Where(x => x.user_name == lbl.Text).FirstOrDefault().follower_state = FollowerState.Unfollow;
                            break;

                        case FollowerState.Unfollow:
                            insta_bad_followers.Where(x => x.user_name == lbl.Text).FirstOrDefault().follower_state = FollowerState.Admin;
                            break;

                        case FollowerState.Admin:
                            insta_bad_followers.Where(x => x.user_name == lbl.Text).FirstOrDefault().follower_state = FollowerState.KeepFollowing;
                            break;
                    }

                    panel.Refresh();
                }
            }
        }

        private void follower_panel_Paint(object sender, PaintEventArgs e)
        {
            Panel pan = (Panel)sender;
            Label lbl = pan.Controls.OfType<Label>().FirstOrDefault();
            Color color = Color.Black;

            if(insta_bad_followers.Where(x => x.user_name == lbl.Text).FirstOrDefault() != null)
            {
                switch(insta_bad_followers.Where(x => x.user_name == lbl.Text).FirstOrDefault().follower_state)
                {
                    case FollowerState.KeepFollowing:
                        color = Color.Black;
                        break;

                    case FollowerState.Unfollow:
                        color = Color.Red;
                        break;

                    case FollowerState.Admin:
                        color = Color.Yellow;
                        break;

                    default:
                        color = Color.White;
                        break;
                }
            }

            if (pan.BorderStyle == BorderStyle.FixedSingle)
            {
                int thickness = 4; //it's up to you
                int halfThickness = thickness / 2;
                using (Pen p = new Pen(color, thickness))
                {
                    e.Graphics.DrawRectangle(p, new Rectangle(halfThickness,
                                                              halfThickness,
                                                              pan.ClientSize.Width - thickness,
                                                              pan.ClientSize.Height - thickness));
                }

                typeof(Control).GetProperty("ResizeRedraw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
               .SetValue(pan, true, null);

                typeof(Panel).GetProperty("DoubleBuffered",
                              System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                 .SetValue(pan, true, null);
            }

            sender = (object)pan;
        }

        private void panel_left_arrow_Click(object sender, EventArgs e)
        {
            // przewin do poprzedniej strony
            HideAllTiles();
            SetBadFollowersPanelNumbers(--this.followers_page, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList()); // page = 1
            CreateBadFollowerPanel(this.followers_page, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList());

            if (this.followers_page == 1)
            {
                panel_left_arrow.Hide();
                panel_right_arrow.Show();
            }
        }

        private void panel_right_arrow_Click(object sender, EventArgs e)
        {
            // przewin do nastepnej strony
            HideAllTiles();
            SetBadFollowersPanelNumbers(++this.followers_page, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList()); // page = 1
            CreateBadFollowerPanel(this.followers_page, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList());

            panel_left_arrow.Show();
        }

        private async void panel_swords_Click(object sender, EventArgs e)
        {
            // pokaz jak samuraj wycina bad-followersow
            pictureBox_swords_cutter.Show();

            foreach(var follower in insta_bad_followers)
            {
                // zmien w bazie danych
                using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                {
                    m_dbConnection.Open();
                    string sql = $"UPDATE instagram_followers SET status = '{(Int32)follower.follower_state}' WHERE user_name = '{follower.user_name}'";
                    SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                    m_dbConnection.Close();
                }

                // unfollow
                if (follower.follower_state == FollowerState.Unfollow)
                {
                    await IGProc.Unfollow(follower.user_id);
                }
            }

            // wyswietl liste ponownie
            HideAllTiles();
            ResetAllBadFollowersPanels(insta_bad_followers);
            SetBadFollowersPanelNumbers(this.followers_page, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList()); // page = 1
            CreateBadFollowerPanel(this.followers_page, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList());

            // schowaj samuraja
            pictureBox_swords_cutter.Hide();
        }

        private async void label_followers_manager_Click(object sender, EventArgs e)
        {
            if (data_holder.instagram_ok)
            {
                // pokazuje panel zarzadzania followersami
                panel_followers_manager.Show();
                panel_settings.Hide();

                if (!followers_manager_initialized)
                {
                    // pokazuje ile osob cie obserwuje
                    pictureBox_followers_wait.Show();
                    int followers = await IGProc.GetFollowersCount();
                    pictureBox_followers_wait.Hide();
                    label_followers_count.Text = followers.ToString();

                    // pokazuje ile osob obserwyjesz
                    pictureBox_following_wait.Show();
                    int following = await IGProc.GetFollowingCount();
                    pictureBox_following_wait.Hide();
                    label_following_count.Text = following.ToString();

                    // wyswietla panel z obserwowanymi osobami
                    pictureBox_bad_following_wait.Show();
                    InstaUserShortList bad_following = await IGProc.GetWhoDoesntFollowYou();
                    await GenerateBadFollowersList(bad_following);
                    SetBadFollowersPanelNumbers(1, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList()); // page = 1
                    CreateEmptyFollowersPanels();
                    CreateBadFollowerPanel(1, insta_bad_followers.Where(x => x.follower_state == FollowerState.KeepFollowing).ToList());
                    panel_left_arrow.Hide();
                    pictureBox_bad_following_wait.Hide();

                    label_bad_following_count.Text = bad_following.Count.ToString();

                    followers_manager_initialized = true;
                    panel_swords.Show();
                }
            }
            else
            {
                MessageBox.Show("Aby korzystać z Menedżera Followersów najpierw musisz się poprawnie zalogować do Instagrama");
            }
        }

        private void button_followers_back_to_menu_Click(object sender, EventArgs e)
        {
            panel_settings.Show();
            panel_followers_manager.Hide();
        }

        private async void button_2FactAuth_confirm_Click(object sender, EventArgs e)
        {
            int code = 0;
            if(textBox_2StepAuth_code.Text.Length == 6 && Int32.TryParse(textBox_2StepAuth_code.Text, out code))
            {
                IResult<InstaLoginTwoFactorResult> res;
                res = await IGProc.TwoFactorLogin(textBox_2StepAuth_code.Text);

                if(res.Succeeded)
                {
                    MessageBox.Show(res.Info.ResponseRaw);

                    if(res.Value == InstaLoginTwoFactorResult.Success && IGProc.IsUserAuthenticated())
                    {
                        MessageBox.Show("Pomyślnie zalogowano do instagrama!");
                    }
                    else
                    {
                        MessageBox.Show("Logowanie do instagrama nie powiodło się...");
                    }
                }
                else
                {
                    MessageBox.Show("Logowanie do instagrama niestety nie powiodło się...");
                }
            }
            else
            {
                MessageBox.Show("Kod autoryzacyjny musi mieć 6 znaków i składać się z samych cyfr. Wpisz go ponownie.");
            }
        }

        private async void textBox_2StepAuth_code_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            {
                int code = 0;
                if (textBox_2StepAuth_code.Text.Length == 6 && Int32.TryParse(textBox_2StepAuth_code.Text, out code))
                {
                    IResult<InstaLoginTwoFactorResult> res;
                    res = await IGProc.TwoFactorLogin(textBox_2StepAuth_code.Text);

                    if (res.Succeeded)
                    {
                        MessageBox.Show(res.Info.ResponseRaw);

                        if (res.Value == InstaLoginTwoFactorResult.Success && IGProc.IsUserAuthenticated())
                        {
                            MessageBox.Show("Pomyślnie zalogowano do instagrama!");
                        }
                        else
                        {
                            MessageBox.Show("Logowanie do instagrama nie powiodło się...");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Logowanie do instagrama niestety nie powiodło się...");
                    }
                }
                else
                {
                    MessageBox.Show("Kod autoryzacyjny musi mieć 6 znaków i składać się z samych cyfr. Wpisz go ponownie.");
                }
            }
        }

        private async void button_insta_challenge_confirm_Click(object sender, EventArgs e)
        {
            if (textBox_challenge_code.Text.Length > 4)
            {
                //MessageBox.Show("Wysyłam kod!");
                try
                {
                    //MessageBox.Show(textBox_challenge_code.Text);
                    InstaResetChallenge res3 = await IGProc.SendVerifyCode(textBox_challenge_code.Text);

                    //MessageBox.Show(res3.LoggedInUser + "\n" + res3.Status + "\n" + res3.Action + "\n" + res3.StepName);
                    if (res3.Status == "ok")
                    {
                        // dodaj do bazy danych
                        using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
                        {
                            m_dbConnection.Open();
                            string sql = $"DELETE FROM instagram_data";
                            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                            command.ExecuteNonQuery(); // nic nie zwraca

                            sql = $"INSERT INTO instagram_data (login, password) VALUES ('{IGProc.login}', '{IGProc.password}')";
                            command = new SQLiteCommand(sql, m_dbConnection);
                            command.ExecuteNonQuery(); // nic nie zwraca
                            m_dbConnection.Close();
                        }

                        // zapisz statusy
                        SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith.LoginSuccess);
                        data_holder.instagram_ok = true;
                    }
                    else
                    {
                        SetUpInstagramPanel(InitDataHolder.ShowInstagramPanelWith.LoginFailed);
                        data_holder.instagram_ok = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("BŁĄD!\n" + ex.Message.ToString());
                }
            }
        }
    }
}
