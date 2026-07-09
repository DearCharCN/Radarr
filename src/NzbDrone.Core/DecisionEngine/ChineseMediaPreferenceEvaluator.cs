using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine
{
    public static class ChineseMediaPreferenceEvaluator
    {
        private const int LosslessScore = 30;
        private const int LossyScore = 10;
        private const int AtmosScore = 40;
        private const int Surround51Score = 20;
        private const int Surround71Score = 30;

        private static readonly Regex ChannelRegex = new (@"(?<!\d)(?<channels>[257])\.1(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NonAlphaNumericRegex = new (@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ChineseMediaPreferenceResult Evaluate(RemoteMovie subject)
        {
            var release = subject?.Release;
            var audioInfo = release?.AudioInfo ?? new List<ReleaseAudioInfo>();
            var originalLanguage = subject?.Movie?.MovieMetadata?.Value?.OriginalLanguage ?? Language.Unknown;
            var hasChineseSubtitle = release?.Subs?.Any(IsChineseText) == true;
            var hasChineseAudio = audioInfo.Any(x => IsChineseText(x.Language)) ||
                                  subject?.Languages?.Contains(Language.Chinese) == true;

            var chineseAudio = BestAudioForLanguage(audioInfo, Language.Chinese);

            if (chineseAudio == null && hasChineseAudio)
            {
                chineseAudio = new ReleaseAudioInfo { Language = Language.Chinese.Name };
            }

            var selectedAudio = SelectAudio(audioInfo, chineseAudio, hasChineseSubtitle, originalLanguage);

            return new ChineseMediaPreferenceResult
            {
                HasChineseAudio = hasChineseAudio,
                HasChineseSubtitle = hasChineseSubtitle,
                IsMediaInfoPending = IsMediaInfoPending(release),
                SelectedAudio = selectedAudio,
                AudioPreferenceScore = Score(selectedAudio)
            };
        }

        public static int CompareAudioSpecification(ReleaseAudioInfo left, ReleaseAudioInfo right)
        {
            return AudioSpecificationRank(left).CompareTo(AudioSpecificationRank(right));
        }

        private static ReleaseAudioInfo SelectAudio(List<ReleaseAudioInfo> audioInfo, ReleaseAudioInfo chineseAudio, bool hasChineseSubtitle, Language originalLanguage)
        {
            var originalAudio = BestAudioForLanguage(audioInfo, originalLanguage);

            if (chineseAudio == null)
            {
                return originalAudio ?? BestAudio(audioInfo);
            }

            if (originalLanguage == Language.Chinese)
            {
                return chineseAudio;
            }

            if (MeetsChineseDubPreferredMinimum(chineseAudio))
            {
                return chineseAudio;
            }

            if (!hasChineseSubtitle || originalAudio == null)
            {
                return chineseAudio;
            }

            var originalMeetsMinimum = MeetsChineseDubPreferredMinimum(originalAudio);
            var comparison = CompareAudioSpecification(originalAudio, chineseAudio);

            if (!originalMeetsMinimum && !MeetsChineseDubPreferredMinimum(chineseAudio))
            {
                return comparison == 0 ? chineseAudio : originalAudio;
            }

            return comparison > 0 ? originalAudio : chineseAudio;
        }

        private static ReleaseAudioInfo BestAudioForLanguage(List<ReleaseAudioInfo> audioInfo, Language language)
        {
            if (language == null || language == Language.Unknown)
            {
                return null;
            }

            return audioInfo
                .Where(x => IsLanguage(x.Language, language))
                .OrderByDescending(AudioSpecificationRank)
                .FirstOrDefault();
        }

        private static ReleaseAudioInfo BestAudio(List<ReleaseAudioInfo> audioInfo)
        {
            return audioInfo
                .OrderByDescending(AudioSpecificationRank)
                .FirstOrDefault();
        }

        private static int Score(ReleaseAudioInfo audioInfo)
        {
            if (audioInfo == null)
            {
                return 0;
            }

            var features = ParseAudioFeatures(audioInfo.Specification);
            var score = 0;

            if (features.HasAtmos)
            {
                score += AtmosScore;
            }

            if (features.ChannelCount >= 8)
            {
                score += Surround71Score;
            }
            else if (features.ChannelCount >= 6)
            {
                score += Surround51Score;
            }

            if (features.CodecTier == AudioCodecTier.Lossless)
            {
                score += LosslessScore;
            }
            else if (features.CodecTier is AudioCodecTier.Ddp or AudioCodecTier.Dd)
            {
                score += LossyScore;
            }

            return score;
        }

        private static bool MeetsChineseDubPreferredMinimum(ReleaseAudioInfo audioInfo)
        {
            var features = ParseAudioFeatures(audioInfo?.Specification);

            return features.CodecTier >= AudioCodecTier.Ddp && features.ChannelCount >= 6;
        }

        private static int AudioSpecificationRank(ReleaseAudioInfo audioInfo)
        {
            var features = ParseAudioFeatures(audioInfo?.Specification);

            return ((int)features.CodecTier * 1000) +
                   (features.HasAtmos ? 100 : 0) +
                   features.ChannelCount;
        }

        private static AudioFeatures ParseAudioFeatures(string specification)
        {
            var normalized = Normalize(specification);
            var channelCount = ParseChannelCount(specification);
            var codecTier = AudioCodecTier.Unknown;

            if (ContainsAny(normalized, "truehd", "dtshdma", "dtsma", "flac", "lpcm", "pcm", "mlp"))
            {
                codecTier = AudioCodecTier.Lossless;
            }
            else if (ContainsAny(normalized, "ddp", "eac3", "eac", "dolbydigitalplus"))
            {
                codecTier = AudioCodecTier.Ddp;
            }
            else if (ContainsAny(normalized, "ac3", "dd", "dolbydigital", "dts"))
            {
                codecTier = AudioCodecTier.Dd;
            }
            else if (ContainsAny(normalized, "aac", "opus", "mp3"))
            {
                codecTier = AudioCodecTier.OtherLossy;
            }

            return new AudioFeatures
            {
                CodecTier = codecTier,
                ChannelCount = channelCount,
                HasAtmos = normalized.Contains("atmos")
            };
        }

        private static int ParseChannelCount(string specification)
        {
            if (specification == null)
            {
                return 0;
            }

            var match = ChannelRegex.Match(specification);

            if (match.Success && int.TryParse(match.Groups["channels"].Value, out var channels))
            {
                return channels + 1;
            }

            var normalized = Normalize(specification);

            if (ContainsAny(normalized, "8ch", "8channels", "eightchannels"))
            {
                return 8;
            }

            if (ContainsAny(normalized, "6ch", "6channels", "sixchannels"))
            {
                return 6;
            }

            if (ContainsAny(normalized, "stereo", "2ch", "2channels"))
            {
                return 2;
            }

            return 0;
        }

        private static bool IsLanguage(string value, Language language)
        {
            if (language == Language.Chinese)
            {
                return IsChineseText(value);
            }

            if (value == null)
            {
                return false;
            }

            var normalized = Normalize(value);
            var isoLanguage = IsoLanguages.Get(language);

            return Normalize(language.Name) == normalized ||
                   Normalize(isoLanguage?.EnglishName) == normalized ||
                   Normalize(isoLanguage?.TwoLetterCode) == normalized ||
                   Normalize(isoLanguage?.ThreeLetterCode) == normalized;
        }

        private static bool IsChineseText(string value)
        {
            if (value == null)
            {
                return false;
            }

            var normalized = Normalize(value);

            return ContainsAny(normalized,
                "chinese",
                "mandarin",
                "cantonese",
                "zh",
                "zho",
                "chi",
                "cmn",
                "yue",
                "chs",
                "cht",
                "zhcn",
                "zhtw",
                "cn") ||
                value.Contains('\u4e2d') ||
                value.Contains('\u56fd') ||
                value.Contains('\u570b') ||
                value.Contains('\u7ca4') ||
                value.Contains('\u7cb5') ||
                value.Contains('\u7b80') ||
                value.Contains('\u7c21') ||
                value.Contains('\u7e41');
        }

        private static bool IsMediaInfoPending(ReleaseInfo release)
        {
            return release?.MediaInfoStatus?.Equals("pending", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            return needles.Any(value.Contains);
        }

        private static string Normalize(string value)
        {
            return NonAlphaNumericRegex.Replace(value ?? string.Empty, string.Empty).ToLowerInvariant();
        }

        private enum AudioCodecTier
        {
            Unknown = 0,
            OtherLossy = 1,
            Dd = 2,
            Ddp = 3,
            Lossless = 4
        }

        private class AudioFeatures
        {
            public AudioCodecTier CodecTier { get; set; }
            public int ChannelCount { get; set; }
            public bool HasAtmos { get; set; }
        }
    }

    public class ChineseMediaPreferenceResult
    {
        public bool HasChineseAudio { get; set; }
        public bool HasChineseSubtitle { get; set; }
        public bool IsMediaInfoPending { get; set; }
        public ReleaseAudioInfo SelectedAudio { get; set; }
        public int AudioPreferenceScore { get; set; }

        public bool HasChineseAudioOrSubtitle => HasChineseAudio || HasChineseSubtitle;
    }
}
