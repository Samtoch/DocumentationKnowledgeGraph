using Application.Common.DTOs;
using Domain.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Queries.GetDocumentStatus
{
    public class GetDocumentStatusQueryHandler : IRequestHandler<GetDocumentStatusQuery, DocumentStatusDto?>
    {
        private readonly IDocumentRepository _documentRepository;

        public GetDocumentStatusQueryHandler(IDocumentRepository documentRepository)
        {
            _documentRepository = documentRepository;
        }

        public async Task<DocumentStatusDto?> Handle(GetDocumentStatusQuery request, CancellationToken cancellationToken)
        {
            var document = await _documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);

            if (document == null)
                return null;

            return new DocumentStatusDto
            {
                DocumentId = document.Id,
                Title = document.Title,
                Source = document.Source,
                ExternalId = document.ExternalId,
                PageCount = document.Pages.Count,
                NodeCount = document.Pages.Sum(p => p.Nodes.Count),
                RelationshipCount = document.Pages.Sum(p => p.Relationships.Count),
                LastModified = document.LastModified,
                Status = document.ProcessingStatus
            };
        }
    }
}
