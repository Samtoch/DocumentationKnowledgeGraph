using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainGraphPath
    {
        public List<string> NodeIds { get; set; } = new();
        public List<string> RelationshipTypes { get; set; } = new();
        public int Length { get; set; }
        public double Weight { get; set; }
    }
}
