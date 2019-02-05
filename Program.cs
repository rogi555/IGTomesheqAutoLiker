using IGTomesheq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace IGTomesheqAutoLiker
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MyApplicationContext context = new MyApplicationContext();
            Application.Run(context);
        }
    }
}
