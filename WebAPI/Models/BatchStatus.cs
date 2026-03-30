namespace WebAPI.Models
{
    public class BatchStatus
    {
        public string BatchId { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public DateTime Created { get; set; }
        public DateTime? Completed { get; set; }
        public int TotalCount { get; set; }
        public int ProcessedCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }

        public int ProgressPercentage => TotalCount > 0
            ? (int)((double)ProcessedCount / TotalCount * 100)
            : 0;
        public double? EstimatedSecondsRemaining { get; set; }

        public List<BatchDocumentResult>? Results { get; set; }

        public List<string>? Errors { get; set; }
        public BatchMetadata? Metadata { get; set; }
    }

}
