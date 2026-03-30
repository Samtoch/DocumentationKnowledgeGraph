using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainEntitySummary
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int MentionCount { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
    }
}
