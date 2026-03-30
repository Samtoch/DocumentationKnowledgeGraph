using System;
using System.Collections.Generic;
using System.Text;
using Neo4j.Driver;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence
{
    /// <summary>
    /// Adapts Microsoft ILogger to Neo4j's ILogger interface
    /// </summary>
    public class Neo4jLoggerAdapter 
    {
        private readonly ILogger _logger;

        public Neo4jLoggerAdapter(ILogger logger)
        {
            _logger = logger;
        }

        public void Error(Exception cause, string message, params object[] args)
        {
            _logger.LogError(cause, message, args);
        }

        public void Info(string message, params object[] args)
        {
            _logger.LogInformation(message, args);
        }

        public void Warn(Exception cause, string message, params object[] args)
        {
            _logger.LogWarning(cause, message, args);
        }

        public void Debug(string message, params object[] args)
        {
            _logger.LogDebug(message, args);
        }

        public void Trace(string message, params object[] args)
        {
            _logger.LogTrace(message, args);
        }

        public bool IsDebugEnabled()
        {
            return _logger.IsEnabled(LogLevel.Debug);
        }

        public bool IsTraceEnabled()
        {
            return _logger.IsEnabled(LogLevel.Trace);
        }
    }
}
