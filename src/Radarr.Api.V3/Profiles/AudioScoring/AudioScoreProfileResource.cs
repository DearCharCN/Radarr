using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Profiles.AudioScoring;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Profiles.AudioScoring
{
    public class AudioScoreProfileResource : RestResource
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public List<AudioScoreRule> Rules { get; set; }
        public List<AudioScoreMutexGroup> MutexGroups { get; set; }
    }

    public static class AudioScoreProfileResourceMapper
    {
        public static AudioScoreProfileResource ToResource(this AudioScoreProfile model)
        {
            if (model == null)
            {
                return null;
            }

            return new AudioScoreProfileResource
            {
                Id = model.Id,
                Name = model.Name,
                Enabled = model.Enabled,
                Rules = model.Rules ?? new List<AudioScoreRule>(),
                MutexGroups = model.MutexGroups ?? new List<AudioScoreMutexGroup>()
            };
        }

        public static AudioScoreProfile ToModel(this AudioScoreProfileResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new AudioScoreProfile
            {
                Id = resource.Id,
                Name = resource.Name,
                Enabled = resource.Enabled,
                Rules = resource.Rules ?? new List<AudioScoreRule>(),
                MutexGroups = resource.MutexGroups ?? new List<AudioScoreMutexGroup>()
            };
        }

        public static List<AudioScoreProfileResource> ToResource(this IEnumerable<AudioScoreProfile> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
