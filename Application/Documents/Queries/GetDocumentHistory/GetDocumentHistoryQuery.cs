using Application.Common.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Queries.GetDocumentHistory
{
    /// <summary>
    /// Query to get the change history of a document
    /// </summary>
    /// <param name="DocumentId">The document identifier</param>
    /// <param name="IncludeExtractions">Whether to include extracted entities in the history</param>
    /// <param name="Page">Page number for pagination</param>
    /// <param name="PageSize">Page size for pagination</param>
    public record GetDocumentHistoryQuery(
        Guid DocumentId,
        bool IncludeExtractions = false,
        int Page = 1,
        int PageSize = 10
    ) : IRequest<DocumentHistoryResult?>;
}
