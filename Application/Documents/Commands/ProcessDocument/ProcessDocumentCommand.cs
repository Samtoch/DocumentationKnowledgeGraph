using Application.Common.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Commands.ProcessDocument
{
    public record ProcessDocumentCommand(
    string Source,
    string ExternalId,
    string Title,
    string Content,
    int PageNumber,
    DateTime LastModified,
    Dictionary<string, string>? Metadata = null) : IRequest<DocumentIngestionResultDto>;
}
