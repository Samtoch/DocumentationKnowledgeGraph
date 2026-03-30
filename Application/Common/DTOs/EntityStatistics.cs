using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class EntityStatistics
    {
        public Dictionary<string, int> CountByType { get; set; } = new();
        public List<TopEntity> TopEntities { get; set; } = new();
        public int TotalEntities { get; set; }
        public int TotalRelationships { get; set; }
        public DateTime AsOf { get; set; } = DateTime.UtcNow;
    }
}
