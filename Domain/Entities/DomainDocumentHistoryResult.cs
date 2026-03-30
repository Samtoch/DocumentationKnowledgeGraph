using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainDocumentHistoryResult
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public List<DomainDocumentVersion> Versions { get; set; } = new();
        public int TotalVersions { get; set; }
    }

}
