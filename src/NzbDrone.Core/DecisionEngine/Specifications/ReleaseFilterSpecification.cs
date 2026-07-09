using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.ReleaseFilters;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class ReleaseFilterMediaInfoPendingSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IReleaseFilterProfileService _releaseFilterProfileService;
        private readonly IReleaseFilterEvaluator _releaseFilterEvaluator;
        private readonly Logger _logger;

        public ReleaseFilterMediaInfoPendingSpecification(IReleaseFilterProfileService releaseFilterProfileService,
                                                          IReleaseFilterEvaluator releaseFilterEvaluator,
                                                          Logger logger)
        {
            _releaseFilterProfileService = releaseFilterProfileService;
            _releaseFilterEvaluator = releaseFilterEvaluator;
            _logger = logger;
        }

        public RejectionType Type => RejectionType.Temporary;
        public SpecificationPriority Priority => SpecificationPriority.Default;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, SearchCriteriaBase searchCriteria)
        {
            if (searchCriteria?.InteractiveSearch == true)
            {
                return DownloadSpecDecision.Accept();
            }

            var profile = ReleaseFilterSpecificationHelper.GetReleaseFilterProfile(subject, _releaseFilterProfileService);

            if (profile == null || !_releaseFilterEvaluator.RequiresMediaInfo(profile) || !ReleaseFilterSpecificationHelper.IsMediaInfoPending(subject))
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Release Filter Profile '{0}' is waiting for mediaInfo on '{1}'", profile.Name, subject.Release.Title);

            return DownloadSpecDecision.Reject(DownloadRejectionReason.ReleaseFilterMediaInfoPending, "Waiting for mediaInfo before checking backend release filter");
        }
    }

    public class ReleaseFilterSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IReleaseFilterProfileService _releaseFilterProfileService;
        private readonly IReleaseFilterEvaluator _releaseFilterEvaluator;
        private readonly Logger _logger;

        public ReleaseFilterSpecification(IReleaseFilterProfileService releaseFilterProfileService,
                                          IReleaseFilterEvaluator releaseFilterEvaluator,
                                          Logger logger)
        {
            _releaseFilterProfileService = releaseFilterProfileService;
            _releaseFilterEvaluator = releaseFilterEvaluator;
            _logger = logger;
        }

        public RejectionType Type => RejectionType.Permanent;
        public SpecificationPriority Priority => SpecificationPriority.Default;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, SearchCriteriaBase searchCriteria)
        {
            var profile = ReleaseFilterSpecificationHelper.GetReleaseFilterProfile(subject, _releaseFilterProfileService);

            if (profile == null)
            {
                return DownloadSpecDecision.Accept();
            }

            if (_releaseFilterEvaluator.RequiresMediaInfo(profile))
            {
                if (ReleaseFilterSpecificationHelper.IsMediaInfoPending(subject))
                {
                    return DownloadSpecDecision.Accept();
                }

                if (ReleaseFilterSpecificationHelper.IsMediaInfoUnavailable(subject))
                {
                    _logger.Debug("Release '{0}' rejected because Release Filter Profile '{1}' needs mediaInfo, but mediaInfo is unavailable",
                        subject.Release.Title,
                        profile.Name);

                    return DownloadSpecDecision.Reject(DownloadRejectionReason.ReleaseFilterMediaInfoUnavailable, "MediaInfo unavailable for backend release filter");
                }
            }

            var result = _releaseFilterEvaluator.Evaluate(subject, profile);

            if (result.Accepted)
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Release '{0}' rejected by backend Release Filter Profile '{1}': {2}",
                subject.Release.Title,
                profile.Name,
                result.Reason);

            return DownloadSpecDecision.Reject(DownloadRejectionReason.ReleaseFilterRejected, "{0}", result.Reason);
        }
    }

    internal static class ReleaseFilterSpecificationHelper
    {
        public static ReleaseFilterProfile GetReleaseFilterProfile(RemoteMovie subject, IReleaseFilterProfileService releaseFilterProfileService)
        {
            var releaseFilterProfileId = subject?.Movie?.QualityProfile?.ReleaseFilterProfileId;

            if (!releaseFilterProfileId.HasValue || releaseFilterProfileId.Value <= 0)
            {
                return null;
            }

            if (!releaseFilterProfileService.Exists(releaseFilterProfileId.Value))
            {
                return null;
            }

            return releaseFilterProfileService.Get(releaseFilterProfileId.Value);
        }

        public static bool IsMediaInfoPending(RemoteMovie subject)
        {
            return subject?.Release?.MediaInfoStatus?.Equals("pending", System.StringComparison.OrdinalIgnoreCase) == true ||
                   subject?.Release?.MediaInfoProgressStatus?.Equals("pending", System.StringComparison.OrdinalIgnoreCase) == true;
        }

        public static bool IsMediaInfoUnavailable(RemoteMovie subject)
        {
            var release = subject?.Release;

            if (release == null)
            {
                return true;
            }

            var hasMediaInfo = release.AudioInfo?.Any() == true || release.Subs?.Any() == true;

            if (hasMediaInfo)
            {
                return false;
            }

            return release.MediaInfoStatus.IsNotNullOrWhiteSpace() &&
                   (release.MediaInfoStatus.Equals("failed", System.StringComparison.OrdinalIgnoreCase) ||
                    release.MediaInfoStatus.Equals("unavailable", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
