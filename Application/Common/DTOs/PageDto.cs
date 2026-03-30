using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class PageDto
    {
        public Guid Id { get; set; }
        public int PageNumber { get; set; }
        public string ContentHash { get; set; } = string.Empty;
        public DateTime? LastModified { get; set; }
        public int NodeCount { get; set; }
        public int RelationshipCount { get; set; }
    }
}
