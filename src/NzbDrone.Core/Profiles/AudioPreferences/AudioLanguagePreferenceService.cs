using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.AudioLanguageMappings;
using NzbDrone.Core.Profiles.AudioScoring;

namespace NzbDrone.Core.Profiles.AudioPreferences
{
    public interface IAudioLanguagePreferenceService
    {
        AudioLanguagePreference Add(AudioLanguagePreference preference);
        List<AudioLanguagePreference> All();
        void Delete(int id);
        bool Exists(int id);
        AudioLanguagePreference Get(int id);
        AudioLanguagePreference Update(AudioLanguagePreference preference);
        AudioPreferenceResult Evaluate(RemoteMovie subject);
    }

    public class AudioLanguagePreferenceService : IAudioLanguagePreferenceService
    {
        private readonly IAudioLanguagePreferenceRepository _repo;
        private readonly IAudioLanguageMappingService _audioLanguageMappingService;
        private readonly IAudioScoreProfileService _audioScoreProfileService;

        public AudioLanguagePreferenceService(IAudioLanguagePreferenceRepository repo,
                                              IAudioLanguageMappingService audioLanguageMappingService,
                                              IAudioScoreProfileService audioScoreProfileService)
        {
            _repo = repo;
            _audioLanguageMappingService = audioLanguageMappingService;
            _audioScoreProfileService = audioScoreProfileService;
        }

        public AudioLanguagePreference Add(AudioLanguagePreference preference)
        {
            return _repo.Insert(preference);
        }

        public AudioLanguagePreference Update(AudioLanguagePreference preference)
        {
            return _repo.Update(preference);
        }

        public void Delete(int id)
        {
            _repo.Delete(id);
        }

        public bool Exists(int id)
        {
            return _repo.Exists(id);
        }

        public AudioLanguagePreference Get(int id)
        {
            return _repo.Get(id);
        }

        public List<AudioLanguagePreference> All()
        {
            return _repo.All().ToList();
        }

        public AudioPreferenceResult Evaluate(RemoteMovie subject)
        {
            var preferenceId = subject?.Movie?.QualityProfile?.AudioLanguagePreferenceId;

            if (!preferenceId.HasValue || preferenceId.Value <= 0 || !Exists(preferenceId.Value))
            {
                return EvaluateLegacyChinesePreference(subject);
            }

            var preference = Get(preferenceId.Value);

            if (preference == null || !preference.Enabled)
            {
                return EvaluateLegacyChinesePreference(subject);
            }

            var audioInfo = _audioLanguageMappingService.TagAudioTracks(subject) ??
                            AudioLanguageMapper.TagAudioTracks(subject, subject?.Release?.AudioInfo);
            var scoreProfileId = preference.AudioScoreProfileId ?? subject?.Movie?.QualityProfile?.AudioScoreProfileId;
            var scoredTracks = audioInfo
                .Select(audio => _audioScoreProfileService.Score(audio, scoreProfileId))
                .ToList();

            var entries = preference.Entries?.Where(entry => entry.Enabled && entry.LanguageTag.IsNotNullOrWhiteSpace()).ToList() ?? new List<AudioLanguagePreferenceItem>();
            AudioTrackScoreResult selected = null;
            string selectedPreferenceTag = null;

            foreach (var entry in entries)
            {
                var bestForEntry = scoredTracks
                    .Where(track => MatchesPreference(track.AudioInfo, entry.LanguageTag))
                    .OrderByDescending(track => track.Score)
                    .FirstOrDefault();

                if (bestForEntry == null)
                {
                    continue;
                }

                if (selected == null)
                {
                    selected = bestForEntry;
                    selectedPreferenceTag = entry.LanguageTag;
                    continue;
                }

                if (bestForEntry.Score - selected.Score > preference.ScoreGapThreshold)
                {
                    selected = bestForEntry;
                    selectedPreferenceTag = entry.LanguageTag;
                }
            }

            return new AudioPreferenceResult
            {
                SelectedAudio = selected?.AudioInfo,
                AudioScore = selected?.Score ?? 0,
                AudioScoreBreakdown = selected?.Breakdown ?? new List<string>(),
                AudioLanguagePreferenceName = preference.Name,
                SelectedPreferenceTag = selectedPreferenceTag,
                HasChineseAudio = audioInfo.Any(audio => AudioLanguageMapper.HasLanguage(audio, Language.Chinese)) ||
                                  subject?.Languages?.Contains(Language.Chinese) == true,
                HasChineseSubtitle = subject?.Release?.Subs?.Any(IsChineseText) == true,
                IsMediaInfoPending = IsMediaInfoPending(subject?.Release)
            };
        }

        private AudioPreferenceResult EvaluateLegacyChinesePreference(RemoteMovie subject)
        {
            var legacy = ChineseMediaPreferenceEvaluator.Evaluate(subject, _audioLanguageMappingService);

            return new AudioPreferenceResult
            {
                SelectedAudio = legacy.SelectedAudio,
                AudioScore = legacy.AudioPreferenceScore,
                AudioScoreBreakdown = legacy.SelectedAudio == null ? new List<string>() : new List<string> { $"Legacy Chinese preference: {legacy.AudioPreferenceScore}" },
                AudioLanguagePreferenceName = "Legacy Chinese Preference",
                SelectedPreferenceTag = legacy.SelectedAudio?.MappedLanguage?.Name ?? legacy.SelectedAudio?.Language,
                HasChineseAudio = legacy.HasChineseAudio,
                HasChineseSubtitle = legacy.HasChineseSubtitle,
                IsMediaInfoPending = legacy.IsMediaInfoPending
            };
        }

        private static bool MatchesPreference(ReleaseAudioInfo audioInfo, string languageTag)
        {
            if (audioInfo == null || languageTag.IsNullOrWhiteSpace())
            {
                return false;
            }

            if (languageTag.Equals(AudioLanguageMapper.OriginTag, System.StringComparison.OrdinalIgnoreCase))
            {
                return AudioLanguageMapper.HasTag(audioInfo, AudioLanguageMapper.OriginTag);
            }

            var language = Language.All.FirstOrDefault(value => value.Name.Equals(languageTag, System.StringComparison.OrdinalIgnoreCase));

            if (language != null)
            {
                return AudioLanguageMapper.HasLanguage(audioInfo, language);
            }

            return AudioLanguageMapper.HasTag(audioInfo, languageTag);
        }

        private static bool IsChineseText(string value)
        {
            if (value == null)
            {
                return false;
            }

            var normalized = new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

            return normalized.Contains("chinese") ||
                   normalized.Contains("mandarin") ||
                   normalized.Contains("guoyu") ||
                   normalized.Contains("zh") ||
                   normalized.Contains("zho") ||
                   normalized.Contains("chi") ||
                   normalized.Contains("chs") ||
                   normalized.Contains("cht") ||
                   value.Contains('\u4e2d') ||
                   value.Contains('\u56fd') ||
                   value.Contains('\u570b');
        }

        private static bool IsMediaInfoPending(ReleaseInfo release)
        {
            return release?.MediaInfoStatus?.Equals("pending", System.StringComparison.OrdinalIgnoreCase) == true ||
                   release?.MediaInfoProgressStatus?.Equals("pending", System.StringComparison.OrdinalIgnoreCase) == true;
        }
    }

    public class AudioPreferenceResult
    {
        public ReleaseAudioInfo SelectedAudio { get; set; }
        public int AudioScore { get; set; }
        public List<string> AudioScoreBreakdown { get; set; }
        public string AudioLanguagePreferenceName { get; set; }
        public string SelectedPreferenceTag { get; set; }
        public bool HasChineseAudio { get; set; }
        public bool HasChineseSubtitle { get; set; }
        public bool IsMediaInfoPending { get; set; }

        public bool HasChineseAudioOrSubtitle => HasChineseAudio || HasChineseSubtitle;
    }
}
