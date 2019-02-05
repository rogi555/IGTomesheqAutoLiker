using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGTomesheqAutoLiker
{
    public class DefaultComment
    {
        public string Comment;
        public bool UsedAutomatically;

        public DefaultComment(string comment_text, int used_automatically)
        {
            Comment = comment_text;
            UsedAutomatically = used_automatically == 0 ? false : true ;
        }
    }
}
