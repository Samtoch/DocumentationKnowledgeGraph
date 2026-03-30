using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class GraphPath
    {
        public List<string> NodeIds { get; set; } = new();
        public List<string> RelationshipTypes { get; set; } = new();
        public int Length { get; set; }
        public double Weight { get; set; }
    }
}
