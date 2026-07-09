using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.CustomFormats
{
    public class CustomFormatMutexGroup : ModelBase
    {
        public CustomFormatMutexGroup()
        {
            Enabled = true;
            CustomFormatIds = new List<int>();
        }

        public string Name { get; set; }
        public bool Enabled { get; set; }
        public List<int> CustomFormatIds { get; set; }
    }
}
