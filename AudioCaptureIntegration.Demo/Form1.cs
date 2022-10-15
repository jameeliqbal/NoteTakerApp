using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using NoteTakerApp.Core;
//using AudioCaptureIntegration.Modules;

namespace AudioCaptureIntegration.Demo
{
    public partial class Form1 : Form
    {
        public RecordingOptions RecordingOptions { get; set; }

        private StorageAccount dropboxAccount;
        private Services _cloudAgent;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeDropboxServices()
        {
            dropboxAccount = new StorageAccount();

            if (!string.IsNullOrEmpty(Properties.Settings.Default.DropboxAccountAPIKey))
            {
                dropboxAccount.APIKey = Properties.Settings.Default.DropboxAccountAPIKey;
            }

            dropboxAccount.AccessToken = Properties.Settings.Default.DropboxAccountAccessToken;
            dropboxAccount.Email = Properties.Settings.Default.DropboxAccountEmail;
            dropboxAccount.Name = Properties.Settings.Default.DropboxAccountName;
            dropboxAccount.RefreshToken = Properties.Settings.Default.DropboxAccountRefreshToken;
            dropboxAccount.UID = Properties.Settings.Default.DropboxAccountUID;

            if (!string.IsNullOrEmpty(Properties.Settings.Default.CloudAccountTokenExpiresAt))
                dropboxAccount.TokenExpiresAt = DateTime.Parse(Properties.Settings.Default.CloudAccountTokenExpiresAt);

            _cloudAgent = new Services(dropboxAccount);
        }

        private void SubscribeToServicesEvents()
        {
            _cloudAgent.Authenticated += _cloudAgent_Authenticated;
            _cloudAgent.AuthenticationTimedout += _cloudAgent_AuthenticationTimedout;
        }

        private void _cloudAgent_AuthenticationTimedout(object sender, EventArgs e)
        {
            Invoke(new Action(() => {
                logSyncInfo("Authentication Timed out!");
                SetAuthenticationControlsState();
            }));
        }

        private void _cloudAgent_Authenticated(object sender, AuthenticatedEventArgs e)
        {
            dropboxAccount.AccessToken = e.Account.AccessToken;
            dropboxAccount.Email = e.Account.Email;
            dropboxAccount.Name = e.Account.Name;
            dropboxAccount.RefreshToken = e.Account.RefreshToken;
            dropboxAccount.TokenExpiresAt = e.Account.TokenExpiresAt;
            dropboxAccount.UID = e.Account.UID;

            SaveToSettings();

            logSyncInfo($"Connected to Account ({dropboxAccount.Email})");
            Invoke(new Action(() =>
            {
                btnCancelSignIn.Visible = false;
                btnSignIn.Enabled = false;
                btnSignOut.Enabled = true;
                lblEmail.Text = dropboxAccount.Email;
                lblUsername.Text = dropboxAccount.Name;
                Activate();
                MessageBox.Show("Connected to Dropbox!", "SUCCESS", MessageBoxButtons.OK); ;
            }));

        }

        private void SaveToSettings()
        {
            Properties.Settings.Default.DropboxAccountAccessToken = dropboxAccount.AccessToken;
            Properties.Settings.Default.DropboxAccountEmail = dropboxAccount.Email;
            Properties.Settings.Default.DropboxAccountName = dropboxAccount.Name;
            Properties.Settings.Default.DropboxAccountRefreshToken = dropboxAccount.RefreshToken;
            Properties.Settings.Default.CloudAccountTokenExpiresAt = dropboxAccount.TokenExpiresAt.ToString();
            Properties.Settings.Default.DropboxAccountUID = dropboxAccount.UID;
            Properties.Settings.Default.Save();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            InitializeDropboxServices();
            SubscribeToServicesEvents();
            SetAuthenticationControlsState();

            //_cloudAgent.Authenticated += _cloudAgent_Authenticated;
            //_cloudAgent.AuthenticationCancelled += _cloudAgent_AuthenticationCancelled;
            //_cloudAgent.NewFilesAdded += _cloudAgent_NewFilesAdded;

            //_cloudAgent.FolderMonitoringStarted += _cloudAgent_FolderMonitoringStarted;
            //_cloudAgent.FolderMonitoringStopped += _cloudAgent_FolderMonitoringStopped;

            //_cloudAgent.TranscriptionFileDeleted += _cloudAgent_TranscriptionFileDeleted;
            //_cloudAgent.TranscriptionFileDownloaded += _cloudAgent_TranscriptionFileDownloaded;
            //_cloudAgent.TranscriptionFileCreated += _cloudAgent_TranscriptionFileCreated;
            //_cloudAgent.AudioFileUploaded += _cloudAgent_AudioFileUploaded;




            //dbs.Login
        }

        private void SetAuthenticationControlsState()
        {
            if (string.IsNullOrEmpty(dropboxAccount.RefreshToken))
            {
                dropboxAccount.IsConnected = false;
                btnSignIn.Enabled = true;
                btnSignOut.Enabled = false;

                lblUsername.Text = "Not logged in";
                lblEmail.Text = "Click Sign-In button to connect to Dropbox";
            }
            else
            {
                dropboxAccount.IsConnected = true;
                btnSignIn.Enabled = false;
                btnSignOut.Enabled = true;
                lblEmail.Text = dropboxAccount.Email;
                lblUsername.Text = dropboxAccount.Name;
            }

            btnCancelSignIn.Visible = false;
        }

        private  void btnStartRecording_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var segmentDuration = txtSegmentDuration.Text.Trim();
                var recordingLimit = txtRecordingLimit.Text.Trim();

                if (string.IsNullOrEmpty(segmentDuration)) return;

                RecordingOptions = new RecordingOptions();
                RecordingOptions.SegmentDuration = int.Parse(segmentDuration);

                if (!string.IsNullOrEmpty(recordingLimit))
                {
                    RecordingOptions.RecordingLimit = int.Parse(recordingLimit);

                }

                 _cloudAgent.Record(RecordingOptions);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                var errorMessage = "Error while recording: " + Environment.NewLine + Environment.NewLine;
                errorMessage += ex.Message + Environment.NewLine + ex.InnerException?.Message;
                MessageBox.Show(errorMessage,"ERROR");
            }
        }

        private void btnStopRecording_Click(object sender, EventArgs e)
        {
            _cloudAgent.StopRecording();
            Cursor = Cursors.Default;
        }

        private async void btnSignIn_Click(object sender, EventArgs e)
        {
            try
            {
                btnSignIn.Enabled = false;
                btnCancelSignIn.Visible = true;

                await _cloudAgent.Login();

            }
            catch (Exception ex)
            {
                var errorMessage = "Error while connecting:" + Environment.NewLine + Environment.NewLine;
                errorMessage += "HRESULT = 0x" + ex.HResult + " "+ ex.Message + Environment.NewLine + ex.InnerException?.Message;
                MessageBox.Show(errorMessage, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logSyncInfo(errorMessage);
            }
        }

        private void logSyncInfo(string text)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(logSyncInfo), new object[] { text });
                return;
            }
            txtLog.AppendText($"[{DateTime.Now}] {text}{Environment.NewLine}");
        }

        private async void btnSignOut_Click(object sender, EventArgs e)
        {
            try
            {
                btnSignOut.Enabled = false;

                await _cloudAgent.Logout();
                dropboxAccount = null;
                _cloudAgent = null;
                Properties.Settings.Default.Reset();
               
                InitializeDropboxServices();
                SubscribeToServicesEvents();

                btnSignIn.Visible = true;

                lblUsername.Text = "Not logged in";
                lblEmail.Text = "Click Sign-In button to connect to Dropbox";

                var message = "Signed Out";
                MessageBox.Show(message, "SUCCESS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                logSyncInfo(message);

                btnSignIn.Enabled = true;
            }
            catch (Exception ex)
            {
                btnSignOut.Enabled = true;

                var errorMessage = "Error while signing out:" + Environment.NewLine + Environment.NewLine;
                errorMessage += "HRESULT = 0x" + ex.HResult + ex.Message + Environment.NewLine + ex.InnerException?.Message;
                MessageBox.Show(errorMessage, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logSyncInfo(errorMessage);
            }

        }
    }
}
