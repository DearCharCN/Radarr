using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Profiles.AudioScoring
{
    public interface IAudioScoreProfileService
    {
        AudioScoreProfile Add(AudioScoreProfile profile);
        List<AudioScoreProfile> All();
        void Delete(int id);
        bool Exists(int id);
        AudioScoreProfile Get(int id);
        AudioScoreProfile Update(AudioScoreProfile profile);
        AudioTrackScoreResult Score(ReleaseAudioInfo audioInfo, int? profileId);
        AudioTrackScoreResult Score(ReleaseAudioInfo audioInfo, AudioScoreProfile profile);
    }

    public class AudioScoreProfileService : IAudioScoreProfileService
    {
        private readonly IAudioScoreProfileRepository _repo;

        public AudioScoreProfileService(IAudioScoreProfileRepository repo)
        {
            _repo = repo;
        }

        public AudioScoreProfile Add(AudioScoreProfile profile)
        {
            return _repo.Insert(profile);
        }

        public AudioScoreProfile Update(AudioScoreProfile profile)
        {
            return _repo.Update(profile);
        }

        public void Delete(int id)
        {
            _repo.Delete(id);
        }

        public bool Exists(int id)
        {
            return _repo.Exists(id);
        }

        public AudioScoreProfile Get(int id)
        {
            return _repo.Get(id);
        }

        public List<AudioScoreProfile> All()
        {
            return _repo.All().ToList();
        }

        public AudioTrackScoreResult Score(ReleaseAudioInfo audioInfo, int? profileId)
        {
            if (!profileId.HasValue || profileId.Value <= 0 || !Exists(profileId.Value))
            {
                return AudioScoreEngine.Score(audioInfo, null);
            }

            return Score(audioInfo, Get(profileId.Value));
        }

        public AudioTrackScoreResult Score(ReleaseAudioInfo audioInfo, AudioScoreProfile profile)
        {
            return AudioScoreEngine.Score(audioInfo, profile);
        }
    }

    public static class AudioScoreEngine
    {
        private static readonly Regex NonAlphaNumericRegex = new (@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static AudioTrackScoreResult Score(ReleaseAudioInfo audioInfo, AudioScoreProfile profile)
        {
            var result = new AudioTrackScoreResult
            {
                AudioInfo = audioInfo,
                Score = 0,
                Matches = new List<AudioScoreMatch>()
            };

            if (audioInfo == null || profile == null || !profile.Enabled)
            {
                return result;
            }

            var rules = profile.Rules?.Where(rule => rule.Enabled && rule.Pattern.IsNotNullOrWhiteSpace()).ToList() ?? new List<AudioScoreRule>();

            if (!rules.Any())
            {
                return result;
            }

            var enabledMutexGroups = (profile.MutexGroups ?? new List<AudioScoreMutexGroup>())
                .Where(group => group.Enabled && group.Name.IsNotNullOrWhiteSpace())
                .Select(group => group.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var directMatches = new List<AudioScoreMatch>();
            var mutexMatches = new Dictionary<string, List<AudioScoreMatch>>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rules)
            {
                if (!Matches(audioInfo, rule))
                {
                    continue;
                }

                var match = new AudioScoreMatch
                {
                    RuleName = rule.Name,
                    Pattern = rule.Pattern,
                    Score = rule.Score,
                    MutexGroup = rule.MutexGroup
                };

                if (rule.MutexGroup.IsNotNullOrWhiteSpace() && enabledMutexGroups.Contains(rule.MutexGroup))
                {
                    if (!mutexMatches.TryGetValue(rule.MutexGroup, out var groupMatches))
                    {
                        groupMatches = new List<AudioScoreMatch>();
                        mutexMatches[rule.MutexGroup] = groupMatches;
                    }

                    groupMatches.Add(match);
                    continue;
                }

                directMatches.Add(match);
            }

            result.Matches.AddRange(directMatches);

            foreach (var groupMatches in mutexMatches)
            {
                var selected = groupMatches.Value
                    .OrderByDescending(match => match.Score)
                    .ThenBy(match => match.RuleName)
                    .First();

                selected.MutexWinner = true;
                result.Matches.Add(selected);
            }

            result.Score = result.Matches.Sum(match => match.Score);

            return result;
        }

        private static bool Matches(ReleaseAudioInfo audioInfo, AudioScoreRule rule)
        {
            var text = string.Join(" ", new[]
            {
                audioInfo.Language,
                audioInfo.MappedLanguage?.Name,
                audioInfo.Specification,
                audioInfo.MatchedLanguageAlias
            }.Concat(audioInfo.LanguageTags ?? new List<string>())
             .Where(value => value.IsNotNullOrWhiteSpace()));

            switch (NormalizeKey(rule.MatchType))
            {
                case "regex":
                    try
                    {
                        return Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase);
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }

                case "normalizedcontains":
                case "normalized":
                    return NormalizeText(text).Contains(NormalizeText(rule.Pattern));
                case "":
                case "contains":
                default:
                    return text.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string NormalizeKey(string value)
        {
            return NormalizeText(value);
        }

        private static string NormalizeText(string value)
        {
            return NonAlphaNumericRegex.Replace(value ?? string.Empty, string.Empty).ToLowerInvariant();
        }
    }

    public class AudioTrackScoreResult
    {
        public ReleaseAudioInfo AudioInfo { get; set; }
        public int Score { get; set; }
        public List<AudioScoreMatch> Matches { get; set; }

        public List<string> Breakdown => Matches?.Select(match => match.ToString()).ToList() ?? new List<string>();
    }

    public class AudioScoreMatch
    {
        public string RuleName { get; set; }
        public string Pattern { get; set; }
        public int Score { get; set; }
        public string MutexGroup { get; set; }
        public bool MutexWinner { get; set; }

        public override string ToString()
        {
            var label = RuleName.IsNotNullOrWhiteSpace() ? RuleName : Pattern;
            var group = MutexGroup.IsNotNullOrWhiteSpace() ? $" [{MutexGroup}]" : string.Empty;

            return $"{label}{group}: {Score}";
        }
    }
}
