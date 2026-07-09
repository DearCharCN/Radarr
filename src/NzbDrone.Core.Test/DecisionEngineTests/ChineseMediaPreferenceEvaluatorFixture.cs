using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class ChineseMediaPreferenceEvaluatorFixture
    {
        private RemoteMovie GivenRemoteMovie(Language originalLanguage, List<ReleaseAudioInfo> audioInfo, List<string> subs = null)
        {
            return new RemoteMovie
            {
                Movie = new Movie
                {
                    MovieMetadata = new MovieMetadata
                    {
                        OriginalLanguage = originalLanguage
                    }
                },
                Languages = new List<Language> { originalLanguage },
                Release = new ReleaseInfo
                {
                    Title = "A Movie 2026",
                    AudioInfo = audioInfo,
                    Subs = subs ?? new List<string>()
                }
            };
        }

        [Test]
        public void should_select_high_quality_chinese_audio_when_it_meets_minimum()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.English,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "English", Specification = "TrueHD Atmos 7.1" },
                    new () { Language = "Chinese", Specification = "TrueHD Atmos 7.1" }
                });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.HasChineseAudioOrSubtitle.Should().BeTrue();
            result.SelectedAudio.Language.Should().Be("Chinese");
            result.AudioPreferenceScore.Should().Be(100);
        }

        [Test]
        public void should_select_chinese_ddp_51_when_available()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.English,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "English", Specification = "TrueHD Atmos 7.1" },
                    new () { Language = "Chinese", Specification = "DDP 5.1" }
                },
                new List<string> { "Chinese" });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.SelectedAudio.Language.Should().Be("Chinese");
            result.AudioPreferenceScore.Should().Be(30);
        }

        [Test]
        public void should_select_chinese_audio_when_language_is_mapped_alias()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.English,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "English", Specification = "TrueHD Atmos 7.1" },
                    new () { Language = "Guoyu", Specification = "DDP 5.1" }
                },
                new List<string> { "Chinese" });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.SelectedAudio.Language.Should().Be("Guoyu");
            result.SelectedAudio.MappedLanguage.Should().Be(Language.Chinese);
            result.SelectedAudio.LanguageTags.Should().Contain("Chinese");
            result.AudioPreferenceScore.Should().Be(30);
        }

        [Test]
        public void should_select_original_audio_when_chinese_audio_is_below_minimum_and_original_is_better_with_chinese_subtitles()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.English,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "English", Specification = "TrueHD Atmos 7.1" },
                    new () { Language = "Chinese", Specification = "AAC 2.0" }
                },
                new List<string> { "Chinese" });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.SelectedAudio.Language.Should().Be("English");
            result.AudioPreferenceScore.Should().Be(100);
        }

        [Test]
        public void should_select_chinese_audio_when_both_chinese_and_original_low_specs_are_equal()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.English,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "English", Specification = "AAC 2.0" },
                    new () { Language = "Chinese", Specification = "AAC 2.0" }
                },
                new List<string> { "Chinese" });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.SelectedAudio.Language.Should().Be("Chinese");
            result.AudioPreferenceScore.Should().Be(0);
        }

        [Test]
        public void should_select_original_audio_when_both_chinese_and_original_are_below_minimum_but_not_equal()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.English,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "English", Specification = "AAC 2.0" },
                    new () { Language = "Chinese", Specification = "AAC 5.1" }
                },
                new List<string> { "Chinese" });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.SelectedAudio.Language.Should().Be("English");
        }

        [Test]
        public void should_keep_chinese_audio_when_no_chinese_subtitle_exists()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.English,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "English", Specification = "TrueHD Atmos 7.1" },
                    new () { Language = "Chinese", Specification = "AAC 2.0" }
                });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.SelectedAudio.Language.Should().Be("Chinese");
        }

        [Test]
        public void should_treat_chinese_original_language_as_chinese_audio()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.Chinese,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "Chinese", Specification = "AAC 2.0" }
                },
                new List<string> { "English" });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.HasChineseAudioOrSubtitle.Should().BeTrue();
            result.SelectedAudio.Language.Should().Be("Chinese");
        }

        [Test]
        public void should_select_original_audio_when_only_chinese_subtitles_are_available()
        {
            var remoteMovie = GivenRemoteMovie(
                Language.Japanese,
                new List<ReleaseAudioInfo>
                {
                    new () { Language = "Japanese", Specification = "DDP 5.1" }
                },
                new List<string> { "CHS" });

            var result = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie);

            result.HasChineseAudio.Should().BeFalse();
            result.HasChineseSubtitle.Should().BeTrue();
            result.SelectedAudio.Language.Should().Be("Japanese");
            result.AudioPreferenceScore.Should().Be(30);
        }
    }
}
