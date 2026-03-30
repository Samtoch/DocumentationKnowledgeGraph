using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ExtractionResult
    {
        public List<ExtractedNode> Nodes { get; set; } = new();
        public List<ExtractedRelationship> Relationships { get; set; } = new();
        public ExtractionMetadata Metadata { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

}
