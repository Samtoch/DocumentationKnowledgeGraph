using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainBatchMetadata
    {
        public string? InitiatedBy { get; set; }
        public string? Source { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long? TotalProcessingTimeMs { get; set; }
        public double? AverageProcessingTimeMs { get; set; }
        public Dictionary<string, string>? CustomData { get; set; }
    }
}
