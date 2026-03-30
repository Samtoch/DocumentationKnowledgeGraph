using Application.Common.DTOs;
using AutoMapper;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.Mappings
{
    public class GraphMappingProfile : Profile
    {
        public GraphMappingProfile()
        {
            // Domain to DTO mappings
            CreateMap<DomainGraphQueryResult, GraphQueryResultDto>();
            //CreateMap<DomainNode, NodeDto>();
            //CreateMap<DomainRelationship, RelationshipDto>();
            //CreateMap<DomainDocumentReference, DocumentReferenceDto>();

            //// Entity statistics mappings
            //CreateMap<DomainEntityStatistics, EntityStatisticsDto>();
            //CreateMap<DomainTopEntity, TopEntityDto>();

            //// Document history mappings
            CreateMap<DomainDocumentHistoryResult, DocumentHistoryResult>();
            //CreateMap<DomainDocumentVersion, DocumentVersionDto>();

            //// Batch mappings
            //CreateMap<DomainBatchStatus, BatchStatusDto>();
            //CreateMap<DomainBatchDocumentResult, BatchDocumentResultDto>();
            //CreateMap<DomainBatchSummary, BatchSummaryDto>();

            //// Entity summary mappings
            //CreateMap<DomainEntitySummary, EntitySummaryDto>();
        }
    }
}
