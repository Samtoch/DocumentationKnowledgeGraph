using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainBatchSummary
    {
        public string BatchId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime? Completed { get; set; }
        public int TotalCount { get; set; }
        public int ProcessedCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int ProgressPercentage => TotalCount > 0
            ? (int)((double)ProcessedCount / TotalCount * 100)
            : 0;
    }
}
