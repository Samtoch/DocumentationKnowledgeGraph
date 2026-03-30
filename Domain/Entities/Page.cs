using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Domain.Entities
{
    public class Page : BaseEntity
    {
        public int PageNumber { get; private set; }
        public string Content { get; private set; }
        public string ContentHash { get; private set; }
        public IReadOnlyCollection<ExtractedNode> Nodes => _nodes.AsReadOnly();
        public IReadOnlyCollection<ExtractedRelationship> Relationships => _relationships.AsReadOnly();

        private readonly List<ExtractedNode> _nodes;
        private readonly List<ExtractedRelationship> _relationships;

        private Page() : base()
        {
            _nodes = new List<ExtractedNode>();
            _relationships = new List<ExtractedRelationship>();
        }

        public Page(int pageNumber, string content) : base()
        {
            PageNumber = pageNumber;
            Content = content ?? throw new ArgumentNullException(nameof(content));
            ContentHash = ComputeHash(content);
            _nodes = new List<ExtractedNode>();
            _relationships = new List<ExtractedRelationship>();
        }

        public bool HasContentChanged(string newContent)
        {
            var newHash = ComputeHash(newContent);
            return newHash != ContentHash;
        }

        public void UpdateContent(string newContent)
        {
            Content = newContent;
            ContentHash = ComputeHash(newContent);
            UpdateModified();
        }

        public void AddExtractedNode(ExtractedNode node)
        {
            ArgumentNullException.ThrowIfNull(node);
            _nodes.Add(node);
        }

        public void AddExtractedNodes(IEnumerable<ExtractedNode> nodes)
        {
            _nodes.AddRange(nodes);
        }

        public void AddExtractedRelationship(ExtractedRelationship relationship)
        {
            ArgumentNullException.ThrowIfNull(relationship);
            _relationships.Add(relationship);
        }

        public void AddExtractedRelationships(IEnumerable<ExtractedRelationship> relationships)
        {
            _relationships.AddRange(relationships);
        }

        public void ClearExtractions()
        {
            _nodes.Clear();
            _relationships.Clear();
        }

        private static string ComputeHash(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
