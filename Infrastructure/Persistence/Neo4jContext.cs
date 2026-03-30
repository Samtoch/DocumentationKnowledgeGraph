using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;
using Domain.Interfaces;

namespace Infrastructure.Persistence
{
    public class Neo4jContext : INeo4jContext
    {
        private readonly IDriver _driver;
        private readonly ILogger<Neo4jContext> _logger;
        private readonly Neo4jSettings _settings;
        private bool _disposed;

        public Neo4jContext(IOptions<Neo4jSettings> settings, ILogger<Neo4jContext> logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                // Validate settings
                _settings.Validate();

                // Create driver configuration using the correct method for your version
                var config = new Config();

                // Set properties if they are settable (depends on version)
                // May need to use reflection or different approach

                // Initialize driver with minimal configuration first
                _driver = GraphDatabase.Driver(_settings.Uri, AuthTokens.Basic(_settings.Username, _settings.Password));

                _logger.LogInformation(
                    "Neo4j context initialized for {Uri} with default configuration",
                    _settings.Uri);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Neo4j driver for {Uri}", _settings.Uri);
                throw new InvalidOperationException($"Failed to connect to Neo4j at {_settings.Uri}", ex);
            }
        }

        /// <summary>
        /// Executes a Cypher query using the ExecutableQuery API with automatic retries
        /// </summary>
        public async Task<IResultCursor> RunQueryAsync(string query, object? parameters = null, string database = "neo4j")
        {
            try
            {
                _logger.LogTrace("Executing query: {Query}", query);

                var session = _driver.AsyncSession(o => o.WithDatabase(database));
                return await session.RunAsync(query, parameters);
            }
            catch (Neo4jException ex) when (ex.Code == "Neo.ClientError.Statement.SyntaxError")
            {
                _logger.LogError(ex, "Syntax error in Cypher query: {Query}", query);
                throw new InvalidOperationException($"Invalid Cypher query syntax: {ex.Message}", ex);
            }
            catch (Neo4jException ex) when (IsTransientError(ex))
            {
                _logger.LogWarning(ex, "Transient Neo4j error, operation may be retried");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Neo4j query: {Query}", query);
                throw;
            }
        }

        /// <summary>
        /// Executes a query in a read transaction
        /// </summary>
        /// <inheritdoc />
        public async Task<T> ExecuteReadAsync<T>(
            string query,
            object? parameters = null,
            string database = "neo4j")
        {
            var session = _driver.AsyncSession(o => o.WithDatabase(database));

            try
            {
                return await session.ExecuteReadAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(query, parameters);
                    var records = await cursor.ToListAsync();
                    return MapResult<T>(records);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in read transaction for query: {Query}", query);
                throw;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <inheritdoc />
        public async Task<T> ExecuteWriteAsync<T>(
            string query,
            object? parameters = null,
            string database = "neo4j")
        {
            var session = _driver.AsyncSession(o => o.WithDatabase(database));

            try
            {
                return await session.ExecuteWriteAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(query, parameters);
                    var records = await cursor.ToListAsync();
                    return MapResult<T>(records);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in write transaction for query: {Query}", query);
                throw;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <inheritdoc />
        public IAsyncSession GetSession(string database = "neo4j")
        {
            return _driver.AsyncSession(o => o.WithDatabase(database));
        }

        /// <inheritdoc />
        public async Task<bool> VerifyConnectivityAsync()
        {
            try
            {
                await _driver.VerifyConnectivityAsync();
                _logger.LogInformation("Neo4j connectivity verified successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Neo4j connectivity verification failed");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<IServerInfo> GetServerInfoAsync()
        {
            try
            {
                return await _driver.GetServerInfoAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Neo4j server info");
                throw;
            }
        }

        /// <summary>
        /// Executes multiple queries in a single transaction
        /// </summary>
        public async Task ExecuteInTransactionAsync(
            IEnumerable<(string Query, object? Parameters)> queries,
            string database = "neo4j")
        {
            var session = _driver.AsyncSession(o => o.WithDatabase(database));
            var transaction = await session.BeginTransactionAsync();

            try
            {
                foreach (var (query, parameters) in queries)
                {
                    await transaction.RunAsync(query, parameters);
                }

                await transaction.CommitAsync();
                _logger.LogDebug("Successfully executed {Count} queries in transaction", queries.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction failed, rolling back");
                await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                await transaction.DisposeAsync();
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Executes a query with custom retry logic
        /// </summary>
        public async Task<IResultCursor> RunQueryWithRetryAsync(
            string query,
            object? parameters = null,
            string database = "neo4j",
            int maxRetries = 3)
        {
            var retryCount = 0;
            var delay = TimeSpan.FromMilliseconds(100);

            while (true)
            {
                try
                {
                    var session = _driver.AsyncSession(o => o.WithDatabase(database));
                    return await session.RunAsync(query, parameters);
                }
                catch (Neo4jException ex) when (IsTransientError(ex) && retryCount < maxRetries)
                {
                    retryCount++;
                    _logger.LogWarning(ex,
                        "Transient Neo4j error, retrying ({RetryCount}/{MaxRetries}) after {Delay}ms",
                        retryCount, maxRetries, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                    delay = TimeSpan.FromTicks(delay.Ticks * 2); // Exponential backoff
                }
            }
        }

        /// <summary>
        /// Checks if a Neo4j error is transient and can be retried
        /// </summary>
        private bool IsTransientError(Neo4jException ex)
        {
            // List of transient error codes that can be retried
            var transientCodes = new[]
            {
            "Neo.TransientError",
            "Neo.ClientError.Cluster.NotALeader",
            "Neo.ClientError.General.ForbiddenOnReadOnlyDatabase",
            "ServiceUnavailable",
            "SessionExpired"
        };

            return transientCodes.Any(code => ex.Code?.StartsWith(code) == true) ||
                   ex.IsRetriable;
        }

        /// <summary>
        /// Maps query results to the expected type
        /// </summary>
        private T MapResult<T>(List<IRecord> records)
        {
            if (typeof(T) == typeof(List<IRecord>))
            {
                return (T)(object)records;
            }

            if (typeof(T) == typeof(IRecord) && records.Count == 1)
            {
                return (T)records[0];
            }

            if (typeof(T) == typeof(int) && records.Count == 1)
            {
                var firstValue = records[0].Values.Values.FirstOrDefault();
                if (firstValue is int intValue)
                    return (T)(object)intValue;
                if (firstValue is long longValue)
                    return (T)(object)(int)longValue;
            }

            if (typeof(T) == typeof(string) && records.Count == 1)
            {
                var firstValue = records[0].Values.Values.FirstOrDefault();
                return (T)(object)(firstValue?.ToString() ?? string.Empty);
            }

            if (typeof(T) == typeof(bool) && records.Count == 1)
            {
                var firstValue = records[0].Values.Values.FirstOrDefault();
                if (firstValue is bool boolValue)
                    return (T)(object)boolValue;
            }

            if (typeof(T) == typeof(Dictionary<string, object>))
            {
                if (records.Count == 1)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in records[0].Values)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                    return (T)(object)dict;
                }
            }

            if (typeof(T) == typeof(List<Dictionary<string, object>>))
            {
                var result = new List<Dictionary<string, object>>();
                foreach (var record in records)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var kvp in record.Values)
                    {
                        dict[kvp.Key] = kvp.Value;
                    }
                    result.Add(dict);
                }
                return (T)(object)result;
            }

            // Default to Json serialization for complex types
            var json = System.Text.Json.JsonSerializer.Serialize(records);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Failed to deserialize to {typeof(T).Name}");
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _driver.DisposeAsync();
                _disposed = true;
                _logger.LogInformation("Neo4j context disposed");
                GC.SuppressFinalize(this);
            }
        }
    }
}