using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ExtractionData
    {
        public List<NodeData> Nodes { get; set; } = new();
        public List<RelationshipData> Relationships { get; set; } = new();
    }
}
