using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class DocumentIngestionResultDto
    {
        public Guid DocumentId { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public bool Updated { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ExtractedNodes { get; set; }
        public int ExtractedRelationships { get; set; }
        public List<string> Warnings { get; set; } = new();
        public DateTime ProcessedAt { get; set; }
    }

}
