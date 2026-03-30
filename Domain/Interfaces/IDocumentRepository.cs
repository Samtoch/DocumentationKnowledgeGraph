using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Document?> GetByExternalIdAsync(string source, string externalId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Document>> GetModifiedSinceAsync(DateTime since, CancellationToken cancellationToken = default);
        Task<Document> AddAsync(Document document, CancellationToken cancellationToken = default);
        Task UpdateAsync(Document document, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string source, string externalId, CancellationToken cancellationToken = default);

        Task<IEnumerable<Document>> GetDocumentsByFiltersAsync(
        string? source = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

        Task<int> GetTotalCountAsync(
            string? source = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? status = null,
            CancellationToken cancellationToken = default);

    }
}
