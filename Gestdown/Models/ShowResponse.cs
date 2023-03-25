#nullable enable
using System.Collections.Generic;

namespace Gestdown.Models
{
    public class ShowResponse
    {
        public class Show
        {
            public string id { get; set; }
            public string name { get; set; }
            public int nbSeasons { get; set; }
            public List<int> seasons { get; set; }
            public int tvDbId { get; set; }
        }

        public List<Show> shows { get; set; }
    }
}