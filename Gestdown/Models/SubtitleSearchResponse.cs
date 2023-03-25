using System;
using System.Collections.Generic;

namespace Gestdown.Models
{
    public class Episode
    {
        public int season { get; set; }
        public int number { get; set; }
        public string title { get; set; }
        public string show { get; set; }
        public DateTime discovered { get; set; }
    }

    public class MatchingSubtitle
    {
        public Guid subtitleId { get; set; }
        public string version { get; set; }
        public bool completed { get; set; }
        public bool hearingImpaired { get; set; }
        public bool corrected { get; set; }
        public bool hd { get; set; }
        public string downloadUri { get; set; }
        public string language { get; set; }
        public DateTime discovered { get; set; }
        public int downloadCount { get; set; }
    }

    public class SubtitleSearchResponse
    {
        public List<MatchingSubtitle> matchingSubtitles { get; set; }
        public Episode episode { get; set; }
    }
}