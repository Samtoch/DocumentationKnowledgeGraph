using Application;
using Domain;
using Domain.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static OllamaSharp.OllamaApiClient;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            // Add services
            services.AddApplication();

            // Configure Neo4j settings from appsettings.json
            services.Configure<Neo4jSettings>(options =>
            {
                var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

                options.Uri = config["Neo4j:Uri"] ?? "bolt://localhost:7687";
                options.Username = config["Neo4j:Username"] ?? "neo4j";
                options.Password = config["Neo4j:Password"] ?? throw new InvalidOperationException("Neo4j:Password is required");
            });

            // Register Neo4j settings
            //services.Configure<Neo4jSettings>(configuration.GetSection("Neo4j"));

            // Register Neo4jContext with DI
            services.AddScoped<INeo4jContext, Neo4jContext>();

            // Register repositories
            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IGraphRepository, Neo4jGraphRepository>();
            services.AddScoped<IEntityExtractor, SemanticKernelEntityExtractor>();

            return services;
        }
    }
}
