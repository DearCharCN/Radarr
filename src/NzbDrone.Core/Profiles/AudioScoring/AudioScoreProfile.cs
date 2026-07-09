using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Profiles.AudioScoring
{
    public class AudioScoreProfile : ModelBase
    {
        public AudioScoreProfile()
        {
            Enabled = true;
            Rules = new List<AudioScoreRule>();
            MutexGroups = new List<AudioScoreMutexGroup>();
        }

        public string Name { get; set; }
        public bool Enabled { get; set; }
        public List<AudioScoreRule> Rules { get; set; }
        public List<AudioScoreMutexGroup> MutexGroups { get; set; }
    }

    public class AudioScoreRule : IEmbeddedDocument
    {
        public string Name { get; set; }
        public string MatchType { get; set; }
        public string Pattern { get; set; }
        public int Score { get; set; }
        public string MutexGroup { get; set; }
        public bool Enabled { get; set; }
    }

    public class AudioScoreMutexGroup : IEmbeddedDocument
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
    }
}
