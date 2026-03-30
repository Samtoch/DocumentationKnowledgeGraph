using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Queries.GetDocumentHistory
{
    public class GetDocumentHistoryQueryValidator : AbstractValidator<GetDocumentHistoryQuery>
    {
        public GetDocumentHistoryQueryValidator()
        {
            RuleFor(v => v.DocumentId)
                .NotEmpty().WithMessage("DocumentId is required");

            RuleFor(v => v.Page)
                .GreaterThan(0).WithMessage("Page must be greater than 0");

            RuleFor(v => v.PageSize)
                .GreaterThan(0).WithMessage("PageSize must be greater than 0")
                .LessThanOrEqualTo(100).WithMessage("PageSize must not exceed 100");
        }
    }
}
