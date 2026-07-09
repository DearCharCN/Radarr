using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Profiles.AudioPreferences;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IPrioritizeDownloadDecision
    {
        List<DownloadDecision> PrioritizeDecisionsForMovies(List<DownloadDecision> decisions);
    }

    public class DownloadDecisionPriorizationService : IPrioritizeDownloadDecision
    {
        private readonly IConfigService _configService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IQualityDefinitionService _qualityDefinitionService;
        private readonly IAudioLanguagePreferenceService _audioLanguagePreferenceService;

        public DownloadDecisionPriorizationService(IConfigService configService,
                                                   IDelayProfileService delayProfileService,
                                                   IQualityDefinitionService qualityDefinitionService,
                                                   IAudioLanguagePreferenceService audioLanguagePreferenceService)
        {
            _configService = configService;
            _delayProfileService = delayProfileService;
            _qualityDefinitionService = qualityDefinitionService;
            _audioLanguagePreferenceService = audioLanguagePreferenceService;
        }

        public List<DownloadDecision> PrioritizeDecisionsForMovies(List<DownloadDecision> decisions)
        {
            return decisions.Where(c => c.RemoteMovie.Movie != null)
                            .GroupBy(c => c.RemoteMovie.Movie.Id, (movieId, downloadDecisions) =>
                            {
                                return downloadDecisions.OrderByDescending(decision => decision, new DownloadDecisionComparer(_configService, _delayProfileService, _qualityDefinitionService, _audioLanguagePreferenceService));
                            })
                            .SelectMany(c => c)
                            .Union(decisions.Where(c => c.RemoteMovie.Movie == null))
                            .ToList();
        }
    }
}
