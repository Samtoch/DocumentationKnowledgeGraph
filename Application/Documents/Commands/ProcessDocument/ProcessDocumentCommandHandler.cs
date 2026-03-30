using Application.Common.DTOs;
using Application.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

namespace Application.Documents.Commands.ProcessDocument
{
    public class ProcessDocumentCommandHandler : IRequestHandler<ProcessDocumentCommand, DocumentIngestionResultDto>
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IGraphRepository _graphRepository;
        private readonly IEntityExtractor _entityExtractor;
        private readonly ILogger<ProcessDocumentCommandHandler> _logger;

        public ProcessDocumentCommandHandler(IDocumentRepository documentRepository, IGraphRepository graphRepository, IEntityExtractor entityExtractor, ILogger<ProcessDocumentCommandHandler> logger)
        {
            _documentRepository = documentRepository;
            _graphRepository = graphRepository;
            _entityExtractor = entityExtractor;
            _logger = logger;
        }

        public async Task<DocumentIngestionResultDto> Handle(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            var warnings = new List<string>();

            try
            {
                // Get or create document
                var document = await GetOrCreateDocumentAsync(request, cancellationToken);

                // Get or create page
                var page = await GetOrCreatePageAsync(document, request, cancellationToken);

                // Check for changes
                if (!page.HasContentChanged(request.Content))
                {
                    _logger.LogDebug("No changes detected for document {DocumentId} page {PageNumber}",
                        document.Id, request.PageNumber);

                    return new DocumentIngestionResultDto
                    {
                        DocumentId = document.Id,
                        ExternalId = request.ExternalId,
                        Updated = false,
                        Message = "No changes detected",
                        ProcessedAt = DateTime.UtcNow
                    };
                }

                // Update page content
                page.UpdateContent(request.Content);

                // Extract entities
                var extraction = await _entityExtractor.ExtractAsync(
                    request.Content,
                    request.Source,
                    request.PageNumber,
                    cancellationToken);

                if (!extraction.Nodes.Any())
                {
                    warnings.Add("No entities were extracted from the document content");
                }

                // Store in graph database
                await _graphRepository.StoreExtractionAsync(
                    document.Id,
                    page.Id,
                    extraction.Nodes,
                    extraction.Relationships,
                    cancellationToken);

                // Update document metadata
                document.UpdateMetadata(request.Title, request.LastModified);
                if (request.Metadata?.Any() == true)
                {
                    foreach (var kvp in request.Metadata)
                    {
                        document.AddMetadata(kvp.Key, kvp.Value);
                    }
                }
                document.MarkAsProcessed();

                await _documentRepository.UpdateAsync(document, cancellationToken);

                _logger.LogInformation(
                    "Successfully processed document {DocumentId} page {PageNumber}. " +
                    "Extracted {NodeCount} nodes and {RelCount} relationships",
                    document.Id, request.PageNumber, extraction.Nodes.Count, extraction.Relationships.Count);

                return new DocumentIngestionResultDto
                {
                    DocumentId = document.Id,
                    ExternalId = request.ExternalId,
                    Updated = true,
                    Message = "Document processed successfully",
                    ExtractedNodes = extraction.Nodes.Count,
                    ExtractedRelationships = extraction.Relationships.Count,
                    Warnings = warnings,
                    ProcessedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document {ExternalId}", request.ExternalId);
                throw new Exception($"Failed to process document: {ex.Message}", ex);
            }
        }

        private async Task<Domain.Entities.Document> GetOrCreateDocumentAsync(ProcessDocumentCommand request, CancellationToken cancellationToken)
        {
            var document = await _documentRepository.GetByExternalIdAsync(request.Source, request.ExternalId, cancellationToken);

            if (document == null)
            {
                document = new Domain.Entities.Document(
                    request.Source,
                    request.ExternalId,
                    request.Title,
                    request.LastModified);

                document = await _documentRepository.AddAsync(document, cancellationToken);
                _logger.LogInformation("Created new document {DocumentId} from {Source}",
                    document.Id, request.Source);
            }

            return document;
        }

        private async Task<Page> GetOrCreatePageAsync(
            Domain.Entities.Document document,
            ProcessDocumentCommand request,
            CancellationToken cancellationToken)
        {
            var page = document.Pages.FirstOrDefault(p => p.PageNumber == request.PageNumber);

            if (page == null)
            {
                var addResult = document.AddPage(request.PageNumber, request.Content);
                if (!addResult.IsSuccess)
                    throw new ConflictException(addResult.Error);

                page = addResult.Value;
                await _documentRepository.UpdateAsync(document, cancellationToken);
                _logger.LogDebug("Added new page {PageNumber} to document {DocumentId}",
                    request.PageNumber, document.Id);
            }

            return page;
        }
    }

}
