using System;

namespace Addic7ed.Model.Addicted
{
    public class Subtitle
    {
        public string Version { get; set; }
        public bool Completed { get; set; }
        public bool HearingImpaired { get; set; }
        public bool Corrected { get; set; }
        public bool HD { get; set; }
        public string DownloadUri { get; set; }
        public string Language { get; set; }
        
        /// <summary>
        /// When was the subtitle discovered
        /// </summary>
        public DateTime Discovered { get; }
    }
}