using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Profiles.AudioScoring
{
    public interface IAudioScoreProfileRepository : IBasicRepository<AudioScoreProfile>
    {
        bool Exists(int id);
    }

    public class AudioScoreProfileRepository : BasicRepository<AudioScoreProfile>, IAudioScoreProfileRepository
    {
        public AudioScoreProfileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public bool Exists(int id)
        {
            return Query(p => p.Id == id).Count == 1;
        }
    }
}
