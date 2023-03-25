#nullable enable
using System.Collections.Generic;

namespace Gestdown.Models
{
    public class ShowResponse
    {
        public class Show
        {
            public string id { get; set; } = null!;
            public string name { get; set; } = null!;
            public int nbSeasons { get; set; }
            public List<int> seasons { get; set; } = null!;
            public int tvDbId { get; set; }
        }

        public List<Show> shows { get; set; } = null!;
    }
}