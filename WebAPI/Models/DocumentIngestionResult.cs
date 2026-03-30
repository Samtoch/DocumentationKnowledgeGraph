namespace WebAPI.Models
{
    public class DocumentIngestionResult
    {
        public Guid DocumentId { get; set; }
        public string ExternalId { get; set; }
        public bool Updated { get; set; }
        public string Message { get; set; }
        public int ExtractedNodes { get; set; }
        public int ExtractedRelationships { get; set; }
        public List<string> Warnings { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
