using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGTomesheq
{
    class TelegramMessage
    {
        private long id;
        public long ID
        {
            get { return this.id; }
            set { this.id = value; }
        }
        private string message_text;
        public string MessageText
        {
            get { return this.message_text; }
            set { this.message_text = value; }
        }
        private long date;
        public long Messagedate
        {
            get { return this.date; }
            set { this.date = value; }
        }

    }
}
