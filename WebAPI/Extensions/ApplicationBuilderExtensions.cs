using NLog;
using Domain.Interfaces;

namespace WebAPI.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        private static readonly NLog.ILogger Logger = LogManager.GetCurrentClassLogger();

        public static async Task EnsureNeo4jConnectedAsync(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var neo4jContext = scope.ServiceProvider.GetRequiredService<INeo4jContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            try
            {
                Logger.Info("Verifying Neo4j database connectivity...");

                var isConnected = await neo4jContext.VerifyConnectivityAsync();

                if (isConnected)
                {
                    Logger.Info("Successfully connected to Neo4j database at {Uri}",
                        configuration["Neo4j:Uri"]);
                }
                else
                {
                    Logger.Error("Failed to connect to Neo4j database at {Uri}",
                        configuration["Neo4j:Uri"]);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error connecting to Neo4j database at {Uri}",
                    configuration["Neo4j:Uri"]);
            }
        }
    }
}
