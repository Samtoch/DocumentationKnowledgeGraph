using System;
using System.Collections.Generic;
using System.Text;
using Neo4j.Driver;

namespace Domain.Interfaces
{
    public interface INeo4jContext : IAsyncDisposable
    {
        /// <summary>
        /// Executes a Cypher query and returns the result cursor
        /// </summary>
        Task<IResultCursor> RunQueryAsync(
            string query,
            object? parameters = null,
            string database = "neo4j");

        /// <summary>
        /// Executes a Cypher query in a read transaction
        /// </summary>
        Task<T> ExecuteReadAsync<T>(
            string query,
            object? parameters = null,
            string database = "neo4j");

        /// <summary>
        /// Executes a Cypher query in a write transaction
        /// </summary>
        Task<T> ExecuteWriteAsync<T>(
            string query,
            object? parameters = null,
            string database = "neo4j");

        /// <summary>
        /// Gets a session for manual transaction management
        /// </summary>
        IAsyncSession GetSession(string database = "neo4j");

        /// <summary>
        /// Verifies database connectivity
        /// </summary>
        Task<bool> VerifyConnectivityAsync();

        /// <summary>
        /// Gets database server information
        /// </summary>
        Task<IServerInfo> GetServerInfoAsync();

        /// <summary>
        /// Executes multiple queries in a single transaction
        /// </summary>
        Task ExecuteInTransactionAsync(
            IEnumerable<(string Query, object? Parameters)> queries,
            string database = "neo4j");

        /// <summary>
        /// Executes a query with retry logic
        /// </summary>
        Task<IResultCursor> RunQueryWithRetryAsync(
            string query,
            object? parameters = null,
            string database = "neo4j",
            int maxRetries = 3);
    }
}
