using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Profiles.AudioLanguageMappings
{
    public class AudioLanguageMapping : ModelBase
    {
        public AudioLanguageMapping()
        {
            Aliases = new List<string>();
            Enabled = true;
        }

        public Language Language { get; set; }
        public List<string> Aliases { get; set; }
        public bool Enabled { get; set; }
    }
}
