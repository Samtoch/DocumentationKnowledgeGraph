using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Document : BaseEntity
    {
        public string Source { get; private set; }
        public string ExternalId { get; private set; }
        public string Title { get; private set; }
        public DateTime LastModified { get; private set; }
        public string ProcessingStatus { get; private set; }
        public IReadOnlyDictionary<string, string> Metadata => _metadata.AsReadOnly();
        public IReadOnlyCollection<Page> Pages => _pages.AsReadOnly();

        private readonly Dictionary<string, string> _metadata;
        private readonly List<Page> _pages;

        private Document() : base() // For EF Core/ORM
        {
            _metadata = new Dictionary<string, string>();
            _pages = new List<Page>();
        }

        public Document(string source, string externalId, string title, DateTime lastModified) : base()
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ExternalId = externalId ?? throw new ArgumentNullException(nameof(externalId));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            LastModified = lastModified;
            ProcessingStatus = "Pending";
            _metadata = new Dictionary<string, string>();
            _pages = new List<Page>();
        }

        public Result<Page> AddPage(int pageNumber, string content)
        {
            if (_pages.Any(p => p.PageNumber == pageNumber))
                return Result<Page>.Failure($"Page {pageNumber} already exists");

            var page = new Page(pageNumber, content);
            _pages.Add(page);
            return Result<Page>.Success(page);
        }

        public Result UpdateMetadata(string title, DateTime lastModified)
        {
            Title = title;
            LastModified = lastModified;
            UpdateModified();
            return Result.Success();
        }

        public void AddMetadata(string key, string value)
        {
            _metadata[key] = value;
        }

        public void MarkAsProcessed()
        {
            ProcessingStatus = "Processed";
            UpdateModified();
        }

        public void MarkAsFailed(string error)
        {
            ProcessingStatus = "Failed";
            AddMetadata("error", error);
            UpdateModified();
        }
    }
}
