using CaptureScreenAudioVideo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 
namespace DropboxAudioTranscription.Modules
{
    
    public class RecordingModule
    {
        
        private readonly AudioCapture audioCapture;
        public RecordingModule()
        {
            audioCapture = AudioCapture.GetInstance();
            audioCapture.Init();
        }

        public void StartRecording(int segmentLimit)
        {
       
                audioCapture.Start(segmentLimit);  
        }

        public void StartRecordingWithLimits(int segmentLimit,int recordingLimit)
        {
            audioCapture.Start(segmentLimit, recordingLimit);

        }
        public void Stop()
        {
            audioCapture.Stop();
        }

        private void ProcessFiles()
        {

        }

        public void ProcessFiles(int lastNMinutes)
        {
            
        }
    }
}
