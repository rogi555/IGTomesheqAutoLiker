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
        private string connectionString;
        private InitDataHolder data_holder;
        public string test;

        public BackgroundWorker BackgroundWorker;

        public SplashScreen(MyApplicationContext parent)
        {
            _parent = parent;
            InitializeComponent();

            // baza danych
            connectionString = "Data Source=tomesheq_db.db;Version=3;";

            // data holder
            data_holder = _parent.InitData;

            // BackgroundWorker
            BackgroundWorker = new BackgroundWorker();
            BackgroundWorker.DoWork += BackgroundWorker_DoWork;
            BackgroundWorker.ProgressChanged += BackgroundWorker_ProgressChanged;
            BackgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            BackgroundWorker.WorkerReportsProgress = true;

            // umiejscowienie okna
            this.CenterToScreen();
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // przepisuje zgromadzone dane do AppContext
            _parent.InitData = data_holder;

            // zamyka splash-screena
            this.Close();
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            switch(e.ProgressPercentage)
            {
                case 10:
                    toolStripStatusLabel1.Text = "Loguję się do Telegrama...";
                    statusStrip1.Refresh();
                    break;
                case 11:
                    toolStripStatusLabel1.Text = "Konieczne będzie ręczne zalogowanie do Telegrama...";
                    statusStrip1.Refresh();
                    break;
                case 20:
                    toolStripStatusLabel1.Text = "Pobieram chaty z Telegrama...";
                    statusStrip1.Refresh();
                    break;
                case 21:
                    toolStripStatusLabel1.Text = "Nie udało się pobrać chatów...";
                    statusStrip1.Refresh();
                    break;
                case 30:
                    toolStripStatusLabel1.Text = "Loguję się do Instagrama...";
                    statusStrip1.Refresh();
                    break;
                case 31:
                    toolStripStatusLabel1.Text = "Konieczne będzie ręczne zalogowanie do Instagrama...";
                    statusStrip1.Refresh();
                    break;
                case 40:
                    toolStripStatusLabel1.Text = "Pobieram domyślne komentarze z bazy danych...";
                    statusStrip1.Refresh();
                    break;
                case 41:
                    toolStripStatusLabel1.Text = "Nie udało się załadować domyślnych komentarzy - problem z bazą danych...";
                    statusStrip1.Refresh();
                    break;
                case 50:
                    toolStripStatusLabel1.Text = "Pobieram zapamiętane grupy wsparcia z bazy danych...";
                    statusStrip1.Refresh();
                    break;
                case 51:
                    toolStripStatusLabel1.Text = "Nie udało się załadować grup wsparcia - problem z bazą danych...";
                    statusStrip1.Refresh();
                    break;
                case 60:
                    toolStripStatusLabel1.Text = "Gotowe!";
                    statusStrip1.Refresh();
                    break;
            }
        }

        private async void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            // telegram
            worker.ReportProgress(10);
            if (!await data_holder.InitTelegram())
            {
                worker.ReportProgress(11);
                Thread.Sleep(500);
            }

            // pobieranie chatów z telegrama
            worker.ReportProgress(20);
            if (!await data_holder.GetAllTelegramChannelsAndChats())
            {
                worker.ReportProgress(21);
                Thread.Sleep(500);
            }

            // instagram
            worker.ReportProgress(30);
            if (!await data_holder.InitInstagram())
            {
                worker.ReportProgress(31);
                Thread.Sleep(500);
            }

            // Sprawdzenie followersów z bagdadu
            //await data_holder.GetBagdadFollowers();

            // domyślne komentarze
            worker.ReportProgress(40);
            if (!data_holder.GetDefaultComments())
            {
                worker.ReportProgress(41);
                Thread.Sleep(500);
            }

            // grupy wsparcia
            worker.ReportProgress(50);
            if (!data_holder.GetSupportGroups()) // !!! dorobić ifa - zraca false w przypadku niepowodzneia
            {
                worker.ReportProgress(51);
                Thread.Sleep(500);
            }
            worker.ReportProgress(60);
            Thread.Sleep(500);
        }

        // pobiera dane potrzebne do działania aplikacji
        public async Task<InitDataHolder> InitializeAppData()
        {
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
            toolStripStatusLabel1.Text = "Pobieram chaty z Telegrama...";
            statusStrip1.Refresh();
            if(!await data_holder.GetAllTelegramChannelsAndChats())
            {

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
            toolStripStatusLabel1.Text = "Gotowe!";
            statusStrip1.Refresh();
            Thread.Sleep(500);
            // zwraca uzyskane informacje dla formularza z głównym interfejsem aplikacji
            return data_holder;
        }
    }
}
