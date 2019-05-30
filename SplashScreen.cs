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
        private MyApplicationContext _parent;
        private InitDataHolder data_holder;
        public string test;

        public SplashScreen(MyApplicationContext parent)
        {
            _parent = parent;
            InitializeComponent();

            // data holder
            data_holder = _parent.InitData;

            // umiejscowienie okna
            this.CenterToScreen();
        }

        // pobiera dane potrzebne do działania aplikacji
        public async Task<InitDataHolder> InitializeAppData()
        {
            // usuwanie starych wpisow
            data_holder.DeleteOldDBEntries(14);

            // telegram
            toolStripStatusLabel1.Text = "Loguję się do Telegrama...";
            statusStrip1.Refresh();
            if (!await data_holder.InitTelegram())
            {
                toolStripStatusLabel1.Text = "Konieczne będzie ręczne zalogowanie do Telegrama...";
                statusStrip1.Refresh();
                Thread.Sleep(500);
            }

            // pobieranie chatów z telegrama
            if (data_holder.telegram_ok)
            {
                toolStripStatusLabel1.Text = "Pobieram chaty z Telegrama...";
                statusStrip1.Refresh();
                if (!await data_holder.GetAllTelegramChannelsAndChats())
                {
                    toolStripStatusLabel1.Text = "Nie udało się odczytać danych z konta Telegramowego...";
                    statusStrip1.Refresh();
                    Thread.Sleep(500);
                } 
            }

            // instagram
            toolStripStatusLabel1.Text = "Loguję się do Instagrama...";
            statusStrip1.Refresh();
            if (!await data_holder.InitInstagram())
            {
                toolStripStatusLabel1.Text = "Konieczne będzie ręczne zalogowanie do Instagrama...";
                statusStrip1.Refresh();
                Thread.Sleep(500);
            }

            // Sprawdzenie followersów z bagdadu
            //await data_holder.GetBagdadFollowers();

            // domyślne komentarze
            toolStripStatusLabel1.Text = "Pobieram domyślne komentarze z bazy danych...";
            statusStrip1.Refresh();
            if(!data_holder.GetDefaultComments())
            {
                toolStripStatusLabel1.Text = "Nie udało się załadować domyślnych komentarzy - problem z bazą danych...";
                statusStrip1.Refresh();
                Thread.Sleep(500);
            }

            // grupy wsparcia
            toolStripStatusLabel1.Text = "Pobieram zapamiętane grupy wsparcia z bazy danych...";
            statusStrip1.Refresh();
            if(!data_holder.GetSupportGroups()) // !!! dorobić ifa - zraca false w przypadku niepowodzneia
            {
                toolStripStatusLabel1.Text = "Nie udało się załadować grup wsparcia - problem z bazą danych...";
                statusStrip1.Refresh();
                Thread.Sleep(500);
            }

            // inicjalizacja ustawien ogolnych
            data_holder.InitSettings();

            // koniec dzialania
            toolStripStatusLabel1.Text = "Gotowe!";
            statusStrip1.Refresh();
            Thread.Sleep(500);

            // zwraca uzyskane informacje dla formularza z głównym interfejsem aplikacji
            return data_holder;
        }
    }
}
