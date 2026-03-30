using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Common.Exceptions
{
    public class ConflictException : ApplicationException
    {
        public ConflictException(string message) : base(message) { }
    }
}
