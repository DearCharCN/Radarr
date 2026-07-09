using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Profiles.AudioLanguageMappings
{
    public interface IAudioLanguageMappingRepository : IBasicRepository<AudioLanguageMapping>
    {
    }

    public class AudioLanguageMappingRepository : BasicRepository<AudioLanguageMapping>, IAudioLanguageMappingRepository
    {
        public AudioLanguageMappingRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
