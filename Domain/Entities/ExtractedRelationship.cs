using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ExtractedRelationship
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FromNodeId { get; set; }  // Source node ID
        public string ToNodeId { get; set; }     // Target node ID
        public string Type { get; set; }   // Relationship type (USES, DEPENDS_ON, etc.)
        public Dictionary<string, object> Properties { get; set; } = new();
        public float Confidence { get; set; }
        public string Context { get; set; }  // The text snippet that implies this relationship
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

        // For graph database compatibility
        public Dictionary<string, object> ToRelationshipProperties()
        {
            var props = new Dictionary<string, object>
            {
                ["id"] = Id,
                ["type"] = Type,
                ["confidence"] = Confidence,
                ["context"] = Context ?? "",
                ["extractedAt"] = ExtractedAt.ToString("o")
            };

            // Add custom properties
            foreach (var kvp in Properties)
            {
                props[$"prop_{kvp.Key}"] = kvp.Value;
            }

            return props;
        }
    }
}
