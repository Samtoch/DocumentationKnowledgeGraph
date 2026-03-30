using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class DocumentHistoryResult
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<DocumentVersion> Versions { get; set; } = new();
        public int TotalVersions { get; set; }
    }
}
