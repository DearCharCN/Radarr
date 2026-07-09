using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Profiles.AudioPreferences
{
    public interface IAudioLanguagePreferenceRepository : IBasicRepository<AudioLanguagePreference>
    {
        bool Exists(int id);
    }

    public class AudioLanguagePreferenceRepository : BasicRepository<AudioLanguagePreference>, IAudioLanguagePreferenceRepository
    {
        public AudioLanguagePreferenceRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public bool Exists(int id)
        {
            return Query(p => p.Id == id).Count == 1;
        }
    }
}
