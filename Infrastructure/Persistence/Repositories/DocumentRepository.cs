using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Persistence.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly INeo4jContext _context;
        private readonly ILogger<DocumentRepository> _logger;

        public DocumentRepository(INeo4jContext context, ILogger<DocumentRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<Domain.Entities.Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var query = @"
                MATCH (d:Document {id: $id})
                OPTIONAL MATCH (d)-[:HAS_PAGE]->(p:Page)
                OPTIONAL MATCH (p)-[:MENTIONS]->(n:Entity)
                RETURN d, 
                       collect(DISTINCT p) as pages,
                       collect(DISTINCT n) as nodes
            ";

            var parameters = new { id = id.ToString() };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query, parameters);
            var records = await cursor.ToListAsync(cancellationToken);

            if (!records.Any())
                return null;

            var record = records.First();

            return await MapToDocumentAsync(record, session);
        }

        /// <inheritdoc />
        public async Task<Domain.Entities.Document?> GetByExternalIdAsync(string source, string externalId, CancellationToken cancellationToken = default)
        {
            var query = @"
                MATCH (d:Document {source: $source, externalId: $externalId})
                OPTIONAL MATCH (d)-[:HAS_PAGE]->(p:Page)
                OPTIONAL MATCH (p)-[:MENTIONS]->(n:Entity)
                RETURN d, 
                       collect(DISTINCT p) as pages,
                       collect(DISTINCT n) as nodes
            ";

            try
            {
                var parameters = new { source, externalId };

                using var session = _context.GetSession();
                var cursor = await session.RunAsync(query, parameters);
                var records = await cursor.ToListAsync(cancellationToken);

                if (!records.Any())
                    return null;

                var record = records.First();

                return await MapToDocumentAsync(record, session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing GetByExternalIdAsync with source: {Source} and externalId: {ExternalId}", source, externalId);
                throw;
            }
        }

        public async Task<IEnumerable<Domain.Entities.Document>> GetDocumentsByFiltersAsync(
            string? source = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? status = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
              {
                    var skip = (page - 1) * pageSize;

                    var queryBuilder = new StringBuilder(@"
                MATCH (d:Document)
                WHERE 1=1
            ");

            if (!string.IsNullOrWhiteSpace(source))
            {
                queryBuilder.Append(" AND d.source = $source");
            }

            if (fromDate.HasValue)
            {
                queryBuilder.Append(" AND d.lastModified >= datetime($fromDate)");
            }

            if (toDate.HasValue)
            {
                queryBuilder.Append(" AND d.lastModified <= datetime($toDate)");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                queryBuilder.Append(" AND d.processingStatus = $status");
            }

            queryBuilder.Append(@"
        OPTIONAL MATCH (d)-[:HAS_PAGE]->(p:Page)
        OPTIONAL MATCH (p)-[:MENTIONS]->(n:Entity)
        RETURN d, 
               collect(DISTINCT p) as pages,
               collect(DISTINCT n) as nodes
        ORDER BY d.lastModified DESC
        SKIP $skip
        LIMIT $pageSize
    ");

            var parameters = new
            {
                source = source ?? "",
                fromDate = fromDate?.ToString("o"),
                toDate = toDate?.ToString("o"),
                status = status ?? "",
                skip,
                pageSize
            };

            try
            {
                using var session = _context.GetSession();
                var cursor = await session.RunAsync(queryBuilder.ToString(), parameters);
                var records = await cursor.ToListAsync(cancellationToken);

                var documents = new List<Domain.Entities.Document>();
                foreach (var record in records)
                {
                    documents.Add(await MapToDocumentAsync(record, session));
                }

                _logger.LogDebug("Retrieved {Count} documents with filters", documents.Count);
                return documents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting documents by filters");
                throw;
            }
        }

        public async Task<int> GetTotalCountAsync(
        string? source = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken cancellationToken = default)
        {
            var queryBuilder = new StringBuilder(@"
            MATCH (d:Document)
            WHERE 1=1
        ");

            if (!string.IsNullOrWhiteSpace(source))
            {
                queryBuilder.Append(" AND d.source = $source");
            }

            if (fromDate.HasValue)
            {
                queryBuilder.Append(" AND d.lastModified >= datetime($fromDate)");
            }

            if (toDate.HasValue)
            {
                queryBuilder.Append(" AND d.lastModified <= datetime($toDate)");
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                queryBuilder.Append(" AND d.processingStatus = $status");
            }

            queryBuilder.Append(" RETURN count(d) as count");

            var parameters = new
            {
                source = source ?? "",
                fromDate = fromDate?.ToString("o"),
                toDate = toDate?.ToString("o"),
                status = status ?? ""
            };

            try
            {
                using var session = _context.GetSession();
                var cursor = await session.RunAsync(queryBuilder.ToString(), parameters);
                var record = await cursor.SingleAsync(cancellationToken);

                var count = record["count"].As<int>();
                _logger.LogDebug("Total document count with filters: {Count}", count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total document count");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Domain.Entities.Document>> GetModifiedSinceAsync(
            DateTime since,
            CancellationToken cancellationToken = default)
        {
            var query = @"
            MATCH (d:Document)
            WHERE d.lastModified >= datetime($since)
            OPTIONAL MATCH (d)-[:HAS_PAGE]->(p:Page)
            RETURN d, collect(p) as pages
            ORDER BY d.lastModified DESC
        ";

            var parameters = new { since = since.ToString("o") };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query, parameters);
            var records = await cursor.ToListAsync();

            var documents = new List<Domain.Entities.Document>();
            foreach (var record in records)
            {
                documents.Add(await MapToDocumentAsync(record, session));
            }

            return documents;
        }

        /// <inheritdoc />
        public async Task<Domain.Entities.Document> AddAsync(Domain.Entities.Document document, CancellationToken cancellationToken = default)
        {
            using var session = _context.GetSession();
            using var transaction = await session.BeginTransactionAsync();

            try
            {
                // Check if document already exists
                var exists = await ExistsAsync(document.Source, document.ExternalId, cancellationToken);
                if (exists)
                {
                    throw new InvalidOperationException(
                        $"Document with source '{document.Source}' and external ID '{document.ExternalId}' already exists");
                }

                // Serialize metadata to JSON string
                var metadataJson = JsonSerializer.Serialize(document.Metadata ?? new Dictionary<string, string>());

                // Create document node
                var createQuery = @"
            CREATE (d:Document {
                id: $id,
                source: $source,
                externalId: $externalId,
                title: $title,
                lastModified: datetime($lastModified),
                created: datetime(),
                processingStatus: $processingStatus,
                metadata: $metadata
            })
            RETURN d
        ";

                var documentParams = new
                {
                    id = document.Id.ToString(),
                    source = document.Source,
                    externalId = document.ExternalId,
                    title = document.Title,
                    lastModified = document.LastModified.ToString("o"),
                    processingStatus = document.ProcessingStatus ?? "Pending",
                    metadata = metadataJson  // ✅ Store as JSON string
                };

                await transaction.RunAsync(createQuery, documentParams);

                // If document has pages, create them
                if (document.Pages?.Any() == true)
                {
                    foreach (var page in document.Pages)
                    {
                        await CreatePageAsync(transaction, document.Id, page);
                    }
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Created new document {DocumentId} from {Source}",
                    document.Id, document.Source);

                return document;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create document {ExternalId}", document.ExternalId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdateAsync(Domain.Entities.Document document, CancellationToken cancellationToken = default)
        {
            using var session = _context.GetSession();
            using var transaction = await session.BeginTransactionAsync();

            try
            {
                // ✅ Serialize metadata to JSON string
                var metadataJson = JsonSerializer.Serialize(document.Metadata ?? new Dictionary<string, string>());

                // Update document properties
                var updateQuery = @"
            MATCH (d:Document {id: $id})
            SET d.title = $title,
                d.lastModified = datetime($lastModified),
                d.processingStatus = $processingStatus,
                d.metadata = $metadata,
                d.updated = datetime()
            RETURN d
        ";

                var updateParams = new
                {
                    id = document.Id.ToString(),
                    title = document.Title,
                    lastModified = document.LastModified.ToString("o"),
                    processingStatus = document.ProcessingStatus ?? "Processed",
                    metadata = metadataJson  // ✅ Store as JSON string
                };

                await transaction.RunAsync(updateQuery, updateParams);

                // Handle pages
                await HandlePagesAsync(transaction, document);

                await transaction.CommitAsync();

                _logger.LogDebug("Updated document {DocumentId}", document.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update document {DocumentId}", document.Id);
                throw;
            }
        }

        private async Task HandlePagesAsync(IAsyncTransaction transaction, Domain.Entities.Document document)
        {
            // Get existing pages
            var existingPagesQuery = @"
        MATCH (d:Document {id: $id})-[:HAS_PAGE]->(p:Page)
        RETURN p
    ";

            var existingPagesResult = await transaction.RunAsync(
                existingPagesQuery,
                new { id = document.Id.ToString() }
            );

            var existingPages = await existingPagesResult.ToListAsync();
            var existingPageNumbers = existingPages
                .Select(r => GetPageNumberFromRecord(r))
                .ToHashSet();

            // Update or create pages
            foreach (var page in document.Pages)
            {
                if (existingPageNumbers.Contains(page.PageNumber))
                {
                    await UpdatePageAsync(transaction, document.Id, page);
                }
                else
                {
                    await CreatePageAsync(transaction, document.Id, page);
                }
            }

            // Delete pages that no longer exist
            var currentPageNumbers = document.Pages.Select(p => p.PageNumber).ToHashSet();
            var pagesToDelete = existingPageNumbers.Except(currentPageNumbers).ToList();

            foreach (var pageNumber in pagesToDelete)
            {
                await DeletePageAsync(transaction, document.Id, pageNumber);
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            using var session = _context.GetSession();
            using var transaction = await session.BeginTransactionAsync();

            try
            {
                // First, get all nodes that are only connected to this document
                var findOrphanedQuery = @"
                MATCH (d:Document {id: $id})-[:HAS_PAGE]->(p:Page)-[:MENTIONS]->(n:Entity)
                OPTIONAL MATCH (n)<-[:MENTIONS]-(other:Page)<-[:HAS_PAGE]-(otherDoc:Document)
                WHERE otherDoc.id <> $id
                WITH n, count(otherDoc) as refCount
                WHERE refCount = 0
                RETURN collect(n.id) as orphanedNodeIds
            ";

                var findResult = await transaction.RunAsync(findOrphanedQuery, new { id = id.ToString() });
                var findRecord = await findResult.SingleAsync();
                var orphanedNodeIds = findRecord["orphanedNodeIds"].As<List<string>>();

                // Delete document relationships and page links
                var deleteRelsQuery = @"
                MATCH (d:Document {id: $id})-[r:HAS_PAGE]->(p:Page)
                OPTIONAL MATCH (p)-[r2:MENTIONS]->()
                DELETE r2
                WITH p
                DELETE r, p
                WITH d
                DETACH DELETE d
            ";

                await transaction.RunAsync(deleteRelsQuery, new { id = id.ToString() });

                // Delete orphaned entity nodes
                if (orphanedNodeIds.Any())
                {
                    var deleteOrphanedQuery = @"
                    UNWIND $nodeIds AS nodeId
                    MATCH (n:Entity {id: nodeId})
                    DETACH DELETE n
                ";

                    await transaction.RunAsync(deleteOrphanedQuery, new { nodeIds = orphanedNodeIds });
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Deleted document {DocumentId} and {OrphanedCount} orphaned nodes",
                    id, orphanedNodeIds.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete document {DocumentId}", id);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string source, string externalId, CancellationToken cancellationToken = default)
        {
            var query = @"
                MATCH (d:Document {source: $source, externalId: $externalId})
                RETURN count(d) > 0 as exists
            ";

            var parameters = new { source, externalId };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query, parameters);
            var record = await cursor.SingleAsync();

            return record["exists"].As<bool>();
        }

        #region Private Helper Methods

        private async Task<Domain.Entities.Document> MapToDocumentAsync(IRecord record, IAsyncSession session)
        {
            var docNode = record["d"].As<INode>();
            var pages = record["pages"].As<List<object>>();

            var document = new Domain.Entities.Document(
                docNode.Properties["source"].As<string>(),
                docNode.Properties["externalId"].As<string>(),
                docNode.Properties["title"].As<string>(),
                GetDateTimeProperty(docNode, "lastModified") ?? DateTime.UtcNow
            );

            // Set Id using reflection (if needed, since it's private)
            var idProp = typeof(Domain.Entities.Document).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProp != null && idProp.CanWrite)
            {
                idProp.SetValue(document, Guid.Parse(docNode.Properties["id"].As<string>()));
            }

            // Set processing status
            if (docNode.Properties.TryGetValue("processingStatus", out var status))
            {
                var statusProp = typeof(Domain.Entities.Document).GetProperty("ProcessingStatus");
                if (statusProp != null && statusProp.CanWrite)
                {
                    statusProp.SetValue(document, status.As<string>());
                }
            }

            // Deserialize metadata from JSON string
            if (docNode.Properties.TryGetValue("metadata", out var metadataObj) && metadataObj is string metadataJson)
            {
                var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        document.AddMetadata(kvp.Key, kvp.Value);
                    }
                }
            }

            // ✅ Use the AddPage method to add pages to the document
            foreach (var pageObj in pages)
            {
                if (pageObj is INode pageNode)
                {
                    var page = await MapToPageAsync(pageNode, session);

                    // Use the domain method to add the page
                    var addResult = document.AddPage(page.PageNumber, page.Content);

                    // If AddPage creates a new page, we need to update the page with the correct ID
                    if (addResult.IsSuccess)
                    {
                        var addedPage = addResult.Value;
                        // Copy the stored page properties to the added page
                        var idField = typeof(Page).GetProperty("Id");
                        if (idField != null && idField.CanWrite)
                        {
                            idField.SetValue(addedPage, page.Id);
                        }
                    }
                }
            }

            return document;
        }

        private async Task<Page> MapToPageAsync(INode pageNode, IAsyncSession session)
        {
            var pageId = Guid.Parse(pageNode.Properties["id"].As<string>());
            var pageNumber = pageNode.Properties["pageNumber"].As<int>();
            var contentHash = pageNode.Properties["contentHash"].As<string>();

            // Get page content
            var content = await GetPageContentAsync(pageId, session);

            var page = new Page(pageNumber, content ?? string.Empty);

            // Set additional properties
            typeof(Page).GetProperty("Id")?.SetValue(page, pageId);
            typeof(Page).GetProperty("ContentHash")?.SetValue(page, contentHash);

            if (pageNode.Properties.TryGetValue("created", out var created))
            {
                typeof(Page).GetProperty("Created")?.SetValue(page, GetDateTimeValue(created));
            }

            if (pageNode.Properties.TryGetValue("lastModified", out var lastModified))
            {
                typeof(Page).GetProperty("LastModified")?.SetValue(page, GetDateTimeValue(lastModified));
            }

            return page;
        }

        private async Task<string?> GetPageContentAsync(Guid pageId, IAsyncSession session)
        {
            var query = @"
            MATCH (p:Page {id: $pageId})
            RETURN p.content as content
        ";

            var parameters = new { pageId = pageId.ToString() };
            var cursor = await session.RunAsync(query, parameters);
            var record = await cursor.SingleAsync();

            return record?.TryGet("content", out string content) == true ? content : null;
        }

        private async Task CreatePageAsync(IAsyncTransaction tx, Guid documentId, Page page)
        {
            var createPageQuery = @"
            MATCH (d:Document {id: $documentId})
            CREATE (p:Page {
                id: $pageId,
                pageNumber: $pageNumber,
                contentHash: $contentHash,
                contentLength: $contentLength,
                created: datetime(),
                lastModified: datetime()
            })
            CREATE (d)-[:HAS_PAGE]->(p)
            RETURN p
        ";

            var pageParams = new
            {
                documentId = documentId.ToString(),
                pageId = page.Id.ToString(),
                pageNumber = page.PageNumber,
                contentHash = page.ContentHash,
                contentLength = page.Content?.Length ?? 0
            };

            await tx.RunAsync(createPageQuery, pageParams);

            // Store page content
            if (!string.IsNullOrEmpty(page.Content))
            {
                await StorePageContentAsync(tx, page.Id, page.Content);
            }
        }

        private async Task UpdatePageAsync(IAsyncTransaction tx, Guid documentId, Page page)
        {
            var updatePageQuery = @"
            MATCH (d:Document {id: $documentId})-[:HAS_PAGE]->(p:Page {pageNumber: $pageNumber})
            SET p.contentHash = $contentHash,
                p.contentLength = $contentLength,
                p.lastModified = datetime(),
                p.updated = datetime()
            RETURN p
        ";

            var pageParams = new
            {
                documentId = documentId.ToString(),
                pageNumber = page.PageNumber,
                contentHash = page.ContentHash,
                contentLength = page.Content?.Length ?? 0
            };

            await tx.RunAsync(updatePageQuery, pageParams);

            // Update content
            if (!string.IsNullOrEmpty(page.Content))
            {
                await StorePageContentAsync(tx, page.Id, page.Content);
            }
        }

        private async Task DeletePageAsync(IAsyncTransaction tx, Guid documentId, int pageNumber)
        {
            var deletePageQuery = @"
            MATCH (d:Document {id: $documentId})-[r:HAS_PAGE]->(p:Page {pageNumber: $pageNumber})
            OPTIONAL MATCH (p)-[r2:MENTIONS]->()
            DELETE r2, r, p
        ";

            await tx.RunAsync(deletePageQuery, new
            {
                documentId = documentId.ToString(),
                pageNumber = pageNumber
            });
        }

        private async Task StorePageContentAsync(IAsyncTransaction tx, Guid pageId, string content)
        {
            var storeContentQuery = @"
            MATCH (p:Page {id: $pageId})
            SET p.content = $content
            RETURN p
        ";

            await tx.RunAsync(storeContentQuery, new
            {
                pageId = pageId.ToString(),
                content = content
            });
        }

        private int GetPageNumberFromRecord(IRecord record)
        {
            var pageNode = record["p"].As<INode>();
            return pageNode.Properties["pageNumber"].As<int>();
        }

        private DateTime? GetDateTimeProperty(INode node, string propertyName)
        {
            if (node.Properties.TryGetValue(propertyName, out var value))
            {
                return GetDateTimeValue(value);
            }
            return null;
        }

        private DateTime? GetDateTimeValue(object value)
        {
            if (value is ZonedDateTime zoned)
            {
                return zoned.UtcDateTime;
            }
            if (value is LocalDateTime local)
            {
                return local.ToDateTime();
            }
            if (value is string dateString && DateTime.TryParse(dateString, out var parsed))
            {
                return parsed;
            }
            return null;
        }

        #endregion
    }

}
