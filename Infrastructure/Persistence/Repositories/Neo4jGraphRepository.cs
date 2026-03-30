using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Application.Common.DTOs;

namespace Infrastructure.Persistence.Repositories
{
    public class Neo4jGraphRepository : IGraphRepository
    {
        private readonly INeo4jContext _context;
        private readonly ILogger<Neo4jGraphRepository> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public Neo4jGraphRepository(INeo4jContext context, ILogger<Neo4jGraphRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };
        }

        #region Store Operations

        /// <inheritdoc />
        public async Task StoreExtractionAsync(
        Guid documentId,
        Guid pageId,
        IEnumerable<ExtractedNode> nodes,
        IEnumerable<ExtractedRelationship> relationships,
        CancellationToken cancellationToken = default)
        {
            var nodeList = nodes?.ToList() ?? new List<ExtractedNode>();
            var relList = relationships?.ToList() ?? new List<ExtractedRelationship>();

            if (!nodeList.Any() && !relList.Any())
            {
                _logger.LogDebug("No nodes or relationships to store for document {DocumentId}", documentId);
                return;
            }

            using var session = _context.GetSession();

            // FIX: Use BeginTransactionAsync without CancellationToken parameter
            // The Neo4j driver doesn't support CancellationToken in this overload
            using var transaction = await session.BeginTransactionAsync();

            try
            {
                // Step 1: Create or merge all nodes
                foreach (var node in nodeList)
                {
                    await CreateOrMergeNodeAsync(transaction, node, documentId);
                }

                // Step 2: Create relationships between nodes
                foreach (var rel in relList)
                {
                    await CreateRelationshipAsync(transaction, rel, documentId);
                }

                // Step 3: Link page to nodes
                if (nodeList.Any())
                {
                    await LinkPageToNodesAsync(transaction, documentId, pageId, nodeList.Select(n => n.Id));
                }

                // Step 4: Update document metadata
                await UpdateDocumentMetadataAsync(transaction, documentId, nodeList.Count, relList.Count);

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Successfully stored {NodeCount} nodes and {RelCount} relationships for document {DocumentId} page {PageId}",
                    nodeList.Count, relList.Count, documentId, pageId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to store graph data for document {DocumentId}", documentId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UpdatePageGraphAsync(
        Guid documentId,
        Guid pageId,
        IEnumerable<ExtractedNode> nodes,
        IEnumerable<ExtractedRelationship> relationships,
        CancellationToken cancellationToken = default)
        {
            var nodeList = nodes?.ToList() ?? new List<ExtractedNode>();
            var relList = relationships?.ToList() ?? new List<ExtractedRelationship>();

            using var session = _context.GetSession();

            // FIX: Use BeginTransactionAsync without CancellationToken parameter
            using var transaction = await session.BeginTransactionAsync();

            try
            {
                // Step 1: Get existing node IDs for this page
                var existingNodeIds = await GetPageNodeIdsAsync(transaction, pageId);

                // Step 2: Remove old links
                if (existingNodeIds.Any())
                {
                    await RemovePageLinksAsync(transaction, pageId);
                }

                // Step 3: Create or merge new nodes
                foreach (var node in nodeList)
                {
                    await CreateOrMergeNodeAsync(transaction, node, documentId);
                }

                // Step 4: Create new relationships
                foreach (var rel in relList)
                {
                    await CreateRelationshipAsync(transaction, rel, documentId);
                }

                // Step 5: Link page to new nodes
                if (nodeList.Any())
                {
                    await LinkPageToNodesAsync(transaction, documentId, pageId, nodeList.Select(n => n.Id));
                }

                // Step 6: Clean up orphaned nodes
                var nodesToRemove = existingNodeIds.Except(nodeList.Select(n => n.Id)).ToList();
                if (nodesToRemove.Any())
                {
                    await RemoveOrphanedNodesAsync(transaction, nodesToRemove);
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Updated graph for document {DocumentId} page {PageId}: Added {NewNodes} nodes, Removed {RemovedNodes} nodes",
                    documentId, pageId, nodeList.Count, nodesToRemove.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update page graph for document {DocumentId}", documentId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeleteDocumentGraphAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            using var session = _context.GetSession();

            // FIX: Use BeginTransactionAsync without CancellationToken parameter
            using var transaction = await session.BeginTransactionAsync();

            try
            {
                // Get all nodes linked to this document
                var query = @"
                MATCH (d:Document {id: $documentId})-[:HAS_PAGE]->(p:Page)-[:MENTIONS]->(n:Entity)
                OPTIONAL MATCH (n)<-[:MENTIONS]-(otherPage:Page)<-[:HAS_PAGE]-(otherDoc:Document)
                WHERE otherDoc.id <> $documentId
                WITH n, count(otherDoc) as refCount
                WHERE refCount = 0
                RETURN collect(n.id) as orphanedNodeIds
            ";

                var parameters = new { documentId = documentId.ToString() };
                var cursor = await transaction.RunAsync(query, parameters);
                var record = await cursor.SingleAsync();
                var orphanedNodeIds = record["orphanedNodeIds"].As<List<string>>();

                // Delete document and its pages
                var deleteQuery = @"
                MATCH (d:Document {id: $documentId})
                OPTIONAL MATCH (d)-[:HAS_PAGE]->(p:Page)
                OPTIONAL MATCH (p)-[r:MENTIONS]->()
                DELETE r, p, d
            ";

                await transaction.RunAsync(deleteQuery, new { documentId = documentId.ToString() });

                // Delete orphaned nodes
                if (orphanedNodeIds.Any())
                {
                    await DeleteNodesAsync(transaction, orphanedNodeIds);
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Deleted document graph for {DocumentId} and {OrphanedCount} orphaned nodes",
                    documentId, orphanedNodeIds.Count);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete document graph for {DocumentId}", documentId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeletePageGraphAsync(Guid documentId, Guid pageId, CancellationToken cancellationToken = default)
        {
            using var session = _context.GetSession();

            // ✅ FIX: Use BeginTransactionAsync without CancellationToken parameter
            using var transaction = await session.BeginTransactionAsync();

            try
            {
                // Get node IDs for this page
                var nodeIds = await GetPageNodeIdsAsync(transaction, pageId);

                // Remove page links
                await RemovePageLinksAsync(transaction, pageId);

                // Delete the page
                var deletePageQuery = @"
                MATCH (p:Page {id: $pageId})
                DETACH DELETE p
            ";

                await transaction.RunAsync(deletePageQuery, new { pageId = pageId.ToString() });

                // Clean up orphaned nodes
                if (nodeIds.Any())
                {
                    await RemoveOrphanedNodesAsync(transaction, nodeIds);
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Deleted page {PageId} graph from document {DocumentId}", pageId, documentId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete page graph for document {DocumentId}", documentId);
                throw;
            }
        }


        #endregion

        #region Query Operations

        /// <inheritdoc />
        public async Task<DomainGraphQueryResult> FindRelatedAsync(string source, string entityType, string entityName, string? relationship = null, int depth = 2, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Finding related entities for source={Source}, entityType={EntityType}, entityName={EntityName}, relationship={Relationship}, depth={Depth}",
                    source, entityType, entityName, relationship ?? "ANY", depth);

                var relationshipPattern = string.IsNullOrWhiteSpace(relationship)
                    ? $"[r*1..{depth}]"
                    : $"[r:{relationship}*1..{depth}]";

                        var query = @$"
                    // Find starting entities
                    MATCH (start:Entity)
                    WHERE start.type = $entityType 
                      AND toLower(start.name) CONTAINS toLower($entityName)
        
                    // Find related entities within depth
                    MATCH path = (start)-{relationshipPattern}-(related:Entity)
        
                    // Find documents that mention these entities
                    OPTIONAL MATCH (doc:Document {{source: $source}})-[:HAS_PAGE]->(p:Page)-[:MENTIONS]->(entity)
                    WHERE entity IN [start] + nodes(path)
        
                    // Build result
                    RETURN 
                        start as sourceEntity,
                        collect(DISTINCT related) as relatedEntities,
                        collect(DISTINCT r) as relationships,
                        collect(DISTINCT {{
                            id: doc.id,
                            title: doc.title,
                            source: doc.source,
                            externalId: doc.externalId
                        }}) as documents
                ";

                var parameters = new
                {
                    source,
                    entityType,
                    entityName
                };

                using var session = _context.GetSession();
                var cursor = await session.RunAsync(query, parameters);
                var records = await cursor.ToListAsync(cancellationToken);

                return MapToDomainGraphQueryResult(records);
            
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error finding related entities for source={Source}, entityType={EntityType}, entityName={EntityName}", source, entityType, entityName);   
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<DomainDocumentReference>> GetDocumentsByEntityAsync(
            string entityName,
            string? entityType = null,
            string? source = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 20,
            string sortBy = "lastModified",
            bool sortDescending = true,
            CancellationToken cancellationToken = default)
        {
            var skip = (page - 1) * pageSize;
            var orderDirection = sortDescending ? "DESC" : "ASC";

            var queryBuilder = new StringBuilder(@"
            MATCH (n:Entity)-[:MENTIONS]-(p:Page)-[:HAS_PAGE]-(d:Document)
            WHERE toLower(n.name) CONTAINS toLower($entityName)
        ");

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                queryBuilder.Append(" AND n.type = $entityType");
            }

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

            var orderBy = sortBy.ToLower() switch
            {
                "title" => "d.title",
                "source" => "d.source",
                _ => "d.lastModified"
            };

            queryBuilder.Append($@"
            RETURN DISTINCT {{
                id: d.id,
                title: d.title,
                source: d.source,
                externalId: d.externalId
            }} as document
            ORDER BY {orderBy} {orderDirection}
            SKIP $skip
            LIMIT $pageSize
        ");

            var parameters = new
            {
                entityName,
                entityType = entityType ?? "",
                source = source ?? "",
                fromDate = fromDate?.ToString("o"),
                toDate = toDate?.ToString("o"),
                skip,
                pageSize
            };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(queryBuilder.ToString(), parameters);
            var records = await cursor.ToListAsync(cancellationToken);

            var documents = new List<DomainDocumentReference>();

            foreach (var record in records)
            {
                var doc = record["document"].As<Dictionary<string, object>>();
                documents.Add(new DomainDocumentReference
                {
                    Id = Guid.Parse(doc["id"].ToString()!),
                    Title = doc["title"].ToString()!,
                    Source = doc["source"].ToString()!,
                    ExternalId = doc["externalId"].ToString()!
                });
            }

            return documents;
        }

        /// <inheritdoc />
        public async Task<int> GetDocumentsByEntityCountAsync(
            string entityName,
            string? entityType = null,
            string? source = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken cancellationToken = default)
        {
            var queryBuilder = new StringBuilder(@"
            MATCH (n:Entity)-[:MENTIONS]-(p:Page)-[:HAS_PAGE]-(d:Document)
            WHERE toLower(n.name) CONTAINS toLower($entityName)
        ");

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                queryBuilder.Append(" AND n.type = $entityType");
            }

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

            queryBuilder.Append(" RETURN count(DISTINCT d) as count");

            var parameters = new
            {
                entityName,
                entityType = entityType ?? "",
                source = source ?? "",
                fromDate = fromDate?.ToString("o"),
                toDate = toDate?.ToString("o")
            };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(queryBuilder.ToString(), parameters);
            var record = await cursor.SingleAsync(cancellationToken);

            return record["count"].As<int>();
        }

        /// <inheritdoc />
        public async Task<DomainEntityStatistics> GetEntityStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var query = @"
            // Count by type
            MATCH (n:Entity)
            WITH n.type as type, count(n) as typeCount
            ORDER BY typeCount DESC
            RETURN collect({type: type, count: typeCount}) as typeCounts

            // Top entities by mentions
            MATCH (n:Entity)<-[:MENTIONS]-(p:Page)
            WITH n, count(p) as mentionCount
            ORDER BY mentionCount DESC
            LIMIT 20
            RETURN collect({
                name: n.name,
                type: n.type,
                mentionCount: mentionCount,
                documentCount: mentionCount
            }) as topEntities

            // Total counts
            MATCH (n:Entity) WITH count(n) as totalEntities
            MATCH (r:RELATES_TO) WITH totalEntities, count(r) as totalRelationships
            RETURN totalEntities, totalRelationships
        ";

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query);
            var records = await cursor.ToListAsync(cancellationToken);

            if (!records.Any())
                return new DomainEntityStatistics();

            var record = records.First();
            var statistics = new DomainEntityStatistics
            {
                AsOf = DateTime.UtcNow
            };

            // Parse type counts
            if (record.TryGet("typeCounts", out List<object> typeCounts))
            {
                foreach (var tc in typeCounts)
                {
                    var dict = tc as Dictionary<string, object>;
                    if (dict != null)
                    {
                        var type = dict["type"].ToString()!;
                        var count = Convert.ToInt32(dict["count"]);
                        statistics.CountByType[type] = count;
                        statistics.TotalEntities += count;
                    }
                }
            }

            // Parse top entities
            if (record.TryGet("topEntities", out List<object> topEntities))
            {
                statistics.TopEntities = topEntities.Select(te =>
                {
                    var dict = te as Dictionary<string, object>;
                    return new DomainTopEntity
                    {
                        Name = dict?["name"].ToString()!,
                        Type = dict?["type"].ToString()!,
                        MentionCount = Convert.ToInt32(dict?["mentionCount"]),
                        DocumentCount = Convert.ToInt32(dict?["documentCount"])
                    };
                }).ToList();
            }

            // Parse total relationships
            if (record.TryGet("totalRelationships", out object totalRels))
            {
                statistics.TotalRelationships = Convert.ToInt32(totalRels);
            }

            return statistics;
        }

        /// <inheritdoc />
        public async Task<DomainDocumentHistoryResult?> GetDocumentHistoryAsync(
            Guid documentId,
            bool includeExtractions = false,
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var skip = (page - 1) * pageSize;

            var query = @"
            MATCH (d:Document {id: $documentId})
            OPTIONAL MATCH (d)-[:HAS_PAGE]->(p:Page)
            OPTIONAL MATCH (p)-[:MENTIONS]->(n:Entity)
            OPTIONAL MATCH (n)-[r:RELATES_TO]-(n2:Entity)
            
            RETURN d,
                   p.pageNumber as pageNumber,
                   p.lastModified as lastModified,
                   p.contentHash as contentHash,
                   collect(DISTINCT n) as nodes,
                   collect(DISTINCT r) as relationships
            ORDER BY p.lastModified DESC
            SKIP $skip
            LIMIT $pageSize
        ";

            var parameters = new
            {
                documentId = documentId.ToString(),
                skip,
                pageSize
            };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query, parameters);
            var records = await cursor.ToListAsync(cancellationToken);

            if (!records.Any())
                return null;

            var firstRecord = records.First();
            var docNode = firstRecord["d"].As<INode>();

            var result = new DomainDocumentHistoryResult
            {
                DocumentId = documentId,
                Title = docNode.Properties["title"].As<string>(),
                Versions = new List<DomainDocumentVersion>()
            };

            foreach (var record in records)
            {
                var version = new DomainDocumentVersion
                {
                    Version = result.Versions.Count + 1,
                    ModifiedAt = GetDateTimeProperty(record, "lastModified") ?? DateTime.UtcNow,
                    ContentHash = record["contentHash"].As<string>(),
                    NodeCount = record["nodes"].As<List<object>>().Count,
                    RelationshipCount = record["relationships"].As<List<object>>().Count
                };

                if (includeExtractions)
                {
                    version.Nodes = MapToDomainNodes(record["nodes"].As<List<object>>());
                    version.Relationships = MapToDomainRelationships(record["relationships"].As<List<object>>());
                }

                result.Versions.Add(version);
            }

            result.TotalVersions = result.Versions.Count;
            return result;
        }

        /// <inheritdoc />
        public async Task<DomainGraphNavigationResult?> NavigateFromEntityAsync(
            string entityId,
            List<string>? relationshipTypes = null,
            int maxDepth = 3,
            CancellationToken cancellationToken = default)
        {
            var relFilter = relationshipTypes?.Any() == true
                ? $"[r:{string.Join("|", relationshipTypes)}*1..{maxDepth}]"
                : $"[r*1..{maxDepth}]";

            var query = $@"
            MATCH path = (start:Entity {{id: $entityId}})-{relFilter}-(related:Entity)
            RETURN start, nodes(path) as nodes, relationships(path) as rels
        ";

            var parameters = new { entityId };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query, parameters);
            var records = await cursor.ToListAsync(cancellationToken);

            if (!records.Any())
                return null;

            var result = new DomainGraphNavigationResult
            {
                StartEntityId = entityId,
                Depth = maxDepth,
                Nodes = new List<object>(),
                Relationships = new List<object>()
            };

            var nodeIds = new HashSet<string>();
            var relKeys = new HashSet<string>();

            foreach (var record in records)
            {
                // Add start node
                if (record.TryGet("start", out INode startNode))
                {
                    if (nodeIds.Add(startNode.Properties["id"].As<string>()))
                    {
                        result.Nodes.Add(startNode.Properties);
                    }
                }

                // Add path nodes
                if (record.TryGet("nodes", out List<object> pathNodes))
                {
                    foreach (INode node in pathNodes)
                    {
                        if (nodeIds.Add(node.Properties["id"].As<string>()))
                        {
                            result.Nodes.Add(node.Properties);
                        }
                    }
                }

                // Add relationships
                if (record.TryGet("rels", out List<object> rels))
                {
                    foreach (IRelationship rel in rels)
                    {
                        var relKey = $"{rel.StartNodeId}_{rel.EndNodeId}_{rel.Type}";
                        if (relKeys.Add(relKey))
                        {
                            result.Relationships.Add(new
                            {
                                type = rel.Type,
                                from = rel.StartNodeId,
                                to = rel.EndNodeId,
                                properties = rel.Properties
                            });
                        }
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<List<DomainGraphPath>> FindPathsAsync(
            string fromEntityId,
            string toEntityId,
            int maxPathLength = 5,
            CancellationToken cancellationToken = default)
        {
            var query = @"
            MATCH path = shortestPath((a:Entity {id: $fromId})-[:RELATES_TO*..$maxPathLength]-(b:Entity {id: $toId}))
            RETURN path
        ";

            var parameters = new
            {
                fromId = fromEntityId,
                toId = toEntityId,
                maxPathLength
            };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query, parameters);
            var records = await cursor.ToListAsync(cancellationToken);

            var paths = new List<DomainGraphPath>();

            foreach (var record in records)
            {
                if (record.TryGet("path", out IPath path))
                {
                    paths.Add(new DomainGraphPath
                    {
                        NodeIds = path.Nodes.Select(n => n.Properties["id"].As<string>()).ToList(),
                        RelationshipTypes = path.Relationships.Select(r => r.Type).ToList(),
                        //Length = path.Length,
                        //Weight = path.Length
                    });
                }
            }

            return paths;
        }

        /// <inheritdoc />
        public async Task<DomainSearchResult<DomainEntitySummary>> GetEntitiesByTypeAsync(
            string? entityType = null,
            string? searchTerm = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var skip = (page - 1) * pageSize;

            var queryBuilder = new StringBuilder(@"
            MATCH (n:Entity)
        ");

            var whereClauses = new List<string>();

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                whereClauses.Add("n.type = $entityType");
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                whereClauses.Add("toLower(n.name) CONTAINS toLower($searchTerm)");
            }

            if (whereClauses.Any())
            {
                queryBuilder.Append("WHERE " + string.Join(" AND ", whereClauses));
            }

            queryBuilder.Append(@"
            OPTIONAL MATCH (n)<-[:MENTIONS]-(p:Page)
            WITH n, count(p) as mentionCount
            RETURN {
                id: n.id,
                name: n.name,
                type: n.type,
                mentionCount: mentionCount,
                properties: n.properties
            } as entity
            ORDER BY mentionCount DESC
            SKIP $skip
            LIMIT $pageSize
        ");

            // Count query
            var countQueryBuilder = new StringBuilder(@"
            MATCH (n:Entity)
        ");

            if (whereClauses.Any())
            {
                countQueryBuilder.Append("WHERE " + string.Join(" AND ", whereClauses));
            }

            countQueryBuilder.Append(" RETURN count(n) as count");

            var parameters = new
            {
                entityType = entityType ?? "",
                searchTerm = searchTerm ?? "",
                skip,
                pageSize
            };

            using var session = _context.GetSession();

            // Get total count
            var countCursor = await session.RunAsync(countQueryBuilder.ToString(), parameters);
            var countRecord = await countCursor.SingleAsync(cancellationToken);
            var totalCount = countRecord["count"].As<int>();

            // Get entities
            var cursor = await session.RunAsync(queryBuilder.ToString(), parameters);
            var records = await cursor.ToListAsync(cancellationToken);

            var items = records.Select(r =>
            {
                var entity = r["entity"].As<Dictionary<string, object>>();
                return new DomainEntitySummary
                {
                    Id = entity["id"].ToString()!,
                    Name = entity["name"].ToString()!,
                    Type = entity["type"].ToString()!,
                    MentionCount = Convert.ToInt32(entity["mentionCount"]),
                    Properties = entity.ContainsKey("properties")
                        ? entity["properties"] as Dictionary<string, object>
                        : new Dictionary<string, object>()
                };
            }).ToList();

            return new DomainSearchResult<DomainEntitySummary>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        #endregion

        #region Batch Operations

        /// <inheritdoc />
        public async Task<string> CreateBatchAsync(
            IEnumerable<Document> documents,
            string? initiatedBy = null,
            string? source = null,
            CancellationToken cancellationToken = default)
        {
            var batchId = Guid.NewGuid().ToString();
            var docList = documents?.ToList() ?? new List<Document>();

            var query = @"
            CREATE (b:Batch {
                id: $id,
                status: $status,
                created: datetime($created),
                totalCount: $totalCount,
                processedCount: 0,
                successCount: 0,
                failedCount: 0,
                initiatedBy: $initiatedBy,
                source: $source,
                metadata: $metadata
            })
            RETURN b
        ";

            var parameters = new
            {
                id = batchId,
                status = "Pending",
                created = DateTime.UtcNow.ToString("o"),
                totalCount = docList.Count,
                initiatedBy = initiatedBy ?? "system",
                source = source ?? "unknown",
                metadata = new Dictionary<string, object>
                {
                    ["documentIds"] = docList.Select(d => d.Id.ToString()).ToList()
                }
            };

            using var session = _context.GetSession();
            await session.RunAsync(query, parameters);

            _logger.LogInformation("Created batch {BatchId} with {Count} documents", batchId, docList.Count);

            return batchId;
        }

        /// <inheritdoc />
        public async Task UpdateBatchStatusAsync(
            string batchId,
            IEnumerable<DomainBatchDocumentResult> results,
            CancellationToken cancellationToken = default)
        {
            var resultsList = results?.ToList() ?? new List<DomainBatchDocumentResult>();
            var successCount = resultsList.Count(r => r.Updated);
            var failedCount = resultsList.Count(r => !r.Updated);
            var status = failedCount == 0 ? "Completed" :
                        successCount == 0 ? "Failed" : "PartialSuccess";

            var query = @"
            MATCH (b:Batch {id: $batchId})
            SET b.processedCount = $processedCount,
                b.successCount = $successCount,
                b.failedCount = $failedCount,
                b.status = $status,
                b.completed = datetime($completed),
                b.results = $results
            RETURN b
        ";

            var parameters = new
            {
                batchId,
                processedCount = resultsList.Count,
                successCount,
                failedCount,
                status,
                completed = DateTime.UtcNow.ToString("o"),
                results = resultsList.Select(r => new
                {
                    r.DocumentId,
                    r.ExternalId,
                    r.Title,
                    r.PageNumber,
                    r.Updated,
                    r.Message,
                    r.ExtractedNodes,
                    r.ExtractedRelationships,
                    r.Warnings,
                    r.Error,
                    ProcessedAt = r.ProcessedAt.ToString("o")
                }).ToList()
            };

            using var session = _context.GetSession();
            await session.RunAsync(query, parameters);

            _logger.LogInformation("Updated batch {BatchId} with status {Status}: {Success} success, {Failed} failed",
                batchId, status, successCount, failedCount);
        }

        /// <inheritdoc />
        public async Task<DomainBatchStatus?> GetBatchStatusAsync(
            string batchId,
            CancellationToken cancellationToken = default)
        {
            var query = @"
            MATCH (b:Batch {id: $batchId})
            RETURN b
        ";

            var parameters = new { batchId };

            using var session = _context.GetSession();
            var cursor = await session.RunAsync(query, parameters);
            var record = await cursor.SingleAsync(cancellationToken);

            if (record == null)
                return null;

            var batchNode = record["b"].As<INode>();
            return MapToDomainBatchStatus(batchNode);
        }

        /// <inheritdoc />
        public async Task<DomainSearchResult<DomainBatchSummary>> GetBatchesAsync(
            string? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var skip = (page - 1) * pageSize;

            var queryBuilder = new StringBuilder(@"
            MATCH (b:Batch)
            WHERE 1=1
        ");

            if (!string.IsNullOrWhiteSpace(status))
            {
                queryBuilder.Append(" AND b.status = $status");
            }

            if (fromDate.HasValue)
            {
                queryBuilder.Append(" AND b.created >= datetime($fromDate)");
            }

            if (toDate.HasValue)
            {
                queryBuilder.Append(" AND b.created <= datetime($toDate)");
            }

            queryBuilder.Append(@"
            RETURN b
            ORDER BY b.created DESC
            SKIP $skip
            LIMIT $pageSize
        ");

            // Count query
            var countQueryBuilder = new StringBuilder(@"
            MATCH (b:Batch)
            WHERE 1=1
        ");

            if (!string.IsNullOrWhiteSpace(status))
            {
                countQueryBuilder.Append(" AND b.status = $status");
            }

            if (fromDate.HasValue)
            {
                countQueryBuilder.Append(" AND b.created >= datetime($fromDate)");
            }

            if (toDate.HasValue)
            {
                countQueryBuilder.Append(" AND b.created <= datetime($toDate)");
            }

            countQueryBuilder.Append(" RETURN count(b) as count");

            var parameters = new
            {
                status = status ?? "",
                fromDate = fromDate?.ToString("o"),
                toDate = toDate?.ToString("o"),
                skip,
                pageSize
            };

            using var session = _context.GetSession();

            // Get total count
            var countCursor = await session.RunAsync(countQueryBuilder.ToString(), parameters);
            var countRecord = await countCursor.SingleAsync(cancellationToken);
            var totalCount = countRecord["count"].As<int>();

            // Get batches
            var cursor = await session.RunAsync(queryBuilder.ToString(), parameters);
            var records = await cursor.ToListAsync(cancellationToken);

            var items = records.Select(r =>
            {
                var batchNode = r["b"].As<INode>();
                return new DomainBatchSummary
                {
                    BatchId = batchNode.Properties["id"].As<string>(),
                    Status = batchNode.Properties["status"].As<string>(),
                    Created = GetDateTimeProperty(batchNode, "created") ?? DateTime.UtcNow,
                    Completed = GetDateTimeProperty(batchNode, "completed"),
                    TotalCount = batchNode.Properties.GetValueOrDefault("totalCount")?.As<int>() ?? 0,
                    ProcessedCount = batchNode.Properties.GetValueOrDefault("processedCount")?.As<int>() ?? 0,
                    SuccessCount = batchNode.Properties.GetValueOrDefault("successCount")?.As<int>() ?? 0,
                    FailedCount = batchNode.Properties.GetValueOrDefault("failedCount")?.As<int>() ?? 0
                };
            }).ToList();

            return new DomainSearchResult<DomainBatchSummary>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        #endregion

        #region Private Helper Methods

        private async Task CreateOrMergeNodeAsync(
        IAsyncTransaction tx,
        ExtractedNode node,
        Guid documentId)
        {
            var properties = new Dictionary<string, object>
            {
                ["id"] = node.Id,
                ["name"] = node.Name,
                ["type"] = node.Type,
                ["confidence"] = node.Confidence,
                ["source"] = node.Source,
                ["created"] = DateTime.UtcNow.ToString("o"),
                ["properties"] = node.Properties ?? new Dictionary<string, object>()
            };

            if (node.Aliases?.Any() == true)
            {
                properties["aliases"] = node.Aliases;
            }

            var query = @"
            MERGE (n:Entity {id: $id})
            ON CREATE SET 
                n = $properties,
                n.firstSeenIn = $documentId,
                n.seenCount = 1,
                n.created = datetime()
            ON MATCH SET 
                n.name = $properties.name,
                n.type = $properties.type,
                n.confidence = CASE 
                    WHEN n.confidence < $properties.confidence 
                    THEN $properties.confidence 
                    ELSE n.confidence 
                END,
                n.lastSeen = datetime(),
                n.seenCount = coalesce(n.seenCount, 0) + 1,
                n.properties = $properties.properties,
                n.aliases = $properties.aliases
            RETURN n
        ";

            var parameters = new
            {
                id = node.Id,
                properties,
                documentId = documentId.ToString()
            };

            await tx.RunAsync(query, parameters);
        }

        private async Task CreateRelationshipAsync(
            IAsyncTransaction tx,
            ExtractedRelationship rel,
            Guid documentId)
        {
            var properties = new Dictionary<string, object>
            {
                ["id"] = rel.Id,
                ["type"] = rel.Type,
                ["confidence"] = rel.Confidence,
                ["context"] = rel.Context ?? "",
                ["created"] = DateTime.UtcNow.ToString("o"),
                ["properties"] = rel.Properties ?? new Dictionary<string, object>()
            };

            var query = @"
            MATCH (a:Entity {id: $fromId})
            MATCH (b:Entity {id: $toId})
            MERGE (a)-[r:RELATES_TO {type: $relType}]->(b)
            ON CREATE SET 
                r = $properties,
                r.firstSeenIn = $documentId,
                r.seenCount = 1,
                r.created = datetime()
            ON MATCH SET 
                r.confidence = CASE 
                    WHEN r.confidence < $properties.confidence 
                    THEN $properties.confidence 
                    ELSE r.confidence 
                END,
                r.lastSeen = datetime(),
                r.seenCount = coalesce(r.seenCount, 0) + 1,
                r.context = $properties.context,
                r.properties = $properties.properties
            RETURN r
        ";

            var parameters = new
            {
                fromId = rel.FromNodeId,
                toId = rel.ToNodeId,
                relType = rel.Type,
                properties,
                documentId = documentId.ToString()
            };

            await tx.RunAsync(query, parameters);
        }

        private async Task LinkPageToNodesAsync(
            IAsyncTransaction tx,
            Guid documentId,
            Guid pageId,
            IEnumerable<string> nodeIds)
        {
            var query = @"
            MATCH (d:Document {id: $documentId})
            MATCH (p:Page {id: $pageId})
            MERGE (d)-[:HAS_PAGE]->(p)
            WITH p
            UNWIND $nodeIds AS nodeId
            MATCH (n:Entity {id: nodeId})
            MERGE (p)-[:MENTIONS]->(n)
        ";

            var parameters = new
            {
                documentId = documentId.ToString(),
                pageId = pageId.ToString(),
                nodeIds = nodeIds.ToArray()
            };

            await tx.RunAsync(query, parameters);
        }

        private async Task<List<string>> GetPageNodeIdsAsync(
            IAsyncTransaction tx,
            Guid pageId)
        {
            var query = @"
            MATCH (p:Page {id: $pageId})-[:MENTIONS]->(n:Entity)
            RETURN collect(n.id) as nodeIds
        ";

            var parameters = new { pageId = pageId.ToString() };
            var result = await tx.RunAsync(query, parameters);
            var record = await result.SingleAsync();

            return record["nodeIds"].As<List<string>>();
        }

        private async Task RemovePageLinksAsync(
            IAsyncTransaction tx,
            Guid pageId)
        {
            var query = @"
            MATCH (p:Page {id: $pageId})-[r:MENTIONS]->()
            DELETE r
        ";

            await tx.RunAsync(query, new { pageId = pageId.ToString() });
        }

        private async Task RemoveOrphanedNodesAsync(
            IAsyncTransaction tx,
            List<string> nodeIds)
        {
            if (!nodeIds.Any())
                return;

            var query = @"
            UNWIND $nodeIds AS nodeId
            MATCH (n:Entity {id: nodeId})
            OPTIONAL MATCH (n)<-[:MENTIONS]-(p:Page)
            WITH n, count(p) as refCount
            WHERE refCount = 0
            DETACH DELETE n
        ";

            await tx.RunAsync(query, new { nodeIds });
        }

        private async Task DeleteNodesAsync(
            IAsyncTransaction tx,
            List<string> nodeIds)
        {
            if (!nodeIds.Any())
                return;

            var query = @"
            UNWIND $nodeIds AS nodeId
            MATCH (n:Entity {id: nodeId})
            DETACH DELETE n
        ";

            await tx.RunAsync(query, new { nodeIds });
        }

        private async Task UpdateDocumentMetadataAsync(
            IAsyncTransaction tx,
            Guid documentId,
            int nodeCount,
            int relCount)
        {
            var query = @"
            MATCH (d:Document {id: $documentId})
            SET d.lastProcessed = datetime(),
                d.nodeCount = coalesce(d.nodeCount, 0) + $nodeCount,
                d.relationshipCount = coalesce(d.relationshipCount, 0) + $relCount,
                d.processingStatus = 'Processed'
            RETURN d
        ";

            var parameters = new
            {
                documentId = documentId.ToString(),
                nodeCount,
                relCount
            };

            await tx.RunAsync(query, parameters);
        }

        /// Delete below later 
        private async Task CreateOrMergeNodeAsync(
            IAsyncTransaction tx,
            ExtractedNode node,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            var properties = new Dictionary<string, object>
            {
                ["id"] = node.Id,
                ["name"] = node.Name,
                ["type"] = node.Type,
                ["confidence"] = node.Confidence,
                ["source"] = node.Source,
                ["created"] = DateTime.UtcNow.ToString("o"),
                ["properties"] = node.Properties ?? new Dictionary<string, object>()
            };

            if (node.Aliases?.Any() == true)
            {
                properties["aliases"] = node.Aliases;
            }

            var query = @"
            MERGE (n:Entity {id: $id})
            ON CREATE SET 
                n = $properties,
                n.firstSeenIn = $documentId,
                n.seenCount = 1,
                n.created = datetime()
            ON MATCH SET 
                n.name = $properties.name,
                n.type = $properties.type,
                n.confidence = CASE 
                    WHEN n.confidence < $properties.confidence 
                    THEN $properties.confidence 
                    ELSE n.confidence 
                END,
                n.lastSeen = datetime(),
                n.seenCount = coalesce(n.seenCount, 0) + 1,
                n.properties = $properties.properties,
                n.aliases = $properties.aliases
            RETURN n
        ";

            var parameters = new
            {
                id = node.Id,
                properties,
                documentId = documentId.ToString()
            };

            await tx.RunAsync(query, parameters);
        }

        private async Task CreateRelationshipAsync(
            IAsyncTransaction tx,
            ExtractedRelationship rel,
            Guid documentId,
            CancellationToken cancellationToken)
        {
            var properties = new Dictionary<string, object>
            {
                ["id"] = rel.Id,
                ["type"] = rel.Type,
                ["confidence"] = rel.Confidence,
                ["context"] = rel.Context ?? "",
                ["created"] = DateTime.UtcNow.ToString("o"),
                ["properties"] = rel.Properties ?? new Dictionary<string, object>()
            };

            var query = @"
            MATCH (a:Entity {id: $fromId})
            MATCH (b:Entity {id: $toId})
            MERGE (a)-[r:RELATES_TO {type: $relType}]->(b)
            ON CREATE SET 
                r = $properties,
                r.firstSeenIn = $documentId,
                r.seenCount = 1,
                r.created = datetime()
            ON MATCH SET 
                r.confidence = CASE 
                    WHEN r.confidence < $properties.confidence 
                    THEN $properties.confidence 
                    ELSE r.confidence 
                END,
                r.lastSeen = datetime(),
                r.seenCount = coalesce(r.seenCount, 0) + 1,
                r.context = $properties.context,
                r.properties = $properties.properties
            RETURN r
        ";

            var parameters = new
            {
                fromId = rel.FromNodeId,
                toId = rel.ToNodeId,
                relType = rel.Type,
                properties,
                documentId = documentId.ToString()
            };

            await tx.RunAsync(query, parameters);
        }

        private async Task LinkPageToNodesAsync(
            IAsyncTransaction tx,
            Guid documentId,
            Guid pageId,
            IEnumerable<string> nodeIds,
            CancellationToken cancellationToken)
        {
            var query = @"
            MATCH (d:Document {id: $documentId})
            MATCH (p:Page {id: $pageId})
            MERGE (d)-[:HAS_PAGE]->(p)
            WITH p
            UNWIND $nodeIds AS nodeId
            MATCH (n:Entity {id: nodeId})
            MERGE (p)-[:MENTIONS]->(n)
        ";

            var parameters = new
            {
                documentId = documentId.ToString(),
                pageId = pageId.ToString(),
                nodeIds = nodeIds.ToArray()
            };

            await tx.RunAsync(query, parameters);
        }

        private async Task<List<string>> GetPageNodeIdsAsync(
            IAsyncTransaction tx,
            Guid pageId,
            CancellationToken cancellationToken)
        {
            var query = @"
            MATCH (p:Page {id: $pageId})-[:MENTIONS]->(n:Entity)
            RETURN collect(n.id) as nodeIds
        ";

            var parameters = new { pageId = pageId.ToString() };
            var result = await tx.RunAsync(query, parameters);
            var record = await result.SingleAsync(cancellationToken);

            return record["nodeIds"].As<List<string>>();
        }

        private async Task RemovePageLinksAsync(
            IAsyncTransaction tx,
            Guid pageId,
            CancellationToken cancellationToken)
        {
            var query = @"
            MATCH (p:Page {id: $pageId})-[r:MENTIONS]->()
            DELETE r
        ";

            await tx.RunAsync(query, new { pageId = pageId.ToString() });
        }

        private async Task RemoveOrphanedNodesAsync(
            IAsyncTransaction tx,
            List<string> nodeIds,
            CancellationToken cancellationToken)
        {
            if (!nodeIds.Any())
                return;

            var query = @"
            UNWIND $nodeIds AS nodeId
            MATCH (n:Entity {id: nodeId})
            OPTIONAL MATCH (n)<-[:MENTIONS]-(p:Page)
            WITH n, count(p) as refCount
            WHERE refCount = 0
            DETACH DELETE n
        ";

            await tx.RunAsync(query, new { nodeIds });
        }

        private async Task DeleteNodesAsync(
            IAsyncTransaction tx,
            List<string> nodeIds,
            CancellationToken cancellationToken)
        {
            if (!nodeIds.Any())
                return;

            var query = @"
            UNWIND $nodeIds AS nodeId
            MATCH (n:Entity {id: nodeId})
            DETACH DELETE n
        ";

            await tx.RunAsync(query, new { nodeIds });
        }

        //private async Task UpdateDocumentMetadataAsync(
        //   IAsyncTransaction tx,
        //   Guid documentId,
        //   int nodeCount,
        //   int relCount)
        //{
        //        var query = @"
        //        MATCH (d:Document {id: $documentId})
        //        SET d.lastProcessed = datetime(),
        //            d.nodeCount = coalesce(d.nodeCount, 0) + $nodeCount,
        //            d.relationshipCount = coalesce(d.relationshipCount, 0) + $relCount,
        //            d.processingStatus = 'Processed'
        //        RETURN d
        //    ";

        //    var parameters = new
        //    {
        //        documentId = documentId.ToString(),
        //        nodeCount,
        //        relCount
        //    };

        //    await tx.RunAsync(query, parameters);
        //}

        private DomainGraphQueryResult MapToDomainGraphQueryResult(List<IRecord> records)
        {
            if (!records.Any())
                return new DomainGraphQueryResult();

            var result = new DomainGraphQueryResult();
            var nodeIds = new HashSet<string>();
            var relKeys = new HashSet<string>();

            foreach (var record in records)
            {
                // Map source entity
                if (record.TryGet("sourceEntity", out INode sourceNode))
                {
                    var sourceId = sourceNode.Properties["id"].As<string>();
                    if (nodeIds.Add(sourceId))
                    {
                        result.Nodes.Add(new DomainNode
                        {
                            Id = sourceId,
                            Name = sourceNode.Properties["name"].As<string>(),
                            Type = sourceNode.Properties["type"].As<string>(),
                            Confidence = sourceNode.Properties.GetValueOrDefault("confidence")?.As<float>() ?? 0.8f,
                            Properties = sourceNode.Properties.GetValueOrDefault("properties") as Dictionary<string, object> ?? new()
                        });
                    }
                }

                // Map related entities
                if (record.TryGet("relatedEntities", out List<object> entityObjs))
                {
                    foreach (INode entityNode in entityObjs)
                    {
                        var entityId = entityNode.Properties["id"].As<string>();
                        if (nodeIds.Add(entityId))
                        {
                            result.Nodes.Add(new DomainNode
                            {
                                Id = entityId,
                                Name = entityNode.Properties["name"].As<string>(),
                                Type = entityNode.Properties["type"].As<string>(),
                                Confidence = entityNode.Properties.GetValueOrDefault("confidence")?.As<float>() ?? 0.8f,
                                Properties = entityNode.Properties.GetValueOrDefault("properties") as Dictionary<string, object> ?? new()
                            });
                        }
                    }
                }

                // Map relationships
                if (record.TryGet("relationships", out List<object> relObjs))
                {
                    foreach (IRelationship rel in relObjs)
                    {
                        var relKey = $"{rel.StartNodeId}_{rel.EndNodeId}_{rel.Type}";
                        if (relKeys.Add(relKey))
                        {
                            result.Relationships.Add(new DomainRelationship
                            {
                                //FromNodeId = rel.StartNodeId,
                                //ToNodeId = rel.EndNodeId,
                                Type = rel.Type,
                                Confidence = rel.Properties.GetValueOrDefault("confidence")?.As<float>() ?? 0.8f,
                                Context = rel.Properties.GetValueOrDefault("context")?.As<string>() ?? string.Empty,
                                Properties = rel.Properties.GetValueOrDefault("properties") as Dictionary<string, object> ?? new()
                            });
                        }
                    }
                }

                // Map documents
                if (record.TryGet("documents", out List<object> docObjs))
                {
                    foreach (var docObj in docObjs)
                    {
                        var docDict = docObj as Dictionary<string, object>;
                        if (docDict != null)
                        {
                            result.Documents.Add(new DomainDocumentReference
                            {
                                Id = Guid.Parse(docDict["id"].ToString()!),
                                Title = docDict["title"].ToString()!,
                                Source = docDict["source"].ToString()!,
                                ExternalId = docDict["externalId"].ToString()!
                            });
                        }
                    }
                }
            }

            return result;
        }

        private List<DomainNode> MapToDomainNodes(List<object> nodeObjs)
        {
            var nodes = new List<DomainNode>();

            foreach (INode nodeObj in nodeObjs)
            {
                var props = nodeObj.Properties;

                nodes.Add(new DomainNode
                {
                    Id = props["id"].As<string>(),
                    Name = props["name"].As<string>(),
                    Type = props["type"].As<string>(),
                    Confidence = props.GetValueOrDefault("confidence")?.As<float>() ?? 0.8f,
                    Properties = props.GetValueOrDefault("properties") as Dictionary<string, object> ?? new()
                });
            }

            return nodes;
        }

        private List<DomainRelationship> MapToDomainRelationships(List<object> relObjs)
        {
            var relationships = new List<DomainRelationship>();

            foreach (IRelationship relObj in relObjs)
            {
                var props = relObj.Properties;

                relationships.Add(new DomainRelationship
                {
                    //FromNodeId = relObj.StartNodeId,
                    //ToNodeId = relObj.EndNodeId,
                    Type = props["type"].As<string>(),
                    Confidence = props.GetValueOrDefault("confidence")?.As<float>() ?? 0.8f,
                    Context = props.GetValueOrDefault("context")?.As<string>() ?? string.Empty,
                    Properties = props.GetValueOrDefault("properties") as Dictionary<string, object> ?? new()
                });
            }

            return relationships;
        }

        private DomainBatchStatus MapToDomainBatchStatus(INode batchNode)
        {
            var props = batchNode.Properties;
            var status = new DomainBatchStatus
            {
                BatchId = props["id"].As<string>(),
                Status = props["status"].As<string>(),
                Created = GetDateTimeProperty(batchNode, "created") ?? DateTime.UtcNow,
                Completed = GetDateTimeProperty(batchNode, "completed"),
                TotalCount = props.GetValueOrDefault("totalCount")?.As<int>() ?? 0,
                ProcessedCount = props.GetValueOrDefault("processedCount")?.As<int>() ?? 0,
                SuccessCount = props.GetValueOrDefault("successCount")?.As<int>() ?? 0,
                FailedCount = props.GetValueOrDefault("failedCount")?.As<int>() ?? 0
            };

            // Map results if present
            if (props.TryGetValue("results", out var resultsObj) && resultsObj is List<object> resultsList)
            {
                status.Results = resultsList.Select(r =>
                {
                    var dict = r as Dictionary<string, object>;
                    return new DomainBatchDocumentResult
                    {
                        DocumentId = Guid.Parse(dict?["documentId"]?.ToString()!),
                        ExternalId = dict?["externalId"]?.ToString()!,
                        Title = dict?["title"]?.ToString()!,
                        PageNumber = Convert.ToInt32(dict?["pageNumber"]),
                        Updated = dict?.ContainsKey("updated") == true && (bool)dict["updated"]!,
                        Message = dict?["message"]?.ToString()!,
                        ExtractedNodes = Convert.ToInt32(dict?["extractedNodes"]),
                        ExtractedRelationships = Convert.ToInt32(dict?["extractedRelationships"]),
                        ProcessedAt = DateTime.Parse(dict?["processedAt"]?.ToString()!),
                        Warnings = dict?.ContainsKey("warnings") == true
                            ? (dict["warnings"] as List<object>)?.Select(w => w.ToString()!).ToList()
                            : null,
                        Error = dict?.ContainsKey("error") == true ? dict["error"]?.ToString() : null
                    };
                }).ToList();
            }

            // Map errors if present
            if (props.TryGetValue("errors", out var errorsObj) && errorsObj is List<object> errorsList)
            {
                status.Errors = errorsList.Select(e => e.ToString()!).ToList();
            }

            // Map metadata if present
            if (props.TryGetValue("metadata", out var metadataObj) && metadataObj is Dictionary<string, object> metadataDict)
            {
                status.Metadata = new DomainBatchMetadata
                {
                    InitiatedBy = metadataDict.GetValueOrDefault("initiatedBy")?.ToString(),
                    Source = metadataDict.GetValueOrDefault("source")?.ToString(),
                    StartTime = DateTime.Parse(metadataDict.GetValueOrDefault("startTime")?.ToString() ?? DateTime.UtcNow.ToString()),
                    EndTime = metadataDict.ContainsKey("endTime")
                        ? DateTime.Parse(metadataDict["endTime"]?.ToString()!)
                        : null,
                    TotalProcessingTimeMs = metadataDict.ContainsKey("totalProcessingTimeMs")
                        ? Convert.ToInt64(metadataDict["totalProcessingTimeMs"])
                        : null,
                    AverageProcessingTimeMs = metadataDict.ContainsKey("averageProcessingTimeMs")
                        ? Convert.ToDouble(metadataDict["averageProcessingTimeMs"])
                        : null,
                    CustomData = metadataDict.ContainsKey("customData")
                        ? metadataDict["customData"] as Dictionary<string, string>
                        : null
                };
            }

            return status;
        }

        private DateTime? GetDateTimeProperty(INode node, string propertyName)
        {
            if (node.Properties.TryGetValue(propertyName, out var value))
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
            }
            return null;
        }

        private DateTime? GetDateTimeProperty(IRecord record, string propertyName)
        {
            if (record.TryGet(propertyName, out ZonedDateTime zoned))
            {
                return zoned.UtcDateTime;
            }
            if (record.TryGet(propertyName, out LocalDateTime local))
            {
                return local.ToDateTime();
            }
            if (record.TryGet(propertyName, out string dateString) && DateTime.TryParse(dateString, out var parsed))
            {
                return parsed;
            }
            return null;
        }

        #endregion
    }
}
