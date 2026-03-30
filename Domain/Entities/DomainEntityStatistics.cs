using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class DomainEntityStatistics
    {
        public Dictionary<string, int> CountByType { get; set; } = new();
        public List<DomainTopEntity> TopEntities { get; set; } = new();
        public int TotalEntities { get; set; }
        public int TotalRelationships { get; set; }
        public DateTime AsOf { get; set; }
    }
}
