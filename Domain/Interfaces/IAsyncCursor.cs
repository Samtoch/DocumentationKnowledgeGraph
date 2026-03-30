using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Interfaces
{
    public interface IAsyncCursor : IAsyncDisposable
    {
        Task<bool> FetchAsync();
        IReadOnlyList<object> Current { get; }
    }
}
