using Domain.Entities;
using Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Interfaces
{
    public interface IGraphRepository
    {
        // Query Operations
        Task<DomainGraphQueryResult> FindRelatedAsync(
            string source,
            string entityType,
            string entityName,
            string? relationship = null,
            int depth = 2,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<DomainDocumentReference>> GetDocumentsByEntityAsync(
            string entityName,
            string? entityType = null,
            string? source = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 20,
            string sortBy = "lastModified",
            bool sortDescending = true,
            CancellationToken cancellationToken = default);

        Task<int> GetDocumentsByEntityCountAsync(
            string entityName,
            string? entityType = null,
            string? source = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default);

        Task<DomainEntityStatistics> GetEntityStatisticsAsync(CancellationToken cancellationToken = default);

        Task<DomainDocumentHistoryResult?> GetDocumentHistoryAsync(
            Guid documentId,
            bool includeExtractions = false,
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default);

        Task<DomainGraphNavigationResult?> NavigateFromEntityAsync(
            string entityId,
            List<string>? relationshipTypes = null,
            int maxDepth = 3,
            CancellationToken cancellationToken = default);

        Task<List<DomainGraphPath>> FindPathsAsync(
            string fromEntityId,
            string toEntityId,
            int maxPathLength = 5,
            CancellationToken cancellationToken = default);

        Task<DomainSearchResult<DomainEntitySummary>> GetEntitiesByTypeAsync(
            string? entityType = null,
            string? searchTerm = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        // Batch Operations
        Task<string> CreateBatchAsync(
            IEnumerable<Document> documents,
            string? initiatedBy = null,
            string? source = null,
            CancellationToken cancellationToken = default);

        Task UpdateBatchStatusAsync(
            string batchId,
            IEnumerable<DomainBatchDocumentResult> results,
            CancellationToken cancellationToken = default);

        Task<DomainBatchStatus?> GetBatchStatusAsync(
            string batchId,
            CancellationToken cancellationToken = default);

        Task<DomainSearchResult<DomainBatchSummary>> GetBatchesAsync(
            string? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        Task StoreExtractionAsync(
        Guid documentId,
        Guid pageId,
        IEnumerable<ExtractedNode> nodes,
        IEnumerable<ExtractedRelationship> relationships,
        CancellationToken cancellationToken = default);
    }

}
