using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;

namespace WebAPI.Extensions
{
    //public class ApiKeyOperationFilter : IOperationFilter
    //{
    //    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    //    {
    //        // Add API key parameter to endpoints that modify data
    //        var httpMethod = context.ApiDescription.HttpMethod;

    //        if (httpMethod == "POST" || httpMethod == "PUT" || httpMethod == "PATCH" || httpMethod == "DELETE")
    //        {
    //            operation.Parameters ??= new List<OpenApiParameter>();

    //            operation.Parameters.Add(new OpenApiParameter
    //            {
    //                Name = "X-API-Key",
    //                In = ParameterLocation.Header,
    //                Required = true,
    //                Schema = new OpenApiSchema
    //                {
    //                    Type = "string"
    //                },
    //                Description = "API Key for authentication (required for write operations)"
    //            });
    //        }
    //    }
    //}
}
