using System.Collections.Generic;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Parser.Model
{
    public class ReleaseAudioInfo
    {
        public ReleaseAudioInfo()
        {
            LanguageTags = new List<string>();
        }

        public string Language { get; set; }
        public string Specification { get; set; }
        public Language MappedLanguage { get; set; }
        public List<string> LanguageTags { get; set; }
        public string MatchedLanguageAlias { get; set; }
    }
}
