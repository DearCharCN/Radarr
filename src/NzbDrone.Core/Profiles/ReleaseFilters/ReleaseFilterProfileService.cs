using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Profiles.ReleaseFilters
{
    public interface IReleaseFilterProfileService
    {
        ReleaseFilterProfile Add(ReleaseFilterProfile profile);
        List<ReleaseFilterProfile> All();
        void Delete(int id);
        bool Exists(int id);
        ReleaseFilterProfile Get(int id);
        ReleaseFilterProfile Update(ReleaseFilterProfile profile);
    }

    public class ReleaseFilterProfileService : IReleaseFilterProfileService
    {
        private readonly IReleaseFilterProfileRepository _repo;

        public ReleaseFilterProfileService(IReleaseFilterProfileRepository repo)
        {
            _repo = repo;
        }

        public ReleaseFilterProfile Add(ReleaseFilterProfile profile)
        {
            return _repo.Insert(profile);
        }

        public ReleaseFilterProfile Update(ReleaseFilterProfile profile)
        {
            return _repo.Update(profile);
        }

        public void Delete(int id)
        {
            _repo.Delete(id);
        }

        public bool Exists(int id)
        {
            return _repo.Exists(id);
        }

        public ReleaseFilterProfile Get(int id)
        {
            return _repo.Get(id);
        }

        public List<ReleaseFilterProfile> All()
        {
            return _repo.All().ToList();
        }
    }
}
