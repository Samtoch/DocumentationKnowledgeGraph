using Application.Common.DTOs;
using Application.Documents.Queries.GetDocumentHistory;
using Application.Documents.Queries.FindRelated;
using Application.Documents.Queries.GetEntityStatistics;
using Application.Documents.Queries.SearchByEntity;
using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using WebAPI.Models;

namespace WebAPI.Controllers
{
    /// <summary>
    /// Handles knowledge graph query operations
    /// </summary>
    [ApiVersion("1.0")]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class QueryController : ApiControllerBase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IGraphRepository _graphRepository;
        private readonly IDocumentRepository _documentRepository;

        public QueryController(
            IGraphRepository graphRepository,
            IDocumentRepository documentRepository)
        {
            _graphRepository = graphRepository;
            _documentRepository = documentRepository;
        }

        #region Entity Queries

        /// <summary>
        /// Find related documents based on entity and relationship criteria
        /// </summary>
        /// <param name="source">The source system (Confluence, Jira, AzureBoard)</param>
        /// <param name="entityType">Type of entity to search for (Technology, Person, Project)</param>
        /// <param name="entityName">Name of the entity</param>
        /// <param name="relationship">Optional relationship type to filter by</param>
        /// <param name="depth">Graph traversal depth (default: 2, max: 5)</param>
        /// <param name="includeDocuments">Include document details in results</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Graph of related documents and entities</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/v1/Query/related?source=Confluence&amp;entityType=Technology&amp;entityName=.NET&amp;depth=2
        /// 
        /// Returns a graph showing all entities related to .NET and documents that mention them.
        /// </remarks>
        [HttpGet("related")]
        [ProducesResponseType(typeof(GraphQueryResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<ActionResult<GraphQueryResultDto>> FindRelated(
            [FromQuery] string source,
            [FromQuery] string entityType,
            [FromQuery] string entityName,
            [FromQuery] string? relationship = null,
            [FromQuery] int depth = 2,
            [FromQuery] bool includeDocuments = true,
            CancellationToken cancellationToken = default)
        {
            // Validate depth
            if (depth < 1 || depth > 5)
            {
                return BadRequest(new ErrorResponse
                {
                    StatusCode = 400,
                    Message = "Depth must be between 1 and 5",
                    TraceId = HttpContext.TraceIdentifier,
                    Timestamp = DateTime.UtcNow,
                    Path = Request.Path,
                    Method = Request.Method
                });
            }

            Logger.Debug("Finding related documents for {EntityType}:{EntityName} in {Source} with depth {Depth}",
                entityType, entityName, source, depth);

            var query = new FindRelatedQuery(source, entityType, entityName, depth, relationship);
            //var query = new FindRelatedQuery(source, entityType, entityName, relationship);
            var result = await Mediator.Send(query, cancellationToken);

            // Filter out documents if not requested
            if (!includeDocuments)
            {
                result.Documents = new List<DocumentReference>();
            }

            Logger.Info("Found {NodeCount} nodes and {RelCount} relationships for query. Document count: {DocCount}",
                result.Nodes.Count, result.Relationships.Count, result.Documents.Count);

            return Ok(result);
        }

        /// <summary>
        /// Search for documents by entity name with advanced filtering
        /// </summary>
        /// <param name="entityName">Name of the entity to search for (supports partial matching)</param>
        /// <param name="entityType">Optional entity type filter</param>
        /// <param name="source">Optional source system filter</param>
        /// <param name="fromDate">Filter documents modified after this date</param>
        /// <param name="toDate">Filter documents modified before this date</param>
        /// <param name="page">Page number for pagination (default: 1)</param>
        /// <param name="pageSize">Page size for pagination (default: 20, max: 100)</param>
        /// <param name="sortBy">Sort field (title, source, lastModified)</param>
        /// <param name="sortDescending">Sort descending</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Paginated list of documents containing the entity</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/v1/Query/search?entityName=authentication&amp;entityType=Concept&amp;source=Confluence&amp;page=1&amp;pageSize=10
        /// 
        /// Returns documents that mention authentication concepts from Confluence.
        /// </remarks>
        [HttpGet("search")]
        [ProducesResponseType(typeof(SearchResult<DocumentReference>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SearchResult<DocumentReference>>> SearchByEntity(
            [FromQuery] string entityName,
            [FromQuery] string? entityType = null,
            [FromQuery] string? source = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sortBy = "lastModified",
            [FromQuery] bool sortDescending = true,
            CancellationToken cancellationToken = default)
        {
            // Validate pagination
            if (page < 1)
            {
                return BadRequest(new ErrorResponse
                {
                    StatusCode = 400,
                    Message = "Page must be greater than 0",
                    TraceId = HttpContext.TraceIdentifier,
                    Timestamp = DateTime.UtcNow,
                    Path = Request.Path,
                    Method = Request.Method
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ErrorResponse
                {
                    StatusCode = 400,
                    Message = "PageSize must be between 1 and 100",
                    TraceId = HttpContext.TraceIdentifier,
                    Timestamp = DateTime.UtcNow,
                    Path = Request.Path,
                    Method = Request.Method
                });
            }

            Logger.Debug("Searching documents for entity '{EntityName}' with filters: Type={EntityType}, Source={Source}",
                entityName, entityType ?? "any", source ?? "any");

            var query = new SearchByEntityQuery(
                entityName,
                entityType,
                source,
                fromDate,
                toDate,
                page,
                pageSize,
                sortBy,
                sortDescending);

            var result = await Mediator.Send(query, cancellationToken);

            Logger.Info("Search returned {Count} results for entity '{EntityName}' (Page {Page}/{TotalPages})",
                result.Items.Count, entityName, result.Page, result.TotalPages);

            return Ok(result);
        }

        /// <summary>
        /// Get statistics about entities in the knowledge graph
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entity statistics including counts by type and most frequent entities</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/v1/Query/statistics
        /// 
        /// Returns statistics about the knowledge graph including:
        /// - Total entities by type
        /// - Most frequently mentioned entities
        /// - Growth trends
        /// </remarks>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(EntityStatistics), StatusCodes.Status200OK)]
        public async Task<ActionResult<EntityStatistics>> GetStatistics(CancellationToken cancellationToken)
        {
            Logger.Debug("Retrieving entity statistics");

            var query = new GetEntityStatisticsQuery();
            var result = await Mediator.Send(query, cancellationToken);

            return Ok(result);
        }

        #endregion

        #region Document Queries

        /// <summary>
        /// Get document change history
        /// </summary>
        /// <param name="documentId">The document identifier</param>
        /// <param name="includeExtractions">Include extracted entities in history</param>
        /// <param name="page">Page number for pagination</param>
        /// <param name="pageSize">Page size for pagination</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Document change history with versions</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/v1/Query/document/123e4567-e89b-12d3-a456-426614174000/history
        /// 
        /// Returns the change history of a document showing how extracted entities evolved over time.
        /// </remarks>
        [HttpGet("document/{documentId}/history")]
        [ProducesResponseType(typeof(DocumentHistoryResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<DocumentHistoryResult>> GetDocumentHistory(
            Guid documentId,
            [FromQuery] bool includeExtractions = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug("Retrieving history for document {DocumentId}", documentId);

            var query = new GetDocumentHistoryQuery(documentId, includeExtractions, page, pageSize);
            var result = await Mediator.Send(query, cancellationToken);

            if (result == null)
            {
                Logger.Warn("Document {DocumentId} not found", documentId);
                return NotFound($"Document {documentId} not found");
            }

            return Ok(result);
        }

        /// <summary>
        /// Get documents by source and date range
        /// </summary>
        /// <param name="source">Source system filter</param>
        /// <param name="fromDate">Start date</param>
        /// <param name="toDate">End date</param>
        /// <param name="status">Processing status filter</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of documents matching criteria</returns>
        [HttpGet("documents")]
        [ProducesResponseType(typeof(SearchResult<DocumentSummary>), StatusCodes.Status200OK)]
        public async Task<ActionResult<SearchResult<DocumentSummary>>> GetDocuments(
            [FromQuery] string? source = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug("Retrieving documents with filters: Source={Source}, Status={Status}",
                source ?? "any", status ?? "any");

            // Implementation would use document repository
            var documents = await _documentRepository.GetDocumentsByFiltersAsync(
                source, fromDate, toDate, status, page, pageSize, cancellationToken);

            var totalCount = await _documentRepository.GetTotalCountAsync(source, fromDate, toDate, status, cancellationToken);

            var result = new SearchResult<DocumentSummary>
            {
                Items = documents.Select(d => new DocumentSummary
                {
                    Id = d.Id,
                    Title = d.Title,
                    Source = d.Source,
                    ExternalId = d.ExternalId,
                    LastModified = d.LastModified,
                    PageCount = d.Pages.Count,
                    NodeCount = d.Pages.Sum(p => p.Nodes.Count),
                    RelationshipCount = d.Pages.Sum(p => p.Relationships.Count),
                    Status = d.ProcessingStatus
                }).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(result);
        }

        #endregion

        #region Graph Navigation

        /// <summary>
        /// Navigate the graph from a starting entity
        /// </summary>
        /// <param name="entityId">Starting entity ID</param>
        /// <param name="relationshipTypes">Optional relationship types to follow</param>
        /// <param name="maxDepth">Maximum traversal depth</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Subgraph starting from the specified entity</returns>
        [HttpGet("navigate/{entityId}")]
        [ProducesResponseType(typeof(GraphNavigationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<GraphNavigationResult>> NavigateGraph(
            string entityId,
            [FromQuery] List<string>? relationshipTypes = null,
            [FromQuery] int maxDepth = 3,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug("Navigating graph from entity {EntityId} with depth {Depth}", entityId, maxDepth);

            // Implementation would use graph repository
            var result = await _graphRepository.NavigateFromEntityAsync(
                entityId, relationshipTypes, maxDepth, cancellationToken);

            if (result == null)
            {
                return NotFound($"Entity {entityId} not found");
            }

            return Ok(result);
        }

        /// <summary>
        /// Find paths between two entities
        /// </summary>
        /// <param name="fromEntityId">Starting entity ID</param>
        /// <param name="toEntityId">Target entity ID</param>
        /// <param name="maxPathLength">Maximum path length</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>All paths between the two entities</returns>
        [HttpGet("paths")]
        [ProducesResponseType(typeof(List<GraphPath>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<GraphPath>>> FindPaths(
            [FromQuery] string fromEntityId,
            [FromQuery] string toEntityId,
            [FromQuery] int maxPathLength = 5,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug("Finding paths from {FromEntity} to {ToEntity}", fromEntityId, toEntityId);

            var paths = await _graphRepository.FindPathsAsync(
                fromEntityId, toEntityId, maxPathLength, cancellationToken);

            return Ok(paths);
        }

        #endregion

        #region Batch Status

        /// <summary>
        /// Check batch processing status
        /// </summary>
        /// <param name="batchId">The batch identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Batch processing status with detailed results</returns>
        /// <remarks>
        /// Sample request:
        /// 
        ///     GET /api/v1/Query/batch/batch_123e4567-e89b-12d3-a456-426614174000/status
        /// 
        /// Returns the current status of a batch processing operation including:
        /// - Overall progress
        /// - Individual document results
        /// - Processing metrics
        /// </remarks>
        [HttpGet("batch/{batchId}/status")]
        [ProducesResponseType(typeof(BatchStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<BatchStatus>> GetBatchStatus(
            string batchId,
            CancellationToken cancellationToken = default)
        {
            // Validate batch ID format
            if (string.IsNullOrWhiteSpace(batchId))
            {
                return BadRequest(new ErrorResponse
                {
                    StatusCode = 400,
                    Message = "Batch ID cannot be empty",
                    TraceId = HttpContext.TraceIdentifier,
                    Timestamp = DateTime.UtcNow,
                    Path = Request.Path,
                    Method = Request.Method
                });
            }

            Logger.Debug("Getting status for batch {BatchId}", batchId);

            // In a real implementation, this would come from a batch repository
            // For now, return mock data for demonstration
            var batchStatus = await GetMockBatchStatus(batchId);

            if (batchStatus == null)
            {
                Logger.Warn("Batch {BatchId} not found", batchId);
                return NotFound($"Batch {batchId} not found");
            }

            // Calculate progress metrics
            batchStatus.SuccessCount = batchStatus.Results?.Count(r => r.Error == null) ?? 0;
            batchStatus.FailedCount = batchStatus.Results?.Count(r => r.Error != null) ?? 0;

            // Calculate estimated time remaining
            if (batchStatus.ProcessedCount > 0 && batchStatus.TotalCount > batchStatus.ProcessedCount)
            {
                var elapsedSeconds = (DateTime.UtcNow - batchStatus.Created).TotalSeconds;
                var avgSecondsPerItem = elapsedSeconds / batchStatus.ProcessedCount;
                batchStatus.EstimatedSecondsRemaining = avgSecondsPerItem * (batchStatus.TotalCount - batchStatus.ProcessedCount);
            }

            Logger.Info("Batch {BatchId} status: {Status} - Progress: {Progress}% ({Processed}/{Total})",
                batchId, batchStatus.Status, batchStatus.ProgressPercentage,
                batchStatus.ProcessedCount, batchStatus.TotalCount);

            return Ok(batchStatus);
        }

        /// <summary>
        /// Get all batches with optional filters
        /// </summary>
        /// <param name="status">Filter by status</param>
        /// <param name="fromDate">Filter by creation date</param>
        /// <param name="toDate">Filter by creation date</param>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of batches matching criteria</returns>
        [HttpGet("batches")]
        [ProducesResponseType(typeof(SearchResult<BatchSummary>), StatusCodes.Status200OK)]
        public async Task<ActionResult<SearchResult<BatchSummary>>> GetBatches(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            Logger.Debug("Retrieving batches with filters: Status={Status}", status ?? "any");

            // Mock implementation - would use batch repository
            var batches = new List<BatchSummary>
        {
            new BatchSummary
            {
                BatchId = "batch_123",
                Status = "Completed",
                Created = DateTime.UtcNow.AddHours(-2),
                Completed = DateTime.UtcNow.AddHours(-1),
                TotalCount = 100,
                ProcessedCount = 100,
                SuccessCount = 98,
                FailedCount = 2
            },
            new BatchSummary
            {
                BatchId = "batch_456",
                Status = "Processing",
                Created = DateTime.UtcNow.AddMinutes(-30),
                TotalCount = 50,
                ProcessedCount = 25,
                SuccessCount = 24,
                FailedCount = 1
            }
        };

            var result = new SearchResult<BatchSummary>
            {
                Items = batches,
                Page = page,
                PageSize = pageSize,
                TotalCount = batches.Count,
                TotalPages = 1
            };

            return Ok(result);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Mock implementation for batch status - replace with actual repository call
        /// </summary>
        private async Task<BatchStatus?> GetMockBatchStatus(string batchId)
        {
            // Simulate async operation
            await Task.Delay(10);

            // Return mock data based on batch ID pattern
            return batchId switch
            {
                "batch_completed" => new BatchStatus
                {
                    BatchId = batchId,
                    Status = "Completed",
                    Created = DateTime.UtcNow.AddHours(-1),
                    Completed = DateTime.UtcNow,
                    TotalCount = 10,
                    ProcessedCount = 10,
                    Results = Enumerable.Range(1, 10).Select(i => new BatchDocumentResult
                    {
                        DocumentId = Guid.NewGuid(),
                        ExternalId = $"DOC-{i}",
                        Title = $"Document {i}",
                        PageNumber = 1,
                        Updated = true,
                        Message = "Processed successfully",
                        ExtractedNodes = Random.Shared.Next(5, 20),
                        ExtractedRelationships = Random.Shared.Next(2, 10),
                        ProcessedAt = DateTime.UtcNow.AddMinutes(-30 + i)
                    }).ToList(),
                    Metadata = new BatchMetadata
                    {
                        InitiatedBy = "system",
                        Source = "Confluence",
                        StartTime = DateTime.UtcNow.AddHours(-1),
                        EndTime = DateTime.UtcNow,
                        TotalProcessingTimeMs = 3600000,
                        AverageProcessingTimeMs = 360000,
                        CustomData = new Dictionary<string, string>
                        {
                            ["source"] = "Confluence",
                            ["space"] = "DEV"
                        }
                    }
                },
                "batch_processing" => new BatchStatus
                {
                    BatchId = batchId,
                    Status = "Processing",
                    Created = DateTime.UtcNow.AddMinutes(-5),
                    TotalCount = 10,
                    ProcessedCount = 4,
                    Results = Enumerable.Range(1, 4).Select(i => new BatchDocumentResult
                    {
                        DocumentId = Guid.NewGuid(),
                        ExternalId = $"DOC-{i}",
                        Title = $"Document {i}",
                        PageNumber = 1,
                        Updated = true,
                        Message = "Processed successfully",
                        ExtractedNodes = Random.Shared.Next(5, 20),
                        ExtractedRelationships = Random.Shared.Next(2, 10),
                        ProcessedAt = DateTime.UtcNow.AddMinutes(-5 + i)
                    }).ToList(),
                    Metadata = new BatchMetadata
                    {
                        InitiatedBy = "user",
                        Source = "Jira",
                        StartTime = DateTime.UtcNow.AddMinutes(-5)
                    }
                },
                "batch_failed" => new BatchStatus
                {
                    BatchId = batchId,
                    Status = "Failed",
                    Created = DateTime.UtcNow.AddHours(-2),
                    Completed = DateTime.UtcNow.AddHours(-2).AddMinutes(5),
                    TotalCount = 5,
                    ProcessedCount = 2,
                    Errors = new List<string> { "Database connection error", "Timeout processing document 3" },
                    Results = new List<BatchDocumentResult>
                {
                    new BatchDocumentResult
                    {
                        DocumentId = Guid.NewGuid(),
                        ExternalId = "DOC-1",
                        Title = "Document 1",
                        PageNumber = 1,
                        Updated = true,
                        Message = "Processed successfully",
                        ExtractedNodes = 15,
                        ExtractedRelationships = 8,
                        ProcessedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(1)
                    },
                    new BatchDocumentResult
                    {
                        DocumentId = Guid.NewGuid(),
                        ExternalId = "DOC-2",
                        Title = "Document 2",
                        PageNumber = 1,
                        Updated = true,
                        Message = "Processed successfully",
                        ExtractedNodes = 12,
                        ExtractedRelationships = 6,
                        ProcessedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(3)
                    },
                    new BatchDocumentResult
                    {
                        DocumentId = Guid.NewGuid(),
                        ExternalId = "DOC-3",
                        Title = "Document 3",
                        PageNumber = 1,
                        Updated = false,
                        Message = "Processing failed",
                        Error = "Timeout after 30 seconds",
                        ProcessedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(4)
                    }
                }
                },
                _ => null
            };
        }

        #endregion
    }
}
