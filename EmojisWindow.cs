using IGTomesheqAutoLiker;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IGTomesheq
{
    public partial class EmojisWindow : Form
    {
        List<SingleEmoji> complete_emojis;
        List<Label> emoji_labels;
        Panel panel_left;
        Panel panel_right;
        Label next_screen_label;
        Label previous_screen_label;

        private string connectionString;

        Form1 parent_form;

        public EmojisWindow(Form1 parent)
        {
            InitializeComponent();

            // DB
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            // utworzenie paneli do przechowywania emojis
            panel_left = new Panel();
            panel_left.Size = new Size(770,420);
            panel_left.Location = new Point(0,0);
            panel_left.Margin = new Padding(0, 0, 0, 0);

            panel_right = new Panel();
            panel_right.Size = new Size(770, 420);
            panel_right.Location = new Point(15, 0);
            panel_right.Margin = new Padding(0, 0, 0, 0);

            panel_left.Visible = true;
            panel_right.Visible = false;

            this.Controls.Add(panel_left);
            this.Controls.Add(panel_right);

            // parent
            parent_form = parent;

            // utworzenie przyciskow przewijania emojis
            next_screen_label = new Label();
            previous_screen_label = new Label();

            // utworzenie obiektu z klasami emoji
            complete_emojis = new List<SingleEmoji>();
            emoji_labels = new List<Label>();

            // objekt Emojis
            J3QQ4.Emoji emojis = new J3QQ4.Emoji();

            // znalezienie pol
            var fields = typeof(J3QQ4.Emoji).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            //var names = Array.ConvertAll(fields, field => field.Name);

            // 
            int i = 0;

            // tworzy emojis w DB jesli jest taka potrzeba
            foreach (var field in fields)
            {
                var ss = field.GetValue(emojis);
                CreateEmojiDBRecord(ss.ToString());
                i++;

                //if (i >= 480)
                //    break;
            }

            // pokazuje emojis w labelach
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"SELECT * FROM emojis ORDER BY times_used DESC, id ASC";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    int j = 0;
                    while (reader.Read())
                    {
                        if (j < 480)
                        {
                            CreateEmojiLabel(0, j, reader.GetString(reader.GetOrdinal("emoji"))); 
                        }
                        else
                        {
                            CreateEmojiLabel(1, j, reader.GetString(reader.GetOrdinal("emoji")));
                        }
                        j++;
                    }
                }
            }
 
            next_screen_label.Text = ">";
            next_screen_label.AutoSize = false;
            next_screen_label.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            next_screen_label.Location = new System.Drawing.Point(770, 0);
            next_screen_label.Size = new System.Drawing.Size(20, 420);
            next_screen_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            next_screen_label.Click += Next_screen_label_Click;
            next_screen_label.Visible = true;

            previous_screen_label.Text = "<";
            previous_screen_label.AutoSize = false;
            previous_screen_label.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            previous_screen_label.Location = new System.Drawing.Point(0, 0);
            previous_screen_label.Margin = new Padding(0,0,0,0);
            previous_screen_label.Size = new System.Drawing.Size(20, 420);
            previous_screen_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            previous_screen_label.Click += Previous_screen_label_Click;
            previous_screen_label.Visible = false;

            this.Controls.Add(next_screen_label);
            this.Controls.Add(previous_screen_label);
        }

        private void Previous_screen_label_Click(object sender, EventArgs e)
        {
            panel_left.Show();
            panel_right.Hide();
            previous_screen_label.Hide();
            next_screen_label.Show();
        }

        private void Next_screen_label_Click(object sender, EventArgs e)
        {
            panel_left.Hide();
            panel_right.Show();
            next_screen_label.Hide();
            previous_screen_label.Show();
            
            //MessageBox.Show("Next screen...");
        }

        private void CreateEmojiLabel(int side /* 0 - left; 1 - right */, int label_nr, string emoji)
        {
            int column, row;
            if (label_nr >= 480)
            {
                column = (label_nr - 480) % 30;
                row = (int)((label_nr - 480) / 30);
            }
            else
            {
                column = label_nr % 30;
                row = (int)(label_nr / 30);
            }

            Label tmp_label = new Label();
            tmp_label.Font = new System.Drawing.Font("Microsoft Sans Serif", 16F);
            tmp_label.Location = new System.Drawing.Point(15 + (column * 25), 15 + (row * 25));
            tmp_label.Name = "label" + label_nr.ToString();
            tmp_label.Size = new System.Drawing.Size(25, 25);
            tmp_label.TabIndex = 0;
            tmp_label.Text = emoji;
            tmp_label.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            tmp_label.Click += new System.EventHandler(this.label_Click);

            complete_emojis.Add(new SingleEmoji(emoji, label_nr));

            emoji_labels.Add(tmp_label);

            if (side == 0)
            {
                panel_left.Controls.Add(emoji_labels.Last()); 
            }
            else if(side == 1)
            {
                panel_right.Controls.Add(emoji_labels.Last());
            }
            else
            {
                MessageBox.Show("Nie umiem wybrać odpowiedniej strony... :(");
            }
        }

        private void CreateEmojiDBRecord(string emoji)
        {
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(connectionString))
            {
                m_dbConnection.Open();
                string sql = $"SELECT * FROM emojis WHERE emoji = '{emoji}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                SQLiteDataReader reader = command.ExecuteReader();
                if (!reader.HasRows)
                {
                    sql = $"INSERT INTO emojis (id, emoji, times_used) VALUES (NULL, '{emoji}', 0)";
                    command = new SQLiteCommand(sql, m_dbConnection);
                    command.ExecuteNonQuery(); // nic nie zwraca
                }
                m_dbConnection.Close();
            }
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

        private void label_Click(object sender, EventArgs e)
        {
            Label tmp = (Label)sender;
            UpdateEmojiUsed(complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
            parent_form.InsertEmojiIntoRichTextBox(complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
            
            this.Close();
            //MessageBox.Show("Kliknieto " + complete_emojis.Where(x => x.label_name == tmp.Name).FirstOrDefault().emoticon);
        }

        private void EmojisWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
    }

    public class SingleEmoji
    {
        public string emoticon;
        public string label_name;

        public SingleEmoji(string emoji, int label_nr)
        {
            emoticon = emoji;
            label_name = "label" + label_nr.ToString();
        }
    }
}
