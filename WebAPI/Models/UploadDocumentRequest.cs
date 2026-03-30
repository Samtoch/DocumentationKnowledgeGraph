namespace WebAPI.Models
{
    public class UploadDocumentRequest
    {
        public string Source { get; set; }

        /// <example>DOC-123</example>
        public string ExternalId { get; set; }

        /// <summary>Document title</summary>
        public string Title { get; set; }

        /// <summary>Page content as text</summary>
        /// <example>This document describes how to implement OAuth2 authentication...</example>
        public string Content { get; set; }

        /// <summary>Page number within the document</summary>
        /// <example>1</example>
        public int PageNumber { get; set; }

        /// <summary>Last modified timestamp from source</summary>
        /// <example>2024-01-15T14:30:00Z</example>
        public DateTime LastModified { get; set; }

        /// <summary>Additional metadata key-value pairs</summary>
        public Dictionary<string, string> Metadata { get; set; }
    }
}
