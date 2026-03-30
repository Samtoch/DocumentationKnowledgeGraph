using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities.Extraction
{
    public class ExtractedRelationship : BaseEntity
    {
        public string FromNodeId { get; private set; }
        public string ToNodeId { get; private set; }
        public string Type { get; private set; }
        public float Confidence { get; private set; }
        public string Context { get; private set; }
        public IReadOnlyDictionary<string, object> Properties => _properties.AsReadOnly();

        private readonly Dictionary<string, object> _properties;

        private ExtractedRelationship() : base()
        {
            _properties = new Dictionary<string, object>();
        }

        public ExtractedRelationship(string fromNodeId, string toNodeId, string type, float confidence, string context) : base()
        {
            FromNodeId = fromNodeId ?? throw new ArgumentNullException(nameof(fromNodeId));
            ToNodeId = toNodeId ?? throw new ArgumentNullException(nameof(toNodeId));
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Confidence = confidence;
            Context = context ?? string.Empty;
            _properties = new Dictionary<string, object>();
        }

        public void AddProperty(string key, object value)
        {
            _properties[key] = value;
        }
    }
}
