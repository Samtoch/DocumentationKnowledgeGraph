using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class ExtractionValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
