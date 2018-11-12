using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TLSharp.Core;
using TeleSharp.TL.Messages;
using TeleSharp.TL;

namespace IGTomesheq
{
    class ChatsMessages
    {
        private int id;
        public int Id
        {
            get { return this.id; }
            set { this.id = value; }
        }
        private TLAbsMessages TlAbsMessages;
        private TLMessages TlMessages;
        private List<TLMessage> filtered_messages;

        public ChatsMessages(int chat_id, TLAbsMessages msgs)
        {
            this.Id = chat_id;
            TlAbsMessages = msgs;
            filtered_messages = new List<TLMessage>();
        }

        public void SetChatId(int chat_id)
        {
            this.Id = chat_id;
        }

        public List<TLMessage> GetChatsMessages()
        {
            TlMessages = (TLMessages)TlAbsMessages;
            for (int i = 0; i < TlMessages.Messages.Count; i++)
            {
                if (TlMessages.Messages[i].ToString() == "TeleSharp.TL.TLMessage")
                {
                    var message = (TLMessage)TlMessages.Messages[i];
                    string tmp_msg = message.Message.ToString();
                    if(tmp_msg.Contains("https://www.instagram.com/"))
                    {
                        filtered_messages.Add(message);
                    }
                }
                else
                {
                    //MessageBox.Show("To MessageService - pierdole to...");
                }
            }

            return filtered_messages;
        }
    }
}
