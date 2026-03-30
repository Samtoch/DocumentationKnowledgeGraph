using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;
using System.Text;

namespace WebAPI.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddSwaggerGenWithAuth(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo  // Now this will work
                {
                    Title = "Documentation Knowledge Graph API",
                    Version = "v1",
                    Description = "API for ingesting and querying software documentation in a knowledge graph",
                    Contact = new OpenApiContact
                    {
                        Name = "Documentation KG Team",
                        Email = "docs-kg@example.com",
                        Url = new Uri("https://github.com/your-repo")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "MIT License",
                        Url = new Uri("https://opensource.org/licenses/MIT")
                    }
                });

                // ... rest of your configuration
            });

            return services;
        }

        public static IServiceCollection AddApiVersioning_(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new QueryStringApiVersionReader("api-version"),
                    new HeaderApiVersionReader("X-API-Version"),
                    new MediaTypeApiVersionReader("ver"));
            });

            // Add API version explorer (for Swagger)
            services.AddVersionedApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
            });

            return services;
        }
    }
}
