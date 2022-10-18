using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoteTakerApp.Core
{
    public class AuthenticatedEventArgs:EventArgs
    {
        public AuthenticatedEventArgs(StorageAccount account)
        {
            Account = account;
        }

        public StorageAccount Account { get; set; }
    }

    public class AuthenticationCancelledEventArgs : EventArgs
    {
        public AuthenticationCancelledEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; set; }
    }
}
