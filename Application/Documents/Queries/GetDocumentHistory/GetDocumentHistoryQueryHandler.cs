using Application.Common.DTOs;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Queries.GetDocumentHistory
{
    public class GetDocumentHistoryQueryHandler : IRequestHandler<GetDocumentHistoryQuery, DocumentHistoryResult?>
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IGraphRepository _graphRepository;
        private readonly ILogger<GetDocumentHistoryQueryHandler> _logger;
        private readonly IMapper _mapper;

        public GetDocumentHistoryQueryHandler(
            IDocumentRepository documentRepository,
            IGraphRepository graphRepository, IMapper mapper,
            ILogger<GetDocumentHistoryQueryHandler> logger)
        {
            _documentRepository = documentRepository;
            _graphRepository = graphRepository;
            _logger = logger; 
            _mapper = mapper;
        }

        public async Task<DocumentHistoryResult?> Handle(
            GetDocumentHistoryQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Getting history for document {DocumentId}", request.DocumentId);

                // First check if document exists
                var document = await _documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);
                if (document == null)
                {
                    _logger.LogWarning("Document {DocumentId} not found", request.DocumentId);
                    return null;
                }

                // Get history from graph repository
                var domainHistory = await _graphRepository.GetDocumentHistoryAsync(
                    request.DocumentId,
                    request.IncludeExtractions,
                    request.Page,
                    request.PageSize,
                    cancellationToken);

                // Implement Automapper here if needed to map from domain result to DTO result
                //DocumentHistoryResult dto = Map<DomainDocumentHistoryResult>(history);

                // If no history found, return basic document info
                if (domainHistory == null)
                {
                    return new DocumentHistoryResult
                    {
                        DocumentId = document.Id,
                        Title = document.Title,
                        Versions = new List<DocumentVersion>(),
                        TotalVersions = 0
                    };
                }

                var dtoHistory = _mapper.Map<DocumentHistoryResult>(domainHistory);

                return dtoHistory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting history for document {DocumentId}", request.DocumentId);
                throw;
            }
        }
    }
}
