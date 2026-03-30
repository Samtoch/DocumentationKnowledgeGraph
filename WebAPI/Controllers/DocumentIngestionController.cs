using Application.Common.DTOs;
using Application.Documents.Commands.ProcessDocument;
using Application.Documents.Queries.GetDocumentStatus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    public class DocumentIngestionController : ApiControllerBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Upload a single document page to be processed and stored in the knowledge graph
        /// </summary>
        /// <param name="request">Document page content and metadata</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Processing result with document ID and extraction summary</returns>
        /// <remarks>
        /// Sample request:
        ///
        ///     POST /api/v1/DocumentIngestion/document
        ///     {
        ///         "source": "Confluence",
        ///         "externalId": "DOC-123",
        ///         "title": "API Authentication Guide",
        ///         "content": "The .NET Core application uses OAuth2 for authentication...",
        ///         "pageNumber": 1,
        ///         "lastModified": "2024-01-15T14:30:00Z",
        ///         "metadata": {
        ///             "author": "Jane Doe",
        ///             "space": "DEV"
        ///         }
        ///     }
        /// </remarks>
        [HttpPost("document")]
        [ProducesResponseType(typeof(DocumentIngestionResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentIngestionResultDto>> UploadDocument(
            [FromBody] UploadDocumentRequest request,
            CancellationToken cancellationToken)
        {
            Logger.Info("Processing document upload: {Source}/{ExternalId} - Page {PageNumber}",
                request.Source, request.ExternalId, request.PageNumber);

            var command = new ProcessDocumentCommand(
                request.Source,
                request.ExternalId,
                request.Title,
                request.Content,
                request.PageNumber,
                request.LastModified,
                request.Metadata
            );

            var result = await Mediator.Send(command, cancellationToken);

            Logger.Info("Document processed successfully: {DocumentId} - Extracted {Nodes} nodes, {Rels} relationships",
                result.DocumentId, result.ExtractedNodes, result.ExtractedRelationships);

            return CreatedAtAction(
                nameof(GetDocumentStatus),
                new { documentId = result.DocumentId, version = "1.0" },
                result
            );
        }

        /// <summary>
        /// Upload multiple document pages in a single batch operation
        /// </summary>
        /// <param name="request">Batch of document pages to process</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Batch processing results with status for each page</returns>
        [HttpPost("documents/batch")]
        [ProducesResponseType(typeof(BatchIngestionResult), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BatchIngestionResult>> UploadDocumentBatch(
            [FromBody] BatchUploadRequest request,
            CancellationToken cancellationToken)
        {
            var batchId = Guid.NewGuid().ToString();
            Logger.Info("Processing batch upload {BatchId} with {Count} documents",
                batchId, request.Documents.Count);

            var tasks = new List<Task<DocumentIngestionResultDto>>();

            foreach (var docRequest in request.Documents)
            {
                var command = new ProcessDocumentCommand(
                    docRequest.Source,
                    docRequest.ExternalId,
                    docRequest.Title,
                    docRequest.Content,
                    docRequest.PageNumber,
                    docRequest.LastModified,
                    docRequest.Metadata
                );

                tasks.Add(Mediator.Send(command, cancellationToken));
            }

            // Process in background
            _ = Task.Run(async () =>
            {
                try
                {
                    var results = await Task.WhenAll(tasks);
                    Logger.Info("Batch {BatchId} processed successfully with {SuccessCount} successful documents",
                        batchId, results.Count(r => r.Updated));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Batch {BatchId} processing failed", batchId);
                }
            }, cancellationToken);

            return Accepted(new BatchIngestionResult
            {
                BatchId = batchId,
                Message = $"Processing {tasks.Count} documents asynchronously",
                StatusEndpoint = $"/api/v1/DocumentIngestion/batch/{batchId}/status"
            });
        }

        /// <summary>
        /// Get document processing status
        /// </summary>
        /// <param name="documentId">The document identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Document processing status</returns>
        [HttpGet("document/{documentId}/status")]
        [ProducesResponseType(typeof(DocumentStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DocumentStatusDto>> GetDocumentStatus(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            Logger.Debug("Getting status for document {DocumentId}", documentId);

            var query = new GetDocumentStatusQuery(documentId);
            var result = await Mediator.Send(query, cancellationToken);

            if (result == null)
            {
                Logger.Warn("Document {DocumentId} not found", documentId);
                return NotFound($"Document {documentId} not found");
            }

            return Ok(result);
        }

        /// <summary>
        /// Check batch processing status
        /// </summary>
        /// <param name="batchId">The batch identifier</param>
        /// <returns>Batch processing status</returns>
        [HttpGet("batch/{batchId}/status")]
        [ProducesResponseType(typeof(BatchStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<BatchStatus>> GetBatchStatus(string batchId)
        {
            Logger.Debug("Getting status for batch {BatchId}", batchId);

            // This would need a batch repository implementation
            // For now, return a placeholder
            return Ok(new BatchStatus
            {
                BatchId = batchId,
                Status = "Processing",
                Created = DateTime.UtcNow.AddMinutes(-5),
                TotalCount = 10,
                ProcessedCount = 5
            });
        }
    }
}
