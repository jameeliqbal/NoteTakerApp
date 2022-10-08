using System;
using System.IO;

namespace CaptureScreenAudioVideo
{
    public abstract class CaptureBase
    {
        protected bool isActive = false; // Capture object activity flag.

        protected string savePath = "";

        protected const string timestampPattern = "yyyy_MM_dd_HH_mm_ss_fff";

        /**
         * Starts capture.
         */
        public abstract void Start();

        /**
         * Stops capture.
         */
        public abstract void Stop();

        /**
         * Sets directory path to save files into it.
         * Creates the directory if it doesn't exist yet.
         */
        public void SetFolder(string saveDir)
        {
            savePath = saveDir;

            if (Directory.Exists(savePath) == false)
            {
                Directory.CreateDirectory(savePath);
            }

            if (savePath.EndsWith("/") == false)
            {
                savePath += "/";
            }
        }

        /**
         * Generates file name using timestamp pattern.
         * fileExt - file extension to be added to generated file name.
         */
        protected string PrepareFullImageFileName(string fileExt)
        {
            string timeStamp = DateTime.Now.ToString(timestampPattern);

            string fullFileName = savePath + timeStamp + fileExt;

            return fullFileName;
        }
    }
}
