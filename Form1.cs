using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace IGTomesheqAutoLiker
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // button dalej na screen1
            this.panel1.Hide();
            this.panel2.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // button dalej na screenie z domyslnymi komentarzami
            this.panel2.Hide();
        }
    }
}
