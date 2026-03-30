using Application.Common.Exceptions;
using FluentValidation;
using NLog;
using System.Text.Json;
using WebAPI.Models;

namespace WebAPI.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IWebHostEnvironment _environment;

        public ErrorHandlingMiddleware(RequestDelegate next, IWebHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            Logger.Error(exception, "An error occurred while processing request: {Message} - Path: {Path}",
                exception.Message, context.Request.Path);

            var response = new ErrorResponse
            {
                TraceId = context.TraceIdentifier,
                Timestamp = DateTime.UtcNow,
                Path = context.Request.Path,
                Method = context.Request.Method
            };

            switch (exception)
            {
                case NotFoundException notFoundException:
                    response.StatusCode = StatusCodes.Status404NotFound;
                    response.Message = notFoundException.Message;
                    break;

                case ConflictException conflictException:
                    response.StatusCode = StatusCodes.Status409Conflict;
                    response.Message = conflictException.Message;
                    break;

                case UnauthorizedAccessException:
                    response.StatusCode = StatusCodes.Status401Unauthorized;
                    response.Message = "Unauthorized - Invalid or missing API Key";
                    break;

                case InvalidOperationException invalidOpException:
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    response.Message = invalidOpException.Message;
                    break;

                case FluentValidation.ValidationException fluentException:
                    response.StatusCode = StatusCodes.Status400BadRequest;
                    response.Message = "Validation failed";
                    response.Errors = fluentException.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray());
                    break;

                default:
                    response.StatusCode = StatusCodes.Status500InternalServerError;
                    response.Message = "An internal server error occurred";

                    if (_environment.IsDevelopment())
                    {
                        response.Exception = exception.ToString();
                        response.StackTrace = exception.StackTrace;
                    }
                    break;
            }

            context.Response.StatusCode = response.StatusCode;
            context.Response.ContentType = "application/json";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _environment.IsDevelopment()
            };

            var json = JsonSerializer.Serialize(response, options);
            await context.Response.WriteAsync(json);
        }
    }
}
