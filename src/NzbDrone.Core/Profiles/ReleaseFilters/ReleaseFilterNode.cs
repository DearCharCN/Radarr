using System.Collections.Generic;
using System.Text.Json;

namespace NzbDrone.Core.Profiles.ReleaseFilters
{
    public class ReleaseFilterNode
    {
        public ReleaseFilterNode()
        {
            Children = new List<ReleaseFilterNode>();
        }

        public string Type { get; set; }
        public string Mode { get; set; }
        public string Field { get; set; }
        public string Operator { get; set; }
        public JsonElement Value { get; set; }
        public List<ReleaseFilterNode> Children { get; set; }
    }
}
