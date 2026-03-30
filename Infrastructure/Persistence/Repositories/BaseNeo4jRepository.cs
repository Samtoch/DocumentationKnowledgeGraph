using Microsoft.Extensions.Logging;
using Neo4j.Driver;
//using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Persistence.Repositories
{
    //public abstract class BaseNeo4jRepository
    //{
    //    protected readonly INeo4jContext _context;
    //    protected readonly ILogger _logger;
    //    protected readonly JsonSerializerOptions _jsonOptions;

    //    protected BaseNeo4jRepository(INeo4jContext context, ILogger logger)
    //    {
    //        _context = context;
    //        _logger = logger;
    //        _jsonOptions = new JsonSerializerOptions
    //        {
    //            PropertyNameCaseInsensitive = true,
    //            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    //        };
    //    }

    //    protected async Task<IResultCursor> RunQueryAsync(
    //        string query,
    //        object parameters = null,
    //        string database = "neo4j")
    //    {
    //        return await _context.RunQueryAsync(query, parameters, database);
    //    }

    //    protected async Task<T> ExecuteInTransactionAsync<T>(Func<IAsyncTransaction, Task<T>> action)
    //    {
    //        using var session = _context.GetSession();
    //        using var transaction = await session.BeginTransactionAsync();

    //        try
    //        {
    //            var result = await action(transaction);
    //            await transaction.CommitAsync();
    //            return result;
    //        }
    //        catch (Exception ex)
    //        {
    //            await transaction.RollbackAsync();
    //            _logger.LogError(ex, "Transaction failed, rolling back");
    //            throw;
    //        }
    //    }

    //    protected async Task ExecuteInTransactionAsync(Func<IAsyncTransaction, Task> action)
    //    {
    //        using var session = _context.GetSession();
    //        using var transaction = await session.BeginTransactionAsync();

    //        try
    //        {
    //            await action(transaction);
    //            await transaction.CommitAsync();
    //        }
    //        catch (Exception ex)
    //        {
    //            await transaction.RollbackAsync();
    //            _logger.LogError(ex, "Transaction failed, rolling back");
    //            throw;
    //        }
    //    }

    //    protected DateTime? GetDateTimeProperty(IRecord record, string key)
    //    {
    //        if (record.TryGet<ZonedDateTime>(key, out var zonedDateTime))
    //        {
    //            return zonedDateTime.UtcDateTime;
    //        }
    //        if (record.TryGet<LocalDateTime>(key, out var localDateTime))
    //        {
    //            return localDateTime.ToDateTime();
    //        }
    //        if (record.TryGet<string>(key, out var dateString) &&
    //            DateTime.TryParse(dateString, out var parsedDate))
    //        {
    //            return parsedDate;
    //        }
    //        return null;
    //    }

    //    protected T DeserializeNodeProperties<T>(IReadOnlyDictionary<string, object> properties) where T : class
    //    {
    //        var json = JsonSerializer.Serialize(properties);
    //        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    //    }
    //}
}
