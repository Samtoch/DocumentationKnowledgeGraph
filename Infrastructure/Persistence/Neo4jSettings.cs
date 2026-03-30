using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence
{
    // Infrastructure/Persistence/Neo4jSettings.cs
    public class Neo4jSettings
    {
        public string Uri { get; set; } = "bolt://localhost:7687";
        public string Username { get; set; } = "neo4j";
        public string Password { get; set; }
        public string Database { get; set; } = "neo4j";

        // Connection settings
        public int ConnectionTimeoutSeconds { get; set; } = 30;
        public int ConnectionAcquisitionTimeoutSeconds { get; set; } = 60;
        public int MaxConnectionPoolSize { get; set; } = 100;
        public int ConnectionIdleTimeoutMinutes { get; set; } = 10;

        // Performance
        public int FetchSize { get; set; } = 1000;

        // Security
        public bool EncryptionEnabled { get; set; } = false;

        // Logging
        public bool EnableLogging { get; set; } = true;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Uri))
                throw new ArgumentException("Neo4j Uri is required");

            if (string.IsNullOrWhiteSpace(Username))
                throw new ArgumentException("Neo4j Username is required");

            if (string.IsNullOrWhiteSpace(Password))
                throw new ArgumentException("Neo4j Password is required");
        }
    }

}
