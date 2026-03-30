namespace WebAPI.Models
{
    public class DocumentStatus
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; }
        public string Source { get; set; }
        public string ExternalId { get; set; }
        public int PageCount { get; set; }
        public int NodeCount { get; set; }
        public int RelationshipCount { get; set; }
        public DateTime LastModified { get; set; }
        public string Status { get; set; }
    }
}
