using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.CustomFormats
{
    public interface ICustomFormatMutexGroupRepository : IBasicRepository<CustomFormatMutexGroup>
    {
        bool Exists(int id);
    }

    public class CustomFormatMutexGroupRepository : BasicRepository<CustomFormatMutexGroup>, ICustomFormatMutexGroupRepository
    {
        public CustomFormatMutexGroupRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public bool Exists(int id)
        {
            return Query(p => p.Id == id).Count == 1;
        }
    }
}
