using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Profiles.AudioPreferences
{
    public class AudioLanguagePreference : ModelBase
    {
        public AudioLanguagePreference()
        {
            Enabled = true;
            Entries = new List<AudioLanguagePreferenceItem>();
        }

        public string Name { get; set; }
        public bool Enabled { get; set; }
        public int ScoreGapThreshold { get; set; }
        public int? AudioScoreProfileId { get; set; }
        public List<AudioLanguagePreferenceItem> Entries { get; set; }
    }

    public class AudioLanguagePreferenceItem : IEmbeddedDocument
    {
        public string LanguageTag { get; set; }
        public bool Enabled { get; set; }
    }
}
