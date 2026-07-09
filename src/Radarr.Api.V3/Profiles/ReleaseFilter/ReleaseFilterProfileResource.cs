using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Profiles.ReleaseFilters;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Profiles.ReleaseFilter
{
    public class ReleaseFilterProfileResource : RestResource
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public ReleaseFilterNode Filter { get; set; }
    }

    public static class ReleaseFilterProfileResourceMapper
    {
        public static ReleaseFilterProfileResource ToResource(this ReleaseFilterProfile model)
        {
            if (model == null)
            {
                return null;
            }

            return new ReleaseFilterProfileResource
            {
                Id = model.Id,
                Name = model.Name,
                Enabled = model.Enabled,
                Filter = DeserializeFilter(model.Filter)
            };
        }

        public static ReleaseFilterProfile ToModel(this ReleaseFilterProfileResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new ReleaseFilterProfile
            {
                Id = resource.Id,
                Name = resource.Name,
                Enabled = resource.Enabled,
                Filter = STJson.ToJson(resource.Filter ?? new ReleaseFilterNode { Type = "group", Mode = "and" })
            };
        }

        public static List<ReleaseFilterProfileResource> ToResource(this IEnumerable<ReleaseFilterProfile> models)
        {
            return models.Select(ToResource).ToList();
        }

        private static ReleaseFilterNode DeserializeFilter(string filter)
        {
            if (filter == null || !STJson.TryDeserialize<ReleaseFilterNode>(filter, out var node))
            {
                return new ReleaseFilterNode { Type = "group", Mode = "and" };
            }

            return node;
        }
    }
}
