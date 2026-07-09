using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.CustomFormats
{
    public interface ICustomFormatMutexGroupService
    {
        CustomFormatMutexGroup Add(CustomFormatMutexGroup group);
        List<CustomFormatMutexGroup> All();
        void Delete(int id);
        bool Exists(int id);
        CustomFormatMutexGroup Get(int id);
        List<CustomFormatMutexGroup> GetMany(IEnumerable<int> ids);
        CustomFormatMutexGroup Update(CustomFormatMutexGroup group);
    }

    public class CustomFormatMutexGroupService : ICustomFormatMutexGroupService
    {
        private readonly ICustomFormatMutexGroupRepository _repo;

        public CustomFormatMutexGroupService(ICustomFormatMutexGroupRepository repo)
        {
            _repo = repo;
        }

        public CustomFormatMutexGroup Add(CustomFormatMutexGroup group)
        {
            return _repo.Insert(group);
        }

        public CustomFormatMutexGroup Update(CustomFormatMutexGroup group)
        {
            return _repo.Update(group);
        }

        public void Delete(int id)
        {
            _repo.Delete(id);
        }

        public bool Exists(int id)
        {
            return _repo.Exists(id);
        }

        public CustomFormatMutexGroup Get(int id)
        {
            return _repo.Get(id);
        }

        public List<CustomFormatMutexGroup> GetMany(IEnumerable<int> ids)
        {
            var wanted = ids?.ToHashSet() ?? new HashSet<int>();

            if (!wanted.Any())
            {
                return new List<CustomFormatMutexGroup>();
            }

            return All().Where(group => wanted.Contains(group.Id)).ToList();
        }

        public List<CustomFormatMutexGroup> All()
        {
            return _repo.All().ToList();
        }
    }
}
