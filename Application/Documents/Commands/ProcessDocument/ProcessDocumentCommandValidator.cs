using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Documents.Commands.ProcessDocument
{
    public class ProcessDocumentCommandValidator : AbstractValidator<ProcessDocumentCommand>
    {
        public ProcessDocumentCommandValidator()
        {
            RuleFor(v => v.Source)
                .NotEmpty().WithMessage("Source is required")
                .MaximumLength(50).WithMessage("Source must not exceed 50 characters");

            RuleFor(v => v.ExternalId)
                .NotEmpty().WithMessage("ExternalId is required")
                .MaximumLength(100).WithMessage("ExternalId must not exceed 100 characters");

            RuleFor(v => v.Title)
                .NotEmpty().WithMessage("Title is required")
                .MaximumLength(500).WithMessage("Title must not exceed 500 characters");

            RuleFor(v => v.Content)
                .NotEmpty().WithMessage("Content is required");

            RuleFor(v => v.PageNumber)
                .GreaterThan(0).WithMessage("PageNumber must be greater than 0");

            RuleFor(v => v.LastModified)
                .NotEmpty().WithMessage("LastModified is required")
                .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("LastModified cannot be in the future");
        }
    }
}
