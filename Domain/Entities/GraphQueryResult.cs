using Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class GraphQueryResult
    {
        public List<object> Nodes { get; set; } = new();
        public List<object> Relationships { get; set; } = new();
        public List<DocumentReference> Documents { get; set; } = new();
    }
}
