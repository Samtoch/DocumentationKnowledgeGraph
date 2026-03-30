using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class GraphNavigationResult
    {
        public string StartEntityId { get; set; } = string.Empty;
        public List<object> Nodes { get; set; } = new();
        public List<object> Relationships { get; set; } = new();
        public int Depth { get; set; }
    }
}
