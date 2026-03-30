using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.ValueObjects
{
    //public class ExtractionResult
    //{
    //    public List<ExtractedNode> Nodes { get; set; } = new();
    //    public List<ExtractedRelationship> Relationships { get; set; } = new();
    //    public ExtractionMetadata Metadata { get; set; } = new();
    //    public List<string> Warnings { get; set; } = new();
    //}

    public class ExtractionMetadata
    {
        public string DocumentSource { get; set; }
        public Guid DocumentId { get; set; }
        public int PageNumber { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string ModelVersion { get; set; } = "1.0";
        public int TokenCount { get; set; }
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}
