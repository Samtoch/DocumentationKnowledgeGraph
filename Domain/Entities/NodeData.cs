using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class NodeData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public float Confidence { get; set; } = 0.8f;
        public Dictionary<string, object> Properties { get; set; } = new();
        public List<string> Aliases { get; set; } = new();
    }
}
