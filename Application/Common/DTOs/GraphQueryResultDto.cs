using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    public class GraphQueryResultDto
    {
        public List<object> Nodes { get; set; } = new();
        public List<object> Relationships { get; set; } = new();
        public List<DocumentReference> Documents { get; set; } = new();
    }
}
