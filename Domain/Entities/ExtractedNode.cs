using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ExtractedNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; } = new();
        public float Confidence { get; set; }
        public List<string> Aliases { get; set; } = new();
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
        public string Source { get; set; }

        // For graph database compatibility
        public Dictionary<string, object> ToNodeProperties()
        {
            var props = new Dictionary<string, object>
            {
                ["id"] = Id,
                ["name"] = Name,
                ["type"] = Type,
                ["confidence"] = Confidence,
                ["extractedAt"] = ExtractedAt.ToString("o"),
                ["source"] = Source ?? "unknown"
            };

            // Add custom properties
            foreach (var kvp in Properties)
            {
                props[$"prop_{kvp.Key}"] = kvp.Value;
            }

            // Add aliases as array property
            if (Aliases.Any())
            {
                props["aliases"] = Aliases;
            }

            return props;
        }
    }
}
