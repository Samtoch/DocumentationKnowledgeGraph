using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<string> Aliases { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public int SeenCount { get; set; }
    }

}
