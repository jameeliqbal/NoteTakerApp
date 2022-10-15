using System;

namespace DropboxAudioTranscription.Modules
{
    public class DropboxAccount
    {
        public string APIKey { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public bool IsConnected { get; set; }
        public string RedirectURI { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string UID { get; set; }
        public DateTime TokenExpiresAt { get; set; }


    }
}
