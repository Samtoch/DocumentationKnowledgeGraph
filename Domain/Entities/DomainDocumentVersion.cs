using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainDocumentVersion
    {
        public int Version { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public int NodeCount { get; set; }
        public int RelationshipCount { get; set; }
        public List<DomainNode>? Nodes { get; set; }
        public List<DomainRelationship>? Relationships { get; set; }
    }
}
