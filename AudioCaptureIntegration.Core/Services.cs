using AudioCaptureIntegration.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioCaptureIntegration.Core
{
    public class Services
    {
        private readonly DropboxAccount account;
        private readonly RecordingModule recording;
        private readonly DropboxModule dropbox;
        //public Services()
        //{

        //}

        public Services(DropboxAccount account)
        {
            this.account = account;
            this.recording = new RecordingModule();
            this.dropbox = new DropboxModule(account);
        }

        public void Login()
        {
            dropbox.Login();
        }

        public void Record(RecordingOptions options)
        {
              recording.Start(options);
        }

        public void Process(int lastNMinutes)
        {
            recording.ProcessFiles(lastNMinutes);
        }
    }
}
