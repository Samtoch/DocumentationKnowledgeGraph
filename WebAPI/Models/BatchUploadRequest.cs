namespace WebAPI.Models
{
    public class BatchUploadRequest
    {
        public List<UploadDocumentRequest> Documents { get; set; }
        public bool ProcessInParallel { get; set; } = true;
    }
}
