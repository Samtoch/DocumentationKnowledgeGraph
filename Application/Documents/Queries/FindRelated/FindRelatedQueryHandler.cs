using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using Application.Common.DTOs;
using AutoMapper;

namespace Application.Documents.Queries.FindRelated
{
    public class FindRelatedQueryHandler : IRequestHandler<FindRelatedQuery, GraphQueryResultDto>
    {
        private readonly IGraphRepository _graphRepository;
        private readonly IMapper _mapper;

        public FindRelatedQueryHandler(IGraphRepository graphRepository, IMapper mapper)
        {
            _graphRepository = graphRepository;
            _mapper = mapper;
        }

        public async Task<GraphQueryResultDto> Handle(
            FindRelatedQuery request,
            CancellationToken cancellationToken)
        {
            // Get Domain model from repository
            var domainResult = await _graphRepository.FindRelatedAsync(
                request.Source,
                request.EntityType,
                request.EntityName,
                request.Relationship,
                request.Depth,
                cancellationToken);

            // Map Domain model to Application DTO
            return _mapper.Map<GraphQueryResultDto>(domainResult);
        }
    }
}
