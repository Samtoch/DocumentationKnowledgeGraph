using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainTopEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int MentionCount { get; set; }
        public int DocumentCount { get; set; }
    }
}
