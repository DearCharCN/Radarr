using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Profiles.ReleaseFilters
{
    public interface IReleaseFilterProfileRepository : IBasicRepository<ReleaseFilterProfile>
    {
        bool Exists(int id);
    }

    public class ReleaseFilterProfileRepository : BasicRepository<ReleaseFilterProfile>, IReleaseFilterProfileRepository
    {
        public ReleaseFilterProfileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public bool Exists(int id)
        {
            return Query(p => p.Id == id).Count == 1;
        }
    }
}
