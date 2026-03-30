using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using System.Text.Json;

namespace WebAPI.Extensions
{
    public static class HealthCheckResponseWriter
    {
        public static async Task WriteResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration,
                timestamp = DateTime.UtcNow,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration,
                    tags = e.Value.Tags,
                    error = e.Value.Exception?.Message,
                    data = e.Value.Data.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.ToString() ?? string.Empty)
                }),
                infrastructure = new
                {
                    machineName = Environment.MachineName,
                    osVersion = Environment.OSVersion.ToString(),
                    processorCount = Environment.ProcessorCount,
                    workingSet = Environment.WorkingSet,
                    userDomainName = Environment.UserDomainName
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options), Encoding.UTF8);
        }
    }
}
