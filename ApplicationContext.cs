using IGTomesheqAutoLiker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IGTomesheqAutoLiker
{
    class MyApplicationContext : ApplicationContext
    {
        Form1 main_form;
        SplashScreen splash_screen;
        InitDataHolder InitData;

        public MyApplicationContext()
        {
            // tworzy obiekt przechowujacy wszystkie dane inicjalizacyjne, zbierane podczas wyswietlania splash screena
            InitData = new InitDataHolder();

            // tworzy splash screena
            splash_screen = new SplashScreen(InitData);
            splash_screen.FormClosing += Splash_screen_FormClosing;
            splash_screen.FormClosed += Splash_screen_FormClosed;
            splash_screen.Activated += Splash_screen_Activated;

            // pokazuje splash screena
            splash_screen.Show();
            splash_screen.Refresh();
        }

        private async void Splash_screen_Activated(object sender, EventArgs e)
        {
            InitData = await splash_screen.InitializeAppData();
            // zamyka splash screena - zostanie pokazany glowny formularz
            splash_screen.Close();
        }

        private void Splash_screen_FormClosed(object sender, FormClosedEventArgs e)
        {
            main_form.Show();
        }

        private void Splash_screen_FormClosing(object sender, FormClosingEventArgs e)
        {
            main_form = new Form1(InitData);
            main_form.FormClosing += Main_form_FormClosing;
            main_form.FormClosed += Main_form_FormClosed;
            main_form.Activated += Main_form_Activated;
        }

        private void Main_form_Activated(object sender, EventArgs e)
        {
            main_form.Initialize();
        }

        private void Main_form_FormClosed(object sender, FormClosedEventArgs e)
        {
            ExitThread();
        }

        private void Main_form_FormClosing(object sender, FormClosingEventArgs e)
        {
            
        }
    }
}
