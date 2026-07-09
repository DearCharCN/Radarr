using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.CustomFormats;
using Radarr.Http.REST;

namespace Radarr.Api.V3.CustomFormats
{
    public class CustomFormatMutexGroupResource : RestResource
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public List<int> CustomFormatIds { get; set; }
    }

    public static class CustomFormatMutexGroupResourceMapper
    {
        public static CustomFormatMutexGroupResource ToResource(this CustomFormatMutexGroup model)
        {
            if (model == null)
            {
                return null;
            }

            return new CustomFormatMutexGroupResource
            {
                Id = model.Id,
                Name = model.Name,
                Enabled = model.Enabled,
                CustomFormatIds = model.CustomFormatIds ?? new List<int>()
            };
        }

        public static CustomFormatMutexGroup ToModel(this CustomFormatMutexGroupResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new CustomFormatMutexGroup
            {
                Id = resource.Id,
                Name = resource.Name,
                Enabled = resource.Enabled,
                CustomFormatIds = resource.CustomFormatIds ?? new List<int>()
            };
        }

        public static List<CustomFormatMutexGroupResource> ToResource(this IEnumerable<CustomFormatMutexGroup> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
