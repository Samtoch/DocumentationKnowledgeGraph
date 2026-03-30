namespace WebAPI.Models
{
    public class BatchDocumentResult
    {
        public Guid DocumentId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public bool Updated { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ExtractedNodes { get; set; }
        public int ExtractedRelationships { get; set; }
        public DateTime ProcessedAt { get; set; }
        public List<string>? Warnings { get; set; }
        public string? Error { get; set; }

    }
}
