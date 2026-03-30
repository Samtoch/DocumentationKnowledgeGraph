using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class TopEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int MentionCount { get; set; }
        public int DocumentCount { get; set; }
    }
}
