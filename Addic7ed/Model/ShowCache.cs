using System;
using System.Collections.Generic;

namespace Addic7ed.Model
{
    public class ShowCache
    {
        private static readonly Random Random = new Random();
        public string ShowId { get; }
        public bool IsExpired => (DateTime.UtcNow - _created) >= _limit;

        private readonly DateTime _created;
        public Dictionary<int, int> SeasonEpisodeCount { get; } = new Dictionary<int, int>();
        private readonly TimeSpan _limit;


        public ShowCache(string showId)
        {
            ShowId = showId;
            _created = DateTime.UtcNow;
            _limit = TimeSpan.FromDays(7) + TimeSpan.FromMinutes(Random.Next(360));
        }
        
    }
}