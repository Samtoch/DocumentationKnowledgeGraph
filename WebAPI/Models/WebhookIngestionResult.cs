namespace WebAPI.Models
{
    public class WebhookIngestionResult
    {
        public string Source { get; set; }
        public int ProcessedCount { get; set; }
        public List<DocumentIngestionResult> Results { get; set; }
        public DateTime WebhookReceivedAt { get; set; }
    }
}
