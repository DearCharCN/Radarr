using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Profiles.AudioLanguageMappings
{
    public interface IAudioLanguageMappingService
    {
        AudioLanguageMapping Add(AudioLanguageMapping mapping);
        List<AudioLanguageMapping> All();
        void Delete(int id);
        AudioLanguageMapping Get(int id);
        AudioLanguageMapping Update(AudioLanguageMapping mapping);
        List<ReleaseAudioInfo> TagAudioTracks(RemoteMovie subject);
        ReleaseAudioInfo TagAudioTrack(RemoteMovie subject, ReleaseAudioInfo audioInfo);
        bool HasLanguage(RemoteMovie subject, ReleaseAudioInfo audioInfo, Language language);
        bool HasTag(RemoteMovie subject, ReleaseAudioInfo audioInfo, string tag);
    }

    public class AudioLanguageMappingService : IAudioLanguageMappingService
    {
        private readonly IAudioLanguageMappingRepository _repo;

        public AudioLanguageMappingService(IAudioLanguageMappingRepository repo)
        {
            _repo = repo;
        }

        public AudioLanguageMapping Add(AudioLanguageMapping mapping)
        {
            return _repo.Insert(mapping);
        }

        public AudioLanguageMapping Update(AudioLanguageMapping mapping)
        {
            return _repo.Update(mapping);
        }

        public void Delete(int id)
        {
            _repo.Delete(id);
        }

        public AudioLanguageMapping Get(int id)
        {
            return _repo.Get(id);
        }

        public List<AudioLanguageMapping> All()
        {
            return _repo.All().ToList();
        }

        public List<ReleaseAudioInfo> TagAudioTracks(RemoteMovie subject)
        {
            return AudioLanguageMapper.TagAudioTracks(subject, subject?.Release?.AudioInfo, All());
        }

        public ReleaseAudioInfo TagAudioTrack(RemoteMovie subject, ReleaseAudioInfo audioInfo)
        {
            return AudioLanguageMapper.TagAudioTrack(subject, audioInfo, All());
        }

        public bool HasLanguage(RemoteMovie subject, ReleaseAudioInfo audioInfo, Language language)
        {
            return AudioLanguageMapper.HasLanguage(TagAudioTrack(subject, audioInfo), language);
        }

        public bool HasTag(RemoteMovie subject, ReleaseAudioInfo audioInfo, string tag)
        {
            return AudioLanguageMapper.HasTag(TagAudioTrack(subject, audioInfo), tag);
        }
    }

    public static class AudioLanguageMapper
    {
        public const string OriginTag = "Origin";

        private static readonly List<AudioLanguageMapping> BuiltInMappings = new ()
        {
            new AudioLanguageMapping
            {
                Language = Language.Chinese,
                Aliases = new List<string>
                {
                    "Chinese",
                    "Mandarin",
                    "Guoyu",
                    "Putonghua",
                    "Cantonese",
                    "ZH",
                    "ZHO",
                    "CHI",
                    "CMN",
                    "YUE",
                    "CHS",
                    "CHT",
                    "CN"
                }
            }
        };

        public static List<ReleaseAudioInfo> TagAudioTracks(RemoteMovie subject, IEnumerable<ReleaseAudioInfo> audioInfo, IEnumerable<AudioLanguageMapping> mappings = null)
        {
            var tagged = (audioInfo ?? new List<ReleaseAudioInfo>())
                .Select(audio => TagAudioTrack(subject, audio, mappings))
                .Where(audio => audio != null)
                .ToList();

            if (subject?.Release != null)
            {
                subject.Release.AudioInfo = tagged;
            }

            return tagged;
        }

        public static ReleaseAudioInfo TagAudioTrack(RemoteMovie subject, ReleaseAudioInfo audioInfo, IEnumerable<AudioLanguageMapping> mappings = null)
        {
            if (audioInfo == null)
            {
                return null;
            }

            var match = Match(audioInfo, mappings);
            var tags = new List<string>();

            audioInfo.MappedLanguage = match?.Language;
            audioInfo.MatchedLanguageAlias = match?.Alias;

            if (match?.Language != null && match.Language != Language.Unknown)
            {
                tags.Add(match.Language.Name);

                if (IsOriginalLanguage(subject, match.Language))
                {
                    tags.Add(OriginTag);
                }
            }

            audioInfo.LanguageTags = tags.Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();

            return audioInfo;
        }

        public static bool HasLanguage(ReleaseAudioInfo audioInfo, Language language)
        {
            if (audioInfo == null || language == null || language == Language.Unknown)
            {
                return false;
            }

            if (audioInfo.MappedLanguage == language)
            {
                return true;
            }

            return audioInfo.LanguageTags?.Any(tag => MatchesText(tag, language.Name)) == true;
        }

        public static bool HasTag(ReleaseAudioInfo audioInfo, string tag)
        {
            if (audioInfo == null || tag.IsNullOrWhiteSpace())
            {
                return false;
            }

            return audioInfo.LanguageTags?.Any(value => MatchesText(value, tag)) == true;
        }

        public static AudioLanguageMatch Match(ReleaseAudioInfo audioInfo, IEnumerable<AudioLanguageMapping> mappings = null)
        {
            var text = GetMatchText(audioInfo);

            if (text.IsNullOrWhiteSpace())
            {
                return null;
            }

            var userMatch = MatchMappings(text, mappings);

            if (userMatch != null)
            {
                return userMatch;
            }

            var builtInMatch = MatchMappings(text, BuiltInMappings);

            if (builtInMatch != null)
            {
                return builtInMatch;
            }

            return MatchKnownLanguage(text);
        }

        private static AudioLanguageMatch MatchMappings(string text, IEnumerable<AudioLanguageMapping> mappings)
        {
            foreach (var mapping in mappings?.Where(mapping => mapping.Enabled && mapping.Language != null) ?? new List<AudioLanguageMapping>())
            {
                foreach (var alias in mapping.Aliases ?? new List<string>())
                {
                    if (MatchesText(text, alias))
                    {
                        return new AudioLanguageMatch
                        {
                            Language = mapping.Language,
                            Alias = alias
                        };
                    }
                }
            }

            return null;
        }

        private static AudioLanguageMatch MatchKnownLanguage(string text)
        {
            foreach (var language in Language.All.Where(language => language.Id > 0))
            {
                var aliases = GetLanguageAliases(language);
                var alias = aliases.FirstOrDefault(value => MatchesText(text, value));

                if (alias.IsNotNullOrWhiteSpace())
                {
                    return new AudioLanguageMatch
                    {
                        Language = language,
                        Alias = alias
                    };
                }
            }

            return null;
        }

        private static List<string> GetLanguageAliases(Language language)
        {
            var aliases = new List<string> { language.Name };
            var isoLanguage = IsoLanguages.Get(language);

            if (isoLanguage != null)
            {
                aliases.Add(isoLanguage.EnglishName);
                aliases.Add(isoLanguage.TwoLetterCode);
                aliases.Add(isoLanguage.ThreeLetterCode);
            }

            return aliases.Where(alias => alias.IsNotNullOrWhiteSpace()).Distinct(System.StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool IsOriginalLanguage(RemoteMovie subject, Language language)
        {
            var originalLanguage = subject?.Movie?.MovieMetadata?.Value?.OriginalLanguage ?? Language.Unknown;

            return originalLanguage != Language.Unknown && originalLanguage == language;
        }

        private static string GetMatchText(ReleaseAudioInfo audioInfo)
        {
            return string.Join(" ", new[] { audioInfo?.Language, audioInfo?.Specification }.Where(value => value.IsNotNullOrWhiteSpace()));
        }

        private static bool MatchesText(string text, string alias)
        {
            if (text.IsNullOrWhiteSpace() || alias.IsNullOrWhiteSpace())
            {
                return false;
            }

            var normalizedText = Normalize(text);
            var normalizedAlias = Normalize(alias);

            if (normalizedAlias.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (normalizedAlias.Length <= 3)
            {
                return GetNormalizedTokens(text).Contains(normalizedAlias);
            }

            return text.Contains(alias, System.StringComparison.OrdinalIgnoreCase) ||
                   normalizedText.Contains(normalizedAlias);
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
        }

        private static List<string> GetNormalizedTokens(string value)
        {
            var tokens = new List<string>();
            var current = new List<char>();

            foreach (var character in value ?? string.Empty)
            {
                if (char.IsLetterOrDigit(character))
                {
                    current.Add(char.ToLowerInvariant(character));
                    continue;
                }

                if (current.Any())
                {
                    tokens.Add(new string(current.ToArray()));
                    current.Clear();
                }
            }

            if (current.Any())
            {
                tokens.Add(new string(current.ToArray()));
            }

            return tokens;
        }
    }

    public class AudioLanguageMatch
    {
        public Language Language { get; set; }
        public string Alias { get; set; }
    }
}
