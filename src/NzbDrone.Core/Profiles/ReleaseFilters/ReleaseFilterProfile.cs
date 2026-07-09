using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Profiles.ReleaseFilters
{
    public class ReleaseFilterProfile : ModelBase
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Filter { get; set; }
    }
}
