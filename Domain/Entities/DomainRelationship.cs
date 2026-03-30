using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainRelationship
    {
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public string Context { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
