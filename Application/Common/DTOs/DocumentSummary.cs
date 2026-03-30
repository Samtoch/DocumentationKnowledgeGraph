using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class DocumentSummary
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public int PageCount { get; set; }
        public int NodeCount { get; set; }
        public int RelationshipCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
