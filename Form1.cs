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
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // button dalej na screen1
            this.panel_hello.Hide();
            this.panel_comments.Show();

            // zainicjuj formularz danymi
            init_comments_screen();
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

        private void button7_Click(object sender, EventArgs e)
        {
            // button dalej na screenie z logowaniem do telegrama

        }

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

        private async void init_telegram()
        {
            client = new TelegramClient(API_ID, API_HASH);
            await client.ConnectAsync();
            hash = await client.SendCodeRequestAsync("48886509311");
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
        }
    }
}
