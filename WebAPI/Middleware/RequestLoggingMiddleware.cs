using NLog;
using System.Diagnostics;
using System.Text;

namespace WebAPI.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public RequestLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Log request
                await LogRequest(context);

                // Capture response body
                var originalBodyStream = context.Response.Body;
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                await _next(context);

                // Log response
                await LogResponse(context, responseBody, stopwatch.ElapsedMilliseconds);

                // Copy response back to original stream
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.Error(ex, "Request {Method} {Path} failed after {Elapsed}ms",
                    context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }

        private async Task LogRequest(HttpContext context)
        {
            context.Request.EnableBuffering();

            var requestBody = await ReadRequestBody(context.Request);
            var headers = context.Request.Headers
                .Where(h => h.Key != "Authorization" && h.Key != "X-API-Key") // Don't log sensitive headers
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            Logger.Debug("Request: {Method} {Path} {QueryString} - Headers: {@Headers} - Body: {Body}",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                headers,
                requestBody.Length > 1000 ? requestBody[..1000] + "..." : requestBody);

            context.Request.Body.Position = 0;
        }

        private async Task LogResponse(HttpContext context, MemoryStream responseBody, long elapsedMs)
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();

            var logLevel = context.Response.StatusCode >= 500 ? NLog.LogLevel.Error :
                          context.Response.StatusCode >= 400 ? NLog.LogLevel.Warn : NLog.LogLevel.Debug;

            Logger.Log(logLevel, "Response: {StatusCode} - Body: {Body} - Elapsed: {Elapsed}ms - Client: {ClientIp}",
                context.Response.StatusCode,
                responseText.Length > 1000 ? responseText[..1000] + "..." : responseText,
                elapsedMs,
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        private async Task<string> ReadRequestBody(HttpRequest request)
        {
            if (request.ContentLength == null || request.ContentLength == 0)
                return string.Empty;

            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
    }
}
