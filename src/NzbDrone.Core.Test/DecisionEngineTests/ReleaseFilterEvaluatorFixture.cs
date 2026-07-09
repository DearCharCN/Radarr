using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.ReleaseFilters;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class ReleaseFilterEvaluatorFixture
    {
        private ReleaseFilterEvaluator _evaluator;
        private RemoteMovie _remoteMovie;

        [SetUp]
        public void SetUp()
        {
            _evaluator = new ReleaseFilterEvaluator();
            _remoteMovie = new RemoteMovie
            {
                Release = new TorrentInfo
                {
                    Title = "A Movie 2026 1080p WEB-DL",
                    Indexer = "M-Team",
                    DownloadProtocol = DownloadProtocol.Torrent,
                    PublishDate = DateTime.UtcNow.AddHours(-2),
                    Size = 20_000_000_000,
                    Seeders = 42,
                    Peers = 50,
                    IndexerFlags = IndexerFlags.G_Freeleech
                },
                ParsedMovieInfo = new ParsedMovieInfo
                {
                    Quality = new QualityModel(Quality.WEBDL1080p),
                    ReleaseGroup = "Group"
                },
                Languages = new List<Language> { Language.English },
                CustomFormats = new List<CustomFormat> { new ("DoVi") },
                CustomFormatScore = 100
            };
        }

        [Test]
        public void should_accept_when_all_and_conditions_match()
        {
            var profile = GivenProfile(Group("and",
                Condition("title", "contains", "WEB-DL"),
                Condition("indexer", "equal", "M-Team"),
                Condition("customFormatScore", "greaterThanOrEqual", 100)));

            var result = _evaluator.Evaluate(_remoteMovie, profile);

            result.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_an_and_condition_does_not_match()
        {
            var profile = GivenProfile(Group("and",
                Condition("title", "contains", "WEB-DL"),
                Condition("seeders", "greaterThan", 100)));

            var result = _evaluator.Evaluate(_remoteMovie, profile);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Contain("seeders");
        }

        [Test]
        public void should_accept_when_any_or_condition_matches()
        {
            var profile = GivenProfile(Group("or",
                Condition("indexer", "equal", "Other"),
                Condition("quality", "equal", "WEBDL-1080p")));

            var result = _evaluator.Evaluate(_remoteMovie, profile);

            result.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_match_list_fields()
        {
            var profile = GivenProfile(Group("and",
                Condition("customFormats", "contains", "DoVi"),
                Condition("languages", "contains", "English")));

            var result = _evaluator.Evaluate(_remoteMovie, profile);

            result.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_match_media_info_audio_and_subtitle_fields()
        {
            _remoteMovie.Release.AudioInfo = new List<ReleaseAudioInfo>
            {
                new () { Language = "English", Specification = "TrueHD Atmos 7.1" },
                new () { Language = "Chinese", Specification = "DDP 5.1" }
            };
            _remoteMovie.Release.Subs = new List<string> { "CHS", "English" };

            var profile = GivenProfile(Group("and",
                Condition("audioLanguages", "contains", "Chinese"),
                Condition("audioSpecifications", "contains", "DDP"),
                Condition("subtitleLanguages", "contains", "CHS")));

            var result = _evaluator.Evaluate(_remoteMovie, profile);

            result.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_match_selected_audio_fields_and_score()
        {
            _remoteMovie.Release.AudioInfo = new List<ReleaseAudioInfo>
            {
                new () { Language = "English", Specification = "TrueHD Atmos 7.1" },
                new () { Language = "Chinese", Specification = "DDP 5.1" }
            };
            _remoteMovie.Release.Subs = new List<string> { "Chinese" };

            var profile = GivenProfile(Group("and",
                Condition("selectedAudioLanguage", "equal", "Chinese"),
                Condition("selectedAudioSpecification", "contains", "DDP"),
                Condition("audioScore", "greaterThanOrEqual", 30),
                Condition("hasChineseAudioOrSubtitle", "equal", true)));

            var result = _evaluator.Evaluate(_remoteMovie, profile);

            result.Accepted.Should().BeTrue();
        }

        [Test]
        public void should_report_when_filter_requires_media_info()
        {
            var mediaInfoProfile = GivenProfile(Group("and",
                Condition("audioLanguages", "contains", "Chinese")));
            var titleOnlyProfile = GivenProfile(Group("and",
                Condition("title", "contains", "WEB-DL")));

            _evaluator.RequiresMediaInfo(mediaInfoProfile).Should().BeTrue();
            _evaluator.RequiresMediaInfo(titleOnlyProfile).Should().BeFalse();
        }

        private ReleaseFilterProfile GivenProfile(ReleaseFilterNode filter)
        {
            return new ReleaseFilterProfile
            {
                Name = "Test Filter",
                Enabled = true,
                Filter = STJson.ToJson(filter)
            };
        }

        private ReleaseFilterNode Group(string mode, params ReleaseFilterNode[] children)
        {
            return new ReleaseFilterNode
            {
                Type = "group",
                Mode = mode,
                Children = new List<ReleaseFilterNode>(children)
            };
        }

        private ReleaseFilterNode Condition(string field, string op, object value)
        {
            return new ReleaseFilterNode
            {
                Type = "condition",
                Field = field,
                Operator = op,
                Value = JsonSerializer.Deserialize<JsonElement>(STJson.ToJson(value), STJson.GetSerializerSettings())
            };
        }
    }
}
