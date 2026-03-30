using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.DTOs
{
    /// <summary>
    /// Represents the result of a document ingestion operation
    /// </summary>
    public class DocumentIngestionResult
    {
        /// <summary>
        /// Unique identifier of the processed document
        /// </summary>
        public Guid DocumentId { get; set; }

        /// <summary>
        /// External identifier from the source system
        /// </summary>
        public string ExternalId { get; set; } = string.Empty;

        /// <summary>
        /// Title of the document
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Page number that was processed
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Indicates whether the document was updated (true) or created (false)
        /// </summary>
        public bool Updated { get; set; }

        /// <summary>
        /// Status message describing the result
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Number of nodes extracted from the document
        /// </summary>
        public int ExtractedNodes { get; set; }

        /// <summary>
        /// Number of relationships extracted from the document
        /// </summary>
        public int ExtractedRelationships { get; set; }

        /// <summary>
        /// List of warnings encountered during processing
        /// </summary>
        public List<string>? Warnings { get; set; }

        /// <summary>
        /// Timestamp when the document was processed
        /// </summary>
        public DateTime ProcessedAt { get; set; }

        /// <summary>
        /// Processing duration in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Indicates if the processing was successful
        /// </summary>
        public bool IsSuccess => string.IsNullOrEmpty(Error);

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Additional metadata from processing
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
