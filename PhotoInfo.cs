using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IGTomesheq
{
    public partial class PhotoInfo : Form
    {
        public PhotoInfo(bool telegram_msg, string info_text)
        {
            InitializeComponent();

            if(telegram_msg)
            {
                label2.Text = "Treść wiadomości Telegram";
            }
            else
            {
                label2.Text = "Opis zdjęcia na insta";
            }
            richTextBox1.Text = info_text;
        }
    }
}
