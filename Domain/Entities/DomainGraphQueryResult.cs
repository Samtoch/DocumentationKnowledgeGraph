using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainGraphQueryResult
    {
        public List<DomainNode> Nodes { get; set; } = new();
        public List<DomainRelationship> Relationships { get; set; } = new();
        public List<DomainDocumentReference> Documents { get; set; } = new();
    }
}
