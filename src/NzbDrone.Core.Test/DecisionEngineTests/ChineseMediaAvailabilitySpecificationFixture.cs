using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class ChineseMediaAvailabilitySpecificationFixture : CoreTest
    {
        private RemoteMovie _remoteMovie;

        [SetUp]
        public void Setup()
        {
            _remoteMovie = new RemoteMovie
            {
                Movie = new Movie
                {
                    MovieMetadata = new MovieMetadata
                    {
                        OriginalLanguage = Language.English
                    }
                },
                Languages = new List<Language> { Language.English },
                Release = new ReleaseInfo
                {
                    Title = "A Movie 2026",
                    AudioInfo = new List<ReleaseAudioInfo>
                    {
                        new () { Language = "English", Specification = "DDP 5.1" }
                    },
                    Subs = new List<string>()
                }
            };
        }

        [Test]
        public void pending_spec_should_temporarily_reject_when_media_info_is_pending_and_no_chinese_access_is_known()
        {
            _remoteMovie.Release.MediaInfoStatus = "pending";

            Mocker.Resolve<ChineseMediaInfoPendingSpecification>().IsSatisfiedBy(_remoteMovie, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void pending_spec_should_accept_when_chinese_subtitle_is_already_known()
        {
            _remoteMovie.Release.MediaInfoStatus = "pending";
            _remoteMovie.Release.Subs = new List<string> { "Chinese" };

            Mocker.Resolve<ChineseMediaInfoPendingSpecification>().IsSatisfiedBy(_remoteMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void pending_spec_should_accept_interactive_search_pending_media_info()
        {
            _remoteMovie.Release.MediaInfoStatus = "pending";

            var searchCriteria = new MovieSearchCriteria
            {
                InteractiveSearch = true
            };

            Mocker.Resolve<ChineseMediaInfoPendingSpecification>().IsSatisfiedBy(_remoteMovie, searchCriteria).Accepted.Should().BeTrue();
        }

        [Test]
        public void availability_spec_should_reject_when_neither_chinese_audio_nor_chinese_subtitles_exist()
        {
            Mocker.Resolve<ChineseMediaAvailabilitySpecification>().IsSatisfiedBy(_remoteMovie, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void availability_spec_should_accept_chinese_subtitles()
        {
            _remoteMovie.Release.Subs = new List<string> { "Chinese" };

            Mocker.Resolve<ChineseMediaAvailabilitySpecification>().IsSatisfiedBy(_remoteMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void availability_spec_should_accept_chinese_audio()
        {
            _remoteMovie.Release.AudioInfo = new List<ReleaseAudioInfo>
            {
                new () { Language = "Chinese", Specification = "DDP 5.1" }
            };

            Mocker.Resolve<ChineseMediaAvailabilitySpecification>().IsSatisfiedBy(_remoteMovie, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void availability_spec_should_not_permanently_reject_pending_media_info()
        {
            _remoteMovie.Release.MediaInfoStatus = "pending";

            Mocker.Resolve<ChineseMediaAvailabilitySpecification>().IsSatisfiedBy(_remoteMovie, null).Accepted.Should().BeTrue();
        }
    }
}
