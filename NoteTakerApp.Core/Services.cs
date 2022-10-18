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

        //+++++++

        // Declare the event using EventHandler<T>
        public event EventHandler<AuthenticatedEventArgs> Authenticated;
        public event EventHandler AuthenticationTimedout;
        public event EventHandler<AuthenticationCancelledEventArgs> AuthenticationCancelled;

        public Services(StorageAccount account)
        {
            this.account = new DropboxAccount();
            this.account.AccessToken= account.AccessToken;
            this.account.APIKey = account.APIKey;
            this.account.RedirectURI = account.RedirectURI;
            this.account.RefreshToken = account.RefreshToken;
            this.account.IsConnected = account.IsConnected;
            this.account.RedirectURI = account.RedirectURI;
            this.account.TokenExpiresAt = account.TokenExpiresAt;
            this.account.UID = account.UID;

            this.dropbox = new DropboxModule(this.account);
            this.recording = new RecordingModule();

            this.dropbox.Authenticated += Dropbox_Authenticated;
            this.dropbox.AuthenticationTimedout += Dropbox_AuthenticationTimedout;
            this.dropbox.AuthenticationCancelled += Dropbox_AuthenticationCancelled;
        }

        private void Dropbox_AuthenticationCancelled(object sender, DropboxAuthenticationCancelledEventArgs e)
        {
            var authenticationCancelledEventArgs = new AuthenticationCancelledEventArgs(e.Message);
            OnAuthenticationCancelledEvent(authenticationCancelledEventArgs);
        }

        private void OnAuthenticationCancelledEvent(AuthenticationCancelledEventArgs e)
        {
            EventHandler<AuthenticationCancelledEventArgs> raiseAuthenticationCancelledEvent = AuthenticationCancelled;

            // Event will be null if there are no subscribers
            if (raiseAuthenticationCancelledEvent != null)
            {
                // Call to raise the event.
                raiseAuthenticationCancelledEvent(this, e);
            }
        }

        private void Dropbox_AuthenticationTimedout(object sender, EventArgs e)
        {
            OnTimedoutEvent(e);
        }

        private void OnTimedoutEvent(EventArgs e)
        {
            EventHandler raiseAuthenticationTimedoutEvent = AuthenticationTimedout;

            // Event will be null if there are no subscribers
            if (raiseAuthenticationTimedoutEvent != null)
            {
                // Call to raise the event.
                raiseAuthenticationTimedoutEvent(this, e);
            }
        }

        private void Dropbox_Authenticated(object sender, EventArgs e)
        {
            var account = new StorageAccount();
            account.AccessToken = this.account.AccessToken;
            account.Email = this.account.Email;
            account.IsConnected = this.account.IsConnected;
            account.Name = this.account.Name;
            account.RedirectURI = this.account.RedirectURI;
            account.RefreshToken = this.account.RefreshToken;
            account.TokenExpiresAt = this.account.TokenExpiresAt;
            account.UID = this.account.UID;

            var authenticatedArgs = new AuthenticatedEventArgs(account);
            OnAuthenticatedEvent(authenticatedArgs);
        }


        // Wrap event invocations inside a protected virtual method
        // to allow derived classes to override the event invocation behavior
        protected virtual void OnAuthenticatedEvent(AuthenticatedEventArgs e)
        {
            // Make a temporary copy of the event to avoid possibility of
            // a race condition if the last subscriber unsubscribes
            // immediately after the null check and before the event is raised.
            EventHandler<AuthenticatedEventArgs> raiseAuthenticatedEvent = Authenticated;

            // Event will be null if there are no subscribers
            if (raiseAuthenticatedEvent != null)
            {
                // Call to raise the event.
                raiseAuthenticatedEvent(this, e);
            }
        }

        #region DROPBOX AUTHENTICATION
        public async Task Login()
        {
            await dropbox.Login();
        }

        public async Task Logout()
        {
            await dropbox.SignOut();
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

        public void CancelSignIn()
        {
            dropbox.CancelSignIn();
        }



        #endregion


        #region DOCUMENT SYNC

        #endregion
    }
}
