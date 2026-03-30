using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class RelationshipData
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public float Confidence { get; set; } = 0.8f;
        public string Context { get; set; } = string.Empty;
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
