namespace WebAPI.Models
{
    public class BatchIngestionResult
    {
        public string BatchId { get; set; }
        public string Message { get; set; }
        public string StatusEndpoint { get; set; }
    }
}
