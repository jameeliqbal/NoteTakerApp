using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DropboxAudioTranscription.Modules
{
    public class DropboxAccount
    {
        public string User { get; set; }
    }

    public class DropboxModule
    {
        private readonly DropboxAccount account;
        public DropboxModule(DropboxAccount account)
        {
            this.account = account;
        }

        public void Login()
        {

        }
    }
}
