using Domain.ValueObjects;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Interfaces
{
    public interface IEntityExtractor
    {
        Task<ExtractionResult> ExtractAsync(
            string content,
            string source,
            int pageNumber = 1,
            CancellationToken cancellationToken = default);

        Task<ExtractionResult> ExtractWithContextAsync(
            string content,
            string source,
            Dictionary<string, object> context,
            CancellationToken cancellationToken = default);
    }
}
