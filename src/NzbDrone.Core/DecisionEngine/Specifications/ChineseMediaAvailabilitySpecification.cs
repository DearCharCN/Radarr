using NLog;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.AudioLanguageMappings;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class ChineseMediaInfoPendingSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IAudioLanguageMappingService _audioLanguageMappingService;
        private readonly Logger _logger;

        public ChineseMediaInfoPendingSpecification(IAudioLanguageMappingService audioLanguageMappingService, Logger logger)
        {
            _audioLanguageMappingService = audioLanguageMappingService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Temporary;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, SearchCriteriaBase searchCriteria)
        {
            var preference = ChineseMediaPreferenceEvaluator.Evaluate(subject, _audioLanguageMappingService);

            if (searchCriteria?.InteractiveSearch == true && preference.IsMediaInfoPending)
            {
                return DownloadSpecDecision.Accept();
            }

            if (preference.HasChineseAudioOrSubtitle || !preference.IsMediaInfoPending)
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Chinese audio/subtitle preference is waiting for mediaInfo on '{0}'", subject.Release.Title);

            return DownloadSpecDecision.Reject(DownloadRejectionReason.ChineseMediaInfoPending, "Waiting for mediaInfo before checking Chinese audio/subtitle preference");
        }
    }

    public class ChineseMediaAvailabilitySpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IAudioLanguageMappingService _audioLanguageMappingService;
        private readonly Logger _logger;

        public ChineseMediaAvailabilitySpecification(IAudioLanguageMappingService audioLanguageMappingService, Logger logger)
        {
            _audioLanguageMappingService = audioLanguageMappingService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, SearchCriteriaBase searchCriteria)
        {
            var preference = ChineseMediaPreferenceEvaluator.Evaluate(subject, _audioLanguageMappingService);

            if (preference.HasChineseAudioOrSubtitle || preference.IsMediaInfoPending)
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Release '{0}' rejected because it has neither Chinese audio nor Chinese subtitles", subject.Release.Title);

            return DownloadSpecDecision.Reject(DownloadRejectionReason.ChineseAudioOrSubtitleRequired, "Chinese audio or Chinese subtitles are required");
        }
    }
}
