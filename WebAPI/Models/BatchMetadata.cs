namespace WebAPI.Models
{
    public class BatchMetadata
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
