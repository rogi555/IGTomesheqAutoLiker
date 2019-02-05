using IGTomesheq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace IGTomesheqAutoLiker
{
    public partial class SplashScreen : Form
    {
        private string connectionString;
        private InitDataHolder data_holder;
        public string test;

        public SplashScreen(InitDataHolder holder)
        {
            InitializeComponent();

            // baza danych
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            // data holder
            data_holder = holder;

            // umiejscowienie okna
            this.CenterToScreen();
        }

        // pobiera dane potrzebne do działania aplikacji
        public async Task<InitDataHolder> InitializeAppData()
        {
            // telegram
            toolStripStatusLabel1.Text = "Loguję się do Telegrama...";
            statusStrip1.Refresh();
            if (await data_holder.InitTelegram())
            {
                toolStripStatusLabel1.Text = "Pobieram chaty z Telegrama...";
                statusStrip1.Refresh();
                // pobieranie chatów z telegrama
                await data_holder.GetAllTelegramChannelsAndChats();

            }
            else
            {
                toolStripStatusLabel1.Text = "Konieczne będzie ręczne zalogowanie do Telegrama...";
                statusStrip1.Refresh();
                Thread.Sleep(500);
            }

            toolStripStatusLabel1.Text = "Loguję się do Instagrama...";
            statusStrip1.Refresh();
            // instagram
            if (!await data_holder.InitInstagram())
            {
                toolStripStatusLabel1.Text = "Konieczne będzie ręczne zalogowanie do Instagrama...";
                statusStrip1.Refresh();
                Thread.Sleep(500);
            }
            toolStripStatusLabel1.Text = "Pobieram domyślne komentarze z bazy danych...";
            statusStrip1.Refresh();
            // domyślne komentarze
            data_holder.GetDefaultComments();
            toolStripStatusLabel1.Text = "Pobieram zapamiętane grupy wsparcia z bazy danych...";
            statusStrip1.Refresh();
            // grupy wsparcia
            data_holder.GetSupportGroups();
            toolStripStatusLabel1.Text = "Gotowe!";
            statusStrip1.Refresh();
            Thread.Sleep(500);
            // zwraca uzyskane informacje dla formularza z głównym interfejsem aplikacji
            return data_holder;
        }
    }
}
