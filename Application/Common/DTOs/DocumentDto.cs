using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class DocumentDto
    {
        public Guid Id { get; set; }
        public string Source { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string ProcessingStatus { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<PageDto> Pages { get; set; } = new();
    }
}
