using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.AudioLanguageMappings;
using NzbDrone.Core.Profiles.AudioPreferences;

namespace NzbDrone.Core.Profiles.ReleaseFilters
{
    public interface IReleaseFilterEvaluator
    {
        ReleaseFilterEvaluationResult Evaluate(RemoteMovie subject, ReleaseFilterProfile profile);
        bool RequiresMediaInfo(ReleaseFilterProfile profile);
    }

    public class ReleaseFilterEvaluator : IReleaseFilterEvaluator
    {
        private static readonly Regex NonAlphaNumericRegex = new (@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly IAudioLanguageMappingService _audioLanguageMappingService;
        private readonly IAudioLanguagePreferenceService _audioLanguagePreferenceService;

        public ReleaseFilterEvaluator(IAudioLanguageMappingService audioLanguageMappingService = null,
                                      IAudioLanguagePreferenceService audioLanguagePreferenceService = null)
        {
            _audioLanguageMappingService = audioLanguageMappingService;
            _audioLanguagePreferenceService = audioLanguagePreferenceService;
        }

        public ReleaseFilterEvaluationResult Evaluate(RemoteMovie subject, ReleaseFilterProfile profile)
        {
            if (profile == null || !profile.Enabled || profile.Filter.IsNullOrWhiteSpace())
            {
                return ReleaseFilterEvaluationResult.Accept();
            }

            if (!STJson.TryDeserialize<ReleaseFilterNode>(profile.Filter, out var root))
            {
                return ReleaseFilterEvaluationResult.Reject($"release filter '{profile.Name}' could not be parsed");
            }

            var result = EvaluateNode(subject, root);

            if (result.Accepted)
            {
                return result;
            }

            return ReleaseFilterEvaluationResult.Reject($"release filter '{profile.Name}' rejected release: {result.Reason}");
        }

        public bool RequiresMediaInfo(ReleaseFilterProfile profile)
        {
            if (profile == null || profile.Filter.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (!STJson.TryDeserialize<ReleaseFilterNode>(profile.Filter, out var root))
            {
                return false;
            }

            return RequiresMediaInfo(root);
        }

        private ReleaseFilterEvaluationResult EvaluateNode(RemoteMovie subject, ReleaseFilterNode node)
        {
            if (node == null)
            {
                return ReleaseFilterEvaluationResult.Accept();
            }

            if (IsGroup(node))
            {
                return EvaluateGroup(subject, node);
            }

            return EvaluateCondition(subject, node);
        }

        private ReleaseFilterEvaluationResult EvaluateGroup(RemoteMovie subject, ReleaseFilterNode node)
        {
            var children = node.Children ?? new List<ReleaseFilterNode>();

            if (!children.Any())
            {
                return ReleaseFilterEvaluationResult.Accept();
            }

            var mode = NormalizeKey(node.Mode);

            if (mode == "or" || mode == "any")
            {
                var reasons = new List<string>();

                foreach (var child in children)
                {
                    var result = EvaluateNode(subject, child);

                    if (result.Accepted)
                    {
                        return result;
                    }

                    reasons.Add(result.Reason);
                }

                return ReleaseFilterEvaluationResult.Reject(reasons.Where(x => x.IsNotNullOrWhiteSpace()).FirstOrDefault() ?? "no OR condition matched");
            }

            foreach (var child in children)
            {
                var result = EvaluateNode(subject, child);

                if (!result.Accepted)
                {
                    return result;
                }
            }

            return ReleaseFilterEvaluationResult.Accept();
        }

        private ReleaseFilterEvaluationResult EvaluateCondition(RemoteMovie subject, ReleaseFilterNode node)
        {
            if (node.Field.IsNullOrWhiteSpace())
            {
                return ReleaseFilterEvaluationResult.Reject("condition has no field");
            }

            var value = GetFieldValue(subject, node.Field);

            if (!value.Exists)
            {
                return ReleaseFilterEvaluationResult.Reject($"{node.Field} is unavailable");
            }

            var result = EvaluateValue(value, node.Operator, node.Value);

            return result ?
                ReleaseFilterEvaluationResult.Accept() :
                ReleaseFilterEvaluationResult.Reject($"{node.Field} did not match {node.Operator}");
        }

        private ReleaseFilterValue GetFieldValue(RemoteMovie subject, string field)
        {
            var release = subject?.Release;
            var parsed = subject?.ParsedMovieInfo;
            var torrent = release as TorrentInfo;

            switch (NormalizeKey(field))
            {
                case "title":
                case "releasetitle":
                    return ReleaseFilterValue.FromString(release?.Title);
                case "indexer":
                    return ReleaseFilterValue.FromString(release?.Indexer);
                case "protocol":
                    return ReleaseFilterValue.FromString(release?.DownloadProtocol.ToString());
                case "quality":
                case "qualityname":
                    return ReleaseFilterValue.FromStrings(parsed?.Quality?.Quality == null ? null : new[]
                    {
                        parsed.Quality.Quality.Name,
                        parsed.Quality.Quality.Id.ToString(CultureInfo.InvariantCulture)
                    });
                case "qualityid":
                    return ReleaseFilterValue.FromNumber(parsed?.Quality?.Quality?.Id);
                case "customformats":
                case "customformat":
                    return ReleaseFilterValue.FromStrings(subject?.CustomFormats?.Select(x => x.Name));
                case "customformatscore":
                    return ReleaseFilterValue.FromNumber(subject?.CustomFormatScore);
                case "size":
                    return ReleaseFilterValue.FromNumber(release?.Size);
                case "age":
                case "agedays":
                    return ReleaseFilterValue.FromNumber(release?.Age);
                case "agehours":
                    return ReleaseFilterValue.FromNumber(release?.AgeHours);
                case "ageminutes":
                    return ReleaseFilterValue.FromNumber(release?.AgeMinutes);
                case "seeders":
                    return ReleaseFilterValue.FromNumber(TorrentInfo.GetSeeders(release));
                case "peers":
                    return ReleaseFilterValue.FromNumber(TorrentInfo.GetPeers(release));
                case "leechers":
                    if (torrent?.Peers.HasValue == true && torrent.Seeders.HasValue)
                    {
                        return ReleaseFilterValue.FromNumber(torrent.Peers.Value - torrent.Seeders.Value);
                    }

                    return ReleaseFilterValue.Missing();
                case "indexerflags":
                case "flags":
                    return ReleaseFilterValue.FromStrings(torrent?.IndexerFlags.ToString()
                        .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => x != "0"));
                case "releasegroup":
                case "group":
                    return ReleaseFilterValue.FromString(parsed?.ReleaseGroup);
                case "languages":
                case "language":
                    return ReleaseFilterValue.FromStrings(subject?.Languages?.Select(x => x.Name));
                case "audiolanguages":
                case "audiolanguage":
                    return ReleaseFilterValue.FromStrings(GetAudioLanguageValues(subject));
                case "audiolanguagetags":
                case "audiolanguagetag":
                case "audiotags":
                case "audiotag":
                    return ReleaseFilterValue.FromStrings(GetAudioLanguageTags(subject));
                case "audioinfo":
                case "audio":
                    return ReleaseFilterValue.FromStrings(TagAudioTracks(subject).Select(FormatAudioInfo));
                case "audiospecifications":
                case "audiospecification":
                case "audiospec":
                    return ReleaseFilterValue.FromStrings(TagAudioTracks(subject).Select(x => x.Specification));
                case "subtitlelanguages":
                case "subtitlelanguage":
                case "subtitles":
                case "subs":
                    return ReleaseFilterValue.FromStrings(release?.Subs);
                case "selectedaudio":
                case "preferredaudio":
                    return ReleaseFilterValue.FromString(FormatAudioInfo(GetAudioPreference(subject).SelectedAudio));
                case "selectedaudiolanguage":
                case "preferredaudiolanguage":
                    return ReleaseFilterValue.FromString(GetDisplayLanguage(GetAudioPreference(subject).SelectedAudio));
                case "selectedaudiospecification":
                case "selectedaudiospec":
                case "preferredaudiospecification":
                case "preferredaudiospec":
                    return ReleaseFilterValue.FromString(GetAudioPreference(subject).SelectedAudio?.Specification);
                case "selectedaudiotags":
                case "selectedaudiotag":
                case "preferredaudiotags":
                case "preferredaudiotag":
                    return ReleaseFilterValue.FromStrings(GetAudioPreference(subject).SelectedAudio?.LanguageTags);
                case "audioscore":
                case "audiopreferencescore":
                    return ReleaseFilterValue.FromNumber(GetAudioPreference(subject).AudioScore);
                case "haschineseaudioorsubtitle":
                case "chineseaccessible":
                    return ReleaseFilterValue.FromBool(GetAudioPreference(subject).HasChineseAudioOrSubtitle);
                case "hasoriginaudio":
                case "hasoriginalaudio":
                    return ReleaseFilterValue.FromBool(TagAudioTracks(subject).Any(audio => AudioLanguageMapper.HasTag(audio, AudioLanguageMapper.OriginTag)));
                case "mediainfostatus":
                case "additionaldatastatus":
                    return ReleaseFilterValue.FromString(release?.MediaInfoStatus ?? release?.MediaInfoProgressStatus);
                default:
                    return ReleaseFilterValue.Missing();
            }
        }

        private bool RequiresMediaInfo(ReleaseFilterNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (IsGroup(node))
            {
                return node.Children?.Any(RequiresMediaInfo) == true;
            }

            return IsMediaInfoField(node.Field);
        }

        private static bool IsMediaInfoField(string field)
        {
            switch (NormalizeKey(field))
            {
                case "audiolanguages":
                case "audiolanguage":
                case "audiolanguagetags":
                case "audiolanguagetag":
                case "audiotags":
                case "audiotag":
                case "audioinfo":
                case "audio":
                case "audiospecifications":
                case "audiospecification":
                case "audiospec":
                case "subtitlelanguages":
                case "subtitlelanguage":
                case "subtitles":
                case "subs":
                case "selectedaudio":
                case "preferredaudio":
                case "selectedaudiolanguage":
                case "preferredaudiolanguage":
                case "selectedaudiospecification":
                case "selectedaudiospec":
                case "preferredaudiospecification":
                case "preferredaudiospec":
                case "selectedaudiotags":
                case "selectedaudiotag":
                case "preferredaudiotags":
                case "preferredaudiotag":
                case "audioscore":
                case "audiopreferencescore":
                case "haschineseaudioorsubtitle":
                case "chineseaccessible":
                case "hasoriginaudio":
                case "hasoriginalaudio":
                    return true;
                default:
                    return false;
            }
        }

        private List<ReleaseAudioInfo> TagAudioTracks(RemoteMovie subject)
        {
            return _audioLanguageMappingService?.TagAudioTracks(subject) ??
                   AudioLanguageMapper.TagAudioTracks(subject, subject?.Release?.AudioInfo);
        }

        private IEnumerable<string> GetAudioLanguageValues(RemoteMovie subject)
        {
            return TagAudioTracks(subject)
                .SelectMany(audio => new[] { audio.Language, audio.MappedLanguage?.Name }
                    .Concat(audio.LanguageTags ?? new List<string>()))
                .Where(value => value.IsNotNullOrWhiteSpace())
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private IEnumerable<string> GetAudioLanguageTags(RemoteMovie subject)
        {
            return TagAudioTracks(subject)
                .SelectMany(audio => audio.LanguageTags ?? new List<string>())
                .Where(value => value.IsNotNullOrWhiteSpace())
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private AudioPreferenceResult GetAudioPreference(RemoteMovie subject)
        {
            return _audioLanguagePreferenceService?.Evaluate(subject) ??
                   ToAudioPreferenceResult(ChineseMediaPreferenceEvaluator.Evaluate(subject, _audioLanguageMappingService));
        }

        private static AudioPreferenceResult ToAudioPreferenceResult(ChineseMediaPreferenceResult result)
        {
            return new AudioPreferenceResult
            {
                SelectedAudio = result.SelectedAudio,
                AudioScore = result.AudioPreferenceScore,
                AudioScoreBreakdown = result.SelectedAudio == null ? new List<string>() : new List<string> { $"Legacy Chinese preference: {result.AudioPreferenceScore}" },
                AudioLanguagePreferenceName = "Legacy Chinese Preference",
                SelectedPreferenceTag = result.SelectedAudio?.MappedLanguage?.Name ?? result.SelectedAudio?.Language,
                HasChineseAudio = result.HasChineseAudio,
                HasChineseSubtitle = result.HasChineseSubtitle,
                IsMediaInfoPending = result.IsMediaInfoPending
            };
        }

        private static string GetDisplayLanguage(ReleaseAudioInfo audioInfo)
        {
            return audioInfo?.MappedLanguage?.Name ?? audioInfo?.Language;
        }

        private static string FormatAudioInfo(ReleaseAudioInfo audioInfo)
        {
            if (audioInfo == null)
            {
                return null;
            }

            if (audioInfo.Language.IsNotNullOrWhiteSpace() && audioInfo.Specification.IsNotNullOrWhiteSpace())
            {
                return $"{audioInfo.Language}: {audioInfo.Specification}";
            }

            return audioInfo.Language.IsNotNullOrWhiteSpace() ? audioInfo.Language : audioInfo.Specification;
        }

        private bool EvaluateValue(ReleaseFilterValue actual, string op, JsonElement expected)
        {
            switch (NormalizeKey(op))
            {
                case "":
                case "equal":
                case "equals":
                case "is":
                case "in":
                    return MatchesEqual(actual, expected);
                case "notequal":
                case "notequals":
                case "isnot":
                case "notin":
                    return !MatchesEqual(actual, expected);
                case "contains":
                case "has":
                    return MatchesContains(actual, expected);
                case "notcontains":
                case "doesnotcontain":
                case "nothas":
                    return !MatchesContains(actual, expected);
                case "exists":
                case "present":
                    return actual.Exists && actual.HasAnyValue;
                case "notexists":
                case "missing":
                    return !actual.Exists || !actual.HasAnyValue;
                case "greaterthan":
                case "gt":
                    return CompareNumber(actual, expected, comparison => comparison > 0);
                case "greaterthanorequal":
                case "greaterthanorequals":
                case "gte":
                    return CompareNumber(actual, expected, comparison => comparison >= 0);
                case "lessthan":
                case "lt":
                    return CompareNumber(actual, expected, comparison => comparison < 0);
                case "lessthanorequal":
                case "lessthanorequals":
                case "lte":
                    return CompareNumber(actual, expected, comparison => comparison <= 0);
                default:
                    return false;
            }
        }

        private bool MatchesEqual(ReleaseFilterValue actual, JsonElement expected)
        {
            if (actual.Number.HasValue && TryGetExpectedNumber(expected, out var expectedNumber))
            {
                return Math.Abs(actual.Number.Value - expectedNumber) < 0.0001;
            }

            if (actual.Bool.HasValue && TryGetExpectedBool(expected, out var expectedBool))
            {
                return actual.Bool.Value == expectedBool;
            }

            var expectedStrings = GetExpectedStrings(expected).Select(NormalizeText).ToList();

            if (!expectedStrings.Any())
            {
                return false;
            }

            return actual.Strings.Select(NormalizeText).Any(expectedStrings.Contains);
        }

        private bool MatchesContains(ReleaseFilterValue actual, JsonElement expected)
        {
            var expectedStrings = GetExpectedStrings(expected).Select(NormalizeText).ToList();

            if (!expectedStrings.Any())
            {
                return false;
            }

            return actual.Strings
                .Select(NormalizeText)
                .Any(actualString => expectedStrings.Any(actualString.Contains));
        }

        private bool CompareNumber(ReleaseFilterValue actual, JsonElement expected, Func<int, bool> predicate)
        {
            if (!actual.Number.HasValue || !TryGetExpectedNumber(expected, out var expectedNumber))
            {
                return false;
            }

            return predicate(actual.Number.Value.CompareTo(expectedNumber));
        }

        private bool TryGetExpectedNumber(JsonElement value, out double number)
        {
            number = 0;

            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    return value.TryGetDouble(out number);
                case JsonValueKind.String:
                    return double.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number);
                default:
                    return false;
            }
        }

        private bool TryGetExpectedBool(JsonElement value, out bool boolValue)
        {
            boolValue = false;

            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                    boolValue = true;
                    return true;
                case JsonValueKind.False:
                    boolValue = false;
                    return true;
                case JsonValueKind.String:
                    return bool.TryParse(value.GetString(), out boolValue);
                default:
                    return false;
            }
        }

        private List<string> GetExpectedStrings(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                    return value.EnumerateArray().SelectMany(GetExpectedStrings).ToList();
                case JsonValueKind.String:
                    return new List<string> { value.GetString() };
                case JsonValueKind.Number:
                    return new List<string> { value.ToString() };
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return new List<string> { value.GetBoolean().ToString() };
                default:
                    return new List<string>();
            }
        }

        private static bool IsGroup(ReleaseFilterNode node)
        {
            return NormalizeKey(node.Type) == "group" || node.Children?.Any() == true;
        }

        private static string NormalizeKey(string value)
        {
            return NonAlphaNumericRegex.Replace(value ?? string.Empty, string.Empty).ToLowerInvariant();
        }

        private static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }

    public class ReleaseFilterEvaluationResult
    {
        public bool Accepted { get; set; }
        public string Reason { get; set; }

        public static ReleaseFilterEvaluationResult Accept()
        {
            return new ReleaseFilterEvaluationResult { Accepted = true };
        }

        public static ReleaseFilterEvaluationResult Reject(string reason)
        {
            return new ReleaseFilterEvaluationResult
            {
                Accepted = false,
                Reason = reason
            };
        }
    }

    public class ReleaseFilterValue
    {
        public bool Exists { get; set; }
        public List<string> Strings { get; set; }
        public double? Number { get; set; }
        public bool? Bool { get; set; }

        public bool HasAnyValue => Number.HasValue || Bool.HasValue || Strings.Any(x => x.IsNotNullOrWhiteSpace());

        public static ReleaseFilterValue Missing()
        {
            return new ReleaseFilterValue
            {
                Exists = false,
                Strings = new List<string>()
            };
        }

        public static ReleaseFilterValue FromString(string value)
        {
            return new ReleaseFilterValue
            {
                Exists = value.IsNotNullOrWhiteSpace(),
                Strings = value.IsNotNullOrWhiteSpace() ? new List<string> { value } : new List<string>()
            };
        }

        public static ReleaseFilterValue FromStrings(IEnumerable<string> values)
        {
            var strings = values?.Where(x => x.IsNotNullOrWhiteSpace()).ToList() ?? new List<string>();

            return new ReleaseFilterValue
            {
                Exists = strings.Any(),
                Strings = strings
            };
        }

        public static ReleaseFilterValue FromNumber(double? value)
        {
            return new ReleaseFilterValue
            {
                Exists = value.HasValue,
                Number = value,
                Strings = value.HasValue ? new List<string> { value.Value.ToString(CultureInfo.InvariantCulture) } : new List<string>()
            };
        }

        public static ReleaseFilterValue FromBool(bool? value)
        {
            return new ReleaseFilterValue
            {
                Exists = value.HasValue,
                Bool = value,
                Strings = value.HasValue ? new List<string> { value.Value.ToString() } : new List<string>()
            };
        }
    }
}
