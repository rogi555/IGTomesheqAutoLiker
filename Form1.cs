using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SQLite;
using System.IO;
using TLSharp.Core;
using TeleSharp.TL.Messages;
using TeleSharp.TL;
using System.Threading.Tasks;
using IGTomesheq;

namespace IGTomesheqAutoLiker
{
    public partial class Form1 : Form
    {
        // baza danych
        private SQLiteConnection m_dbConnection;

        // Telegram
        const int API_ID = 546804;
        const string API_HASH = "da4ce7c1d67eb3a1b1e2274f32d08531";

        TelegramClient client;
        string code;
        string hash;
        TeleSharp.TL.TLUser user;
        FileSessionStore store;
        List<TLChat> chats;
        List<ChatsMessages> chats_messages;

        public Form1()
        {
            InitializeComponent();

            // baza danych
            try
            {
                m_dbConnection = new SQLiteConnection("Data Source=tomesheq_db.db;Version=3;");
                m_dbConnection.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                throw;
            }

            // Telegram
            code = "";
            hash = "";
            chats_messages = new List<ChatsMessages>();
        }

        /* --- SCREEN POWITALNY --- */
        private void button1_Click(object sender, EventArgs e)
        {
            // button dalej na screen1 (powitanie)
            this.panel_hello.Hide();
            this.panel_comments.Show();

            // zainicjuj formularz danymi
            init_comments_screen();
        }
        /* --- SCREEN POWITALNY --- */

        /* --- SCREEN Z DOMYŚLNYMI KOMENTARZAMI --- */
        private void init_comments_screen()
        {
            // odczytaj z bazy danych dostepne komentarze
            string sql = "SELECT * FROM default_comments";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                listView1.Items.Add(reader["comment"].ToString());
                //MessageBox.Show(reader["comment"].ToString());
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // button dalej na screenie z domyslnymi komentarzami
            this.panel_comments.Hide();
            this.panel_telegram_login.Show();

            // inicjalizacja okna telegrama
            init_telegram();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // button zapisywania komentarza

            // jesli pole jest puste, to nic nie dodawaj
            if (richTextBox1.Text.Length == 0)
                return;
            // dodaj komentarz do listy
            listView1.Items.Add(new ListViewItem(richTextBox1.Text.ToString()));
            // dodaj komentarz do bazy danych
            string sql = $"INSERT INTO default_comments (id_comment, comment, used_automatically) VALUES (NULL, '{richTextBox1.Text.ToString()}', 0)";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.ExecuteNonQuery(); // nic nie zwraca
            // wyczysc richtextbox
            richTextBox1.Text = "";
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // button do usuniecia niechcianego komentarza z listView1

            // jesli nic nie zaznaczono to nic nie rob
            if (listView1.SelectedIndices.Count == 0)
                return;
            // usun zaznaczony komentarz z bazy danych
            string sql = $"DELETE FROM default_comments WHERE comment='{listView1.SelectedItems[0].Text}'";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            command.ExecuteNonQuery(); // nic nie zwraca
            // usun zaznaczony komentarz z listy
            listView1.SelectedItems[0].Remove();
        }
        /* --- SCREEN Z DOMYŚLNYMI KOMENTARZAMI --- */

        /* --- SCREEN Z LOGOWANIEM DO TELEGRAMA --- */
        private async void init_telegram()
        {
            // inicjalizuje polaczenie telegramowe
            store = new FileSessionStore();
            client = new TelegramClient(API_ID, API_HASH, store);
            await client.ConnectAsync();
            if(client.IsUserAuthorized())
            {
                // ukryj pola do wpisania kodu
                textBox1.Hide();
                label15.Hide();
                button6.Hide();
                // pokaz labele z info, ze logowanie pomyslne
                label16.Show();
                label17.Show();

                // pobierz chaty
                GetAllTelegramChats();
            }
            else
            {
                hash = await client.SendCodeRequestAsync("48886509311");
            }
        }

        private async void GetAllTelegramChats()
        {
            // pobiera wszystkie chaty uzytkownika
            var dialogs = (TLDialogs)await client.GetUserDialogsAsync();
            chats = dialogs.Chats
                .OfType<TLChat>()
                .ToList();
        }

        private async void button6_Click(object sender, EventArgs e)
        {
            // button zapisz kod potwierdzajacy od Telegrama

            // jesli nie wpisano kodu, nic nie rob
            if (textBox1.Text.Length == 0)
                return;
            // jesli kod poprawny (?) to zapisz go
            code = textBox1.Text;
            // ukryj pola do wpisania kodu
            textBox1.Hide();
            label15.Hide();
            button6.Hide();
            // debug purposes
            //MessageBox.Show("Kod: " + code);
            // wyslij do Telegrama kod
            user = await client.MakeAuthAsync("48886509311", hash, code);
            // wyswietl komunikat, gdy logowanie przebieglo pomyslnie
            if(user.Id > 0)
            {
                label16.Show();
                label17.Show();
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // button dalej na screenie z logowaniem do telegrama

            if (chats != null)
            {
                foreach(TLChat chat in chats)
                {
                    listView2.Items.Add(chat.Title.ToString());
                }

                panel_telegram_login.Hide();
                panel_telegram_chats.Show();
            }
            else
            {
                MessageBox.Show("chats == null");
            }
        }
        /* --- SCREEN Z LOGOWANIEM DO TELEGRAMA --- */

        /* --- SCREEN Z GRUPAMI WSPARCIA TELEGRAMA --- */
        private async void button7_Click(object sender, EventArgs e)
        {
            // button dalej na screenie z grupami wsparcia telegrama
            panel_telegram_chats.Hide();

            foreach(var chat in chats)
            {
                try
                {
                    TLAbsMessages msgs = await client.GetHistoryAsync(new TLInputPeerChat { ChatId = chat.Id }, 0, 100, 100);
                    chats_messages.Add(new ChatsMessages(chat.Id, msgs));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("a "+ex.Message.ToString());
                }
            }

            foreach(var chat_msgs in chats_messages)
            {
                foreach(var msg in chat_msgs.GetChatsMessages())
                {
                    // dostep do czystej wiadomosci z zawartoscia linku instagrama
                    
                }
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            // button przenies chat jako grupe wsparcia
            if(listView2.SelectedIndices.Count > 0)
            {
                listView3.Items.Add(listView2.SelectedItems[0].Text);

                string sql = $"INSERT INTO support_group_names (id_group, group_name) VALUES (NULL, '{listView2.SelectedItems[0].Text}')";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca

                listView2.SelectedItems[1].Remove();
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            // button usun chat z grup wsparcia

            if (listView3.SelectedIndices.Count > 0)
            {
                listView2.Items.Add(listView3.SelectedItems[0].Text);

                string sql = $"DELETE FROM support_group_names WHERE group_name = '{listView3.SelectedItems[0].Text}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca

                listView3.SelectedItems[0].Remove(); 
            }
        }
        /* --- SCREEN Z GRUPAMI WSPARCIA TELEGRAMA --- */

        /* --- SCREEN LAJKOWANIA ZDJEC --- */
        private void InitChatsList()
        {
            if (chats == null)
                return;

            foreach(var chat in chats)
            {
                listBox1.Items.Add(chat.Title.ToString());
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            // button dalej na ekranie lajkowania zdjec

        }

        private void button11_Click(object sender, EventArgs e)
        {
            // polajkuj zdjecie insta

        }

        private void button12_Click(object sender, EventArgs e)
        {
            // dodaj komentarz insta

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // zaznaczono element na liscie -> pokaz zdjecia z tej grupy

        }

        private void button13_Click(object sender, EventArgs e)
        {
            // nastepne zdjecie z grupy

        }
        /* --- SCREEN LAJKOWANIA ZDJEC --- */
    }
}
