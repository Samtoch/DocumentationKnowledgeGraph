using Application.Common.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Queries.GetDocumentStatus
{
    public record GetDocumentStatusQuery(Guid DocumentId) : IRequest<DocumentStatusDto?>;
}
