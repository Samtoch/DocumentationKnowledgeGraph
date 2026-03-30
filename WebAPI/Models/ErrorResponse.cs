namespace WebAPI.Models
{
    public class ErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string TraceId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public Dictionary<string, string[]>? Errors { get; set; }
        public string? Exception { get; set; }
        public string? StackTrace { get; set; }
    }
}
