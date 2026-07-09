using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Profiles.ReleaseFilters;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Profiles.ReleaseFilter
{
    public class ReleaseFilterProfileResource : RestResource
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public ReleaseFilterNode Filter { get; set; }
    }

    public static class ReleaseFilterProfileResourceMapper
    {
        public static ReleaseFilterProfileResource ToResource(this ReleaseFilterProfile model)
        {
            if (model == null)
            {
                return null;
            }

            return new ReleaseFilterProfileResource
            {
                Id = model.Id,
                Name = model.Name,
                Enabled = model.Enabled,
                Filter = DeserializeFilter(model.Filter)
            };
        }

        public static ReleaseFilterProfile ToModel(this ReleaseFilterProfileResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new ReleaseFilterProfile
            {
                Id = resource.Id,
                Name = resource.Name,
                Enabled = resource.Enabled,
                Filter = SerializeFilter(resource.Filter ?? new ReleaseFilterNode { Type = "group", Mode = "and" })
            };
        }

        public static List<ReleaseFilterProfileResource> ToResource(this IEnumerable<ReleaseFilterProfile> models)
        {
            return models.Select(ToResource).ToList();
        }

        private static ReleaseFilterNode DeserializeFilter(string filter)
        {
            if (filter == null || !STJson.TryDeserialize<ReleaseFilterNode>(filter, out var node))
            {
                return new ReleaseFilterNode { Type = "group", Mode = "and" };
            }

            return node;
        }

        private static string SerializeFilter(ReleaseFilterNode node)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteFilterNode(writer, node);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteFilterNode(Utf8JsonWriter writer, ReleaseFilterNode node)
        {
            node ??= new ReleaseFilterNode { Type = "group", Mode = "and" };

            writer.WriteStartObject();

            if (!string.IsNullOrWhiteSpace(node.Type))
            {
                writer.WriteString("type", node.Type);
            }

            if (!string.IsNullOrWhiteSpace(node.Mode))
            {
                writer.WriteString("mode", node.Mode);
            }

            if (!string.IsNullOrWhiteSpace(node.Field))
            {
                writer.WriteString("field", node.Field);
            }

            if (!string.IsNullOrWhiteSpace(node.Operator))
            {
                writer.WriteString("operator", node.Operator);
            }

            if (node.Value.HasValue && node.Value.Value.ValueKind != JsonValueKind.Undefined)
            {
                writer.WritePropertyName("value");
                node.Value.Value.WriteTo(writer);
            }

            if (node.Children != null && (node.Children.Any() || node.Type == "group"))
            {
                writer.WritePropertyName("children");
                writer.WriteStartArray();

                foreach (var child in node.Children)
                {
                    WriteFilterNode(writer, child);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
    }
}
