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
        private Services services;

        public Form1()
        {
            InitializeComponent();
            dropboxAccount = new  StorageAccount();
            services = new Services(dropboxAccount);
        }

        private void Form1_Load(object sender, EventArgs e)
        {



            //dbs.Login
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

                 services.Record(RecordingOptions);
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
            services.StopRecording();
            Cursor = Cursors.Default;
        }
    }
}
