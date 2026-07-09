using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Profiles.ReleaseFilters;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class ReleaseFilterSpecificationFixture : CoreTest
    {
        private const int ReleaseFilterProfileId = 7;

        private RemoteMovie _remoteMovie;
        private ReleaseFilterProfile _profile;

        [SetUp]
        public void SetUp()
        {
            _profile = new ReleaseFilterProfile
            {
                Id = ReleaseFilterProfileId,
                Name = "MediaInfo Filter",
                Enabled = true
            };

            _remoteMovie = new RemoteMovie
            {
                Movie = new Movie
                {
                    QualityProfile = new QualityProfile
                    {
                        ReleaseFilterProfileId = ReleaseFilterProfileId
                    }
                },
                Release = new ReleaseInfo
                {
                    Title = "A Movie 2026 1080p WEB-DL",
                    AudioInfo = new List<ReleaseAudioInfo>(),
                    Subs = new List<string>()
                }
            };

            Mocker.GetMock<IReleaseFilterProfileService>()
                .Setup(x => x.Exists(ReleaseFilterProfileId))
                .Returns(true);

            Mocker.GetMock<IReleaseFilterProfileService>()
                .Setup(x => x.Get(ReleaseFilterProfileId))
                .Returns(_profile);

            Mocker.GetMock<IReleaseFilterEvaluator>()
                .Setup(x => x.RequiresMediaInfo(_profile))
                .Returns(true);

            Mocker.GetMock<IReleaseFilterEvaluator>()
                .Setup(x => x.Evaluate(_remoteMovie, _profile))
                .Returns(ReleaseFilterEvaluationResult.Accept());
        }

        [Test]
        public void pending_spec_should_temporarily_reject_when_filter_needs_media_info()
        {
            _remoteMovie.Release.MediaInfoStatus = "pending";

            var result = Mocker.Resolve<ReleaseFilterMediaInfoPendingSpecification>().IsSatisfiedBy(_remoteMovie, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(DownloadRejectionReason.ReleaseFilterMediaInfoPending);
            Mocker.Resolve<ReleaseFilterMediaInfoPendingSpecification>().Type.Should().Be(RejectionType.Temporary);
        }

        [Test]
        public void pending_spec_should_accept_interactive_search()
        {
            _remoteMovie.Release.MediaInfoStatus = "pending";

            var searchCriteria = new MovieSearchCriteria
            {
                InteractiveSearch = true
            };

            Mocker.Resolve<ReleaseFilterMediaInfoPendingSpecification>().IsSatisfiedBy(_remoteMovie, searchCriteria).Accepted.Should().BeTrue();
        }

        [Test]
        public void release_filter_spec_should_not_permanently_reject_pending_media_info()
        {
            _remoteMovie.Release.MediaInfoStatus = "pending";

            var result = Mocker.Resolve<ReleaseFilterSpecification>().IsSatisfiedBy(_remoteMovie, null);

            result.Accepted.Should().BeTrue();
        }

        [Test]
        public void release_filter_spec_should_reject_when_required_media_info_is_unavailable()
        {
            _remoteMovie.Release.MediaInfoStatus = "failed";

            var result = Mocker.Resolve<ReleaseFilterSpecification>().IsSatisfiedBy(_remoteMovie, null);

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(DownloadRejectionReason.ReleaseFilterMediaInfoUnavailable);
            Mocker.GetMock<IReleaseFilterEvaluator>().Verify(x => x.Evaluate(_remoteMovie, _profile), Times.Never());
        }

        [Test]
        public void release_filter_spec_should_evaluate_when_required_media_info_exists()
        {
            _remoteMovie.Release.AudioInfo = new List<ReleaseAudioInfo>
            {
                new () { Language = "Chinese", Specification = "DDP 5.1" }
            };

            var result = Mocker.Resolve<ReleaseFilterSpecification>().IsSatisfiedBy(_remoteMovie, null);

            result.Accepted.Should().BeTrue();
            Mocker.GetMock<IReleaseFilterEvaluator>().Verify(x => x.Evaluate(_remoteMovie, _profile), Times.Once());
        }
    }
}
