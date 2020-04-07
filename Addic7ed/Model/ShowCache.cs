using System;
using System.Collections.Generic;

namespace Addic7ed.Model
{
    internal class ShowCache
    {
        private static readonly Random Random = new Random();
        public string ShowId { get; }
        public bool IsExpired => (DateTime.UtcNow - _created) >= _limit;

        private readonly DateTime _created;
        public Dictionary<int, IEnumerable<Addic7edResult>> Seasons { get; } = new Dictionary<int, IEnumerable<Addic7edResult>>();
        private readonly TimeSpan _limit;


        public ShowCache(string showId)
        {
            ShowId = showId;
            _created = DateTime.UtcNow;
            _limit = TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(Random.Next(120));
        }
        
    }
}