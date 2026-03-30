using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.Exceptions
{
    public class ValidationException : ApplicationException
    {
        public ValidationException(IReadOnlyDictionary<string, string[]> errors)
            : base("Validation failed") => Errors = errors;

        public IReadOnlyDictionary<string, string[]> Errors { get; }
    }
}
