using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Profiles.AudioPreferences;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Profiles.AudioPreferences
{
    public class AudioLanguagePreferenceResource : RestResource
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public int ScoreGapThreshold { get; set; }
        public int? AudioScoreProfileId { get; set; }
        public List<AudioLanguagePreferenceItem> Entries { get; set; }
    }

    public static class AudioLanguagePreferenceResourceMapper
    {
        public static AudioLanguagePreferenceResource ToResource(this AudioLanguagePreference model)
        {
            if (model == null)
            {
                return null;
            }

            return new AudioLanguagePreferenceResource
            {
                Id = model.Id,
                Name = model.Name,
                Enabled = model.Enabled,
                ScoreGapThreshold = model.ScoreGapThreshold,
                AudioScoreProfileId = model.AudioScoreProfileId,
                Entries = model.Entries ?? new List<AudioLanguagePreferenceItem>()
            };
        }

        public static AudioLanguagePreference ToModel(this AudioLanguagePreferenceResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new AudioLanguagePreference
            {
                Id = resource.Id,
                Name = resource.Name,
                Enabled = resource.Enabled,
                ScoreGapThreshold = resource.ScoreGapThreshold,
                AudioScoreProfileId = resource.AudioScoreProfileId,
                Entries = resource.Entries ?? new List<AudioLanguagePreferenceItem>()
            };
        }

        public static List<AudioLanguagePreferenceResource> ToResource(this IEnumerable<AudioLanguagePreference> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
