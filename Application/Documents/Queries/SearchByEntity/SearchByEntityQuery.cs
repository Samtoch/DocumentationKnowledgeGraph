using Application.Common.DTOs;
using Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Queries.SearchByEntity
{
    public record SearchByEntityQuery(
    string EntityName,
    string? EntityType = null,
    string? Source = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Page = 1,
    int PageSize = 20,
    string SortBy = "lastModified",
    bool SortDescending = true) : IRequest<SearchResult<DocumentReference>>;
}
