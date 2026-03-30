using System;
using System.Collections.Generic;
using System.Text;
using MediatR;
using Application.Common.DTOs;

namespace Application.Documents.Commands
{
    public record ProcessDocumentUpdateCommand(
    string Source,
    string ExternalId,
    string Title,
    string Content,
    int PageNumber,
    DateTime LastModified,
    Dictionary<string, string> Metadata
    ) : IRequest<DocumentIngestionResult>;

}
