using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Languages;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Profiles.AudioLanguageMapping
{
    public class AudioLanguageMappingResource : RestResource
    {
        public Language Language { get; set; }
        public List<string> Aliases { get; set; }
        public bool Enabled { get; set; }
    }

    public static class AudioLanguageMappingResourceMapper
    {
        public static AudioLanguageMappingResource ToResource(this NzbDrone.Core.Profiles.AudioLanguageMappings.AudioLanguageMapping model)
        {
            if (model == null)
            {
                return null;
            }

            return new AudioLanguageMappingResource
            {
                Id = model.Id,
                Language = model.Language,
                Aliases = model.Aliases ?? new List<string>(),
                Enabled = model.Enabled
            };
        }

        public static NzbDrone.Core.Profiles.AudioLanguageMappings.AudioLanguageMapping ToModel(this AudioLanguageMappingResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new NzbDrone.Core.Profiles.AudioLanguageMappings.AudioLanguageMapping
            {
                Id = resource.Id,
                Language = resource.Language,
                Aliases = resource.Aliases ?? new List<string>(),
                Enabled = resource.Enabled
            };
        }

        public static List<AudioLanguageMappingResource> ToResource(this IEnumerable<NzbDrone.Core.Profiles.AudioLanguageMappings.AudioLanguageMapping> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
