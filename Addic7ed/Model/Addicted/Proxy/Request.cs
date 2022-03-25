using Addic7ed.Model.Addicted.Config;

namespace Addic7ed.Model.Addicted.Proxy
{
    public class SearchRequest
    {
        public Addic7edCreds Credentials { get; set; }
        public string Show { get; set; }
        public int Episode { get; set; }
        public int Season { get; set; }
        public string FileName { get; set; }

        public string LanguageISO { get; set; }
    }

    public class SearchResponse
    {
        public Subtitle[] MatchingSubtitles { get; set; }

        public Episode Episode { get; set; }
    }
}