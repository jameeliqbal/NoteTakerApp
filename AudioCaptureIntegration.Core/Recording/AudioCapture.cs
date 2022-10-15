using System;
using System.IO;
using NAudio.Wave;
using NAudio.Lame;
using System.Timers;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace CaptureScreenAudioVideo
{
    public class AudioCapture : CaptureBase
    {
        private TimeSpan? SegmentLimit;
        private long SegmentLimitInBytes;
        private long CurrentSegmentLengthInBytes;
        private TimeSpan? RecordingLimit;
        private bool ProcessFilesImmediately = true ;
        TimeSpan recordedTime;
        List<AudioFileReader> recordingsToDelete;

        //+++++++++++++++

        private const int defaultBitRate = 320;

        private int bitRate = defaultBitRate; // MP3 audio stream bit rate.

        private const string mp3FileExt = ".mp3";

        WasapiLoopbackCapture audioCapture;

        LameMP3FileWriter fileWriter;

        bool fileSaved = false;

        private AudioCapture() { } // For singleton implementation.

        private static AudioCapture _instance;
        private static string mp3FileFullPath;
        public static AudioCapture GetInstance()
        {
            if (_instance == null)
            {
                _instance = new AudioCapture();
            }

            return _instance;
        }


        public void Init()
        {
            audioCapture = new WasapiLoopbackCapture();
            audioCapture.DataAvailable += AudioCapture_DataAvailable;
            audioCapture.RecordingStopped += AudioCapture_RecordingStopped;
             
        }

        private void AudioCapture_RecordingStopped(object sender, StoppedEventArgs e)
        {
            if (fileWriter == null)
            {
                return;
            }

            fileWriter.Flush();
            fileWriter.Close();
            fileWriter.Dispose();
            fileWriter = null;

            fileSaved = true;
            CurrentSegmentLengthInBytes = 0;
        }

        private void AudioCapture_DataAvailable(object sender, WaveInEventArgs a)
        {
            if (fileWriter == null)
            {
                return;
            }

            //if segmentlimit provided, then save recording in segmented files.
            if (SegmentLimit.HasValue)
            {
                //write segment data to file
                int bytesRecorded = a.BytesRecorded;
                fileWriter.Write(a.Buffer, 0, bytesRecorded);

                CurrentSegmentLengthInBytes += bytesRecorded;
                Debug.WriteLine("BUFFER: " + a.Buffer.Length + " - BYTESRECORDED: " + a.BytesRecorded + " CurrentSegmentLengthInBytes: " + CurrentSegmentLengthInBytes);

                bool IsSegmentLimitReached = CurrentSegmentLengthInBytes > SegmentLimitInBytes;
                if (IsSegmentLimitReached)
                {
                    //finalize the current segment file and close.
                    fileWriter.Flush();
                    fileWriter.Close();
                    fileSaved = true;


                    if (ProcessFilesImmediately)
                    {
                        var fileToProcess = mp3FileFullPath.Clone().ToString();
                        Task.Run(()=>ProcessFiles(fileToProcess));
                    }
                    else  
                    {
                        //Keep only lastNSeconds of Recording, remove unwanted segments
                        Task.Run(TrimRecording);

                    }
                    //create new segment file
                    string timeStamp = DateTime.Now.ToString(timestampPattern);
                    mp3FileFullPath = savePath + timeStamp + mp3FileExt;
                    fileWriter = new LameMP3FileWriter(mp3FileFullPath, audioCapture.WaveFormat, bitRate);

                    //reset values for new segment
                    fileSaved = false;
                    CurrentSegmentLengthInBytes = 0;
                }

            }
            else
            {
                //save recording in a single file
                fileWriter.Write(a.Buffer, 0, a.BytesRecorded);
                Debug.WriteLine("BUFFER: " + a.Buffer.Length + " - BYTESRECORDED: " + a.BytesRecorded + " POSITION: " + fileWriter.Position);

            }
        }

        private void ProcessFiles(string fileToProcess)
        {
            //upload segmentfile
        }

        /// <summary>
        /// Remove recordings that are outside the recording limit.
        /// </summary>
        private void TrimRecording()
        {
            var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
            var mp3FileinCurrentDirectory = currentDirectory.GetFileSystemInfos()
                .Where(f => f.Extension == ".mp3")
                .OrderByDescending(f => f.Name);

            TimeSpan currentRecordingDuration = new TimeSpan();

            foreach (var mp3file in mp3FileinCurrentDirectory)
            {
                bool isRecordingLimitReached = currentRecordingDuration > RecordingLimit;
                if (isRecordingLimitReached)
                {
                    //remove older recordings
                    if (File.Exists(mp3file.FullName))
                        mp3file.Delete();

                    continue;
                }

                AudioFileReader recording;

                try
                {
                    //track duration of current recording
                    recording = new AudioFileReader(mp3file.FullName);
                    currentRecordingDuration += recording.TotalTime;

                    Debug.WriteLine("File: " + mp3file.Name + "    Duration: " + recording.TotalTime);
                }
                catch (System.IO.FileLoadException)
                {
                    //skip tracking duration of this file and continue if recording is in progress
                    continue;
                }

            }
            
        }

        public void Release()
        {
            audioCapture.Dispose();

            audioCapture = null;
        }

        public override void Start()
        {
            if (isActive)
            {
                return;
            }

            mp3FileFullPath = PrepareFullImageFileName(mp3FileExt);
            StartRecording(mp3FileFullPath);

            isActive = true;
        }

        /// <summary>
        /// Use this method to save recording in segmented files
        /// </summary>
        /// <param name="segmentLimit"></param>
        public void Start(int segmentLimit)
        {
            SegmentLimit = TimeSpan.FromSeconds(segmentLimit);
            SegmentLimitInBytes = (long)(audioCapture.WaveFormat.AverageBytesPerSecond * segmentLimit);
            CurrentSegmentLengthInBytes = 0;

            Start();

        }

        /// <summary>
        /// Use this method to limit recording to last N seconds, and also save recording in segmented files.
        /// </summary>
        /// <param name="segmentLength"></param>
        /// <param name="recordingLimit" Description="Limit the recording to recordingLimit seconds "></param>
        public void Start(int segmentLength, int recordingLimit)
        {
            ProcessFilesImmediately = false;
            RecordingLimit = TimeSpan.FromSeconds(recordingLimit);
            recordedTime = new TimeSpan();
            recordingsToDelete = new List<AudioFileReader>();
            Start(segmentLength);
        }

        public override void Stop()
        {
            if (isActive == false)
            {
                return;
            }

            StopRecording();

            isActive = false;
        }

        public void SetAudioCodecParams(int bitRate)
        {
            this.bitRate = bitRate;
        }

        private void StartRecording(string mp3FileName)
        {
            fileSaved = false; // For new recording session we reset this flag value.

            fileWriter = new LameMP3FileWriter(mp3FileName, audioCapture.WaveFormat, bitRate);

            audioCapture.StartRecording();
        }

        private void StopRecording()
        {
            audioCapture.StopRecording();
        }

        public bool IsFileSaved()
        {
            return fileSaved;
        }
        public string GetFullMp3FilePath()
        {
            return mp3FileFullPath;
        }

        /// <summary>
        /// deletes all recording files, except file of active recording
        /// </summary>
        public void Clear()
        {
            var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
            var mp3FileinCurrentDirectory = currentDirectory.GetFileSystemInfos()
                .Where(f => f.Extension == ".mp3")
                .OrderByDescending(f => f.Name);

            foreach (var mp3file in mp3FileinCurrentDirectory)
            {
                try
                {
                    //remove  recording
                    if (File.Exists(mp3file.FullName))
                        mp3file.Delete();
                }
                catch (IOException)
                {
                    //skip deleting this file as recording is currently being saved in it
                    continue;
                }

            }
        }
    }
}