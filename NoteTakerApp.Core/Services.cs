using DropboxAudioTranscription.Modules;
 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

  namespace NoteTakerApp.Core
{
    public class Services
    {
        private readonly DropboxAccount account;
        private readonly RecordingModule recording;
        private readonly DropboxModule dropbox;
        //public Services()
        //{

        //}

        public Services(StorageAccount account)
        {
            this.account = new DropboxAccount();
            this.account.User = account.User;
            this.dropbox = new DropboxModule(this.account);

            this.recording = new RecordingModule();
        }

        #region DROPBOX AUTHENTICATION
        public void Login()
        {
            dropbox.Login();
        }
        #endregion

        #region TRANSCRIPTION
        public void Record(RecordingOptions options)
        {
            var SaveOnlyLastNSeconds = options.RecordingLimit != null;

            if (SaveOnlyLastNSeconds)
                recording.StartRecordingWithLimits(options.SegmentDuration, options.RecordingLimit.Value);
            else
                recording.StartRecording(options.SegmentDuration); //recording in segments

        }

        public void Process(int lastNSeconds)
        {
            recording.ProcessFiles(lastNSeconds);
        }

        public void StopRecording()
        {
            recording.Stop();
        }

        #endregion


        #region DOCUMENT SYNC

        #endregion
    }
}
