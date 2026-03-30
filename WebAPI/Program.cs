
using Application;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Domain.Interfaces;
using Infrastructure;
using Infrastructure.Persistence.Repositories;
using Infrastructure.SemanticKernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using NLog;
using NLog.Web;
using WebAPI.Extensions;
using WebAPI.Middleware;

namespace WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
            logger.Debug("Starting Documentation Knowledge Graph API");

            try
            {
                var builder = WebApplication.CreateBuilder(args);

                // Configure NLog
                builder.Logging.ClearProviders();
                builder.Host.UseNLog();

                // Configure Semantic Kernel
                builder.Services.AddKernel();

                // Add services
                builder.Services.AddApplication();
                builder.Services.AddInfrastructure();

                // Register repositories
                builder.Services.AddScoped<IGraphRepository, Neo4jGraphRepository>();
                builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

                // Configure OpenAI
                var openAIApiKey = builder.Configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API Key is required");

                builder.Services.AddOpenAIChatCompletion(
                    modelId: builder.Configuration["OpenAI:ModelId"] ?? "gpt-4",
                    apiKey: openAIApiKey
                );

                builder.Services.AddOpenAITextEmbeddingGeneration(
                    modelId: builder.Configuration["OpenAI:EmbeddingModelId"] ?? "text-embedding-3-small",
                    apiKey: openAIApiKey
                );

                // Configure API versioning - use a different method name to avoid conflicts
                builder.Services.AddApiVersioning(options =>
                {
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                });

                builder.Services.AddVersionedApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

                // Add API services
                builder.Services.AddControllers(options =>
                {
                    options.Filters.Add<ApiExceptionFilterAttribute>();
                });

                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Title = "Documentation Knowledge Graph API",
                        Version = "v1",
                        Description = "API for ingesting and querying software documentation in a knowledge graph"
                    });
                });

                builder.Services.AddProblemDetails();
                builder.Services.AddHttpContextAccessor();

                // Add CORS
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll", policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
                });

                // Add Response Caching
                builder.Services.AddResponseCaching();
                builder.Services.AddMemoryCache();

                // Health Checks
                builder.Services.AddHealthChecks()
                    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

                var app = builder.Build();

                // Configure pipeline
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }
                else
                {
                    app.UseHsts();
                    app.UseHttpsRedirection();
                }

                app.UseMiddleware<ErrorHandlingMiddleware>();
                app.UseMiddleware<RequestLoggingMiddleware>();
                app.UseCors("AllowAll");
                app.UseResponseCaching();
                app.MapControllers();
                app.MapHealthChecks("/health");

                logger.Info("Documentation Knowledge Graph API started successfully");
                app.Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Application failed to start");
                throw;
            }
            finally
            {
                LogManager.Shutdown();
            }
        }
    }
}
