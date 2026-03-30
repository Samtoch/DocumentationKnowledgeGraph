using Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using Application.Common.DTOs;

namespace Application.Documents.Queries.FindRelated
{
    public record FindRelatedQuery(
    string Source,
    string EntityType,
    string EntityName, 
    int Depth,
    string? Relationship = null) : IRequest<GraphQueryResultDto>;
}
