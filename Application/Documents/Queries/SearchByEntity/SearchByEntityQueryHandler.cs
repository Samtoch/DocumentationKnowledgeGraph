using Application.Common.DTOs;
using AutoMapper;
using Domain.Interfaces;
using Domain.ValueObjects;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Queries.SearchByEntity
{
    public class SearchByEntityQueryHandler : IRequestHandler<SearchByEntityQuery, SearchResult<DocumentReference>>
    {
        private readonly IGraphRepository _graphRepository;
        private readonly IMapper _mapper;

        public SearchByEntityQueryHandler(IMapper mapper, IGraphRepository graphRepository)
        {
            _graphRepository = graphRepository;
            _mapper = mapper;
        }

        public async Task<SearchResult<DocumentReference>> Handle(
            SearchByEntityQuery request,
            CancellationToken cancellationToken)
        {
            var documents = await _graphRepository.GetDocumentsByEntityAsync(
                request.EntityName,
                request.EntityType,
                request.Source,
                request.FromDate,
                request.ToDate,
                request.Page,
                request.PageSize,
                request.SortBy,
                request.SortDescending,
                cancellationToken);

            var totalCount = await _graphRepository.GetDocumentsByEntityCountAsync(
                request.EntityName,
                request.EntityType,
                request.Source,
                request.FromDate,
                request.ToDate,
                cancellationToken);

            var documentDto = _mapper.Map<List<DocumentReference>>(documents);

            return new SearchResult<DocumentReference>
            {
                Items = documentDto.ToList(),
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };
        }
    }
}
