using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class DocumentVersion
    {
        public int Version { get; set; }
        public DateTime ModifiedAt { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public int NodeCount { get; set; }
        public int RelationshipCount { get; set; }
        public List<ExtractedNode>? Nodes { get; set; }
        public List<ExtractedRelationship>? Relationships { get; set; }
    }
}
