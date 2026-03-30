using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class DocumentReference
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
    }
}
