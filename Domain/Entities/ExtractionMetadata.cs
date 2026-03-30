using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ExtractionMetadata
    {
        public string DocumentSource { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string ModelVersion { get; set; } = "1.0";
        public DateTime ExtractedAt { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }
}
