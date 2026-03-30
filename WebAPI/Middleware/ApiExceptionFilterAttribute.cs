using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NLog;

namespace WebAPI.Middleware
{
    public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public override void OnException(ExceptionContext context)
        {
            Logger.Error(context.Exception, "Unhandled exception in controller: {Message}",
                context.Exception.Message);

            var problemDetails = new ProblemDetails
            {
                Title = "An error occurred",
                Status = StatusCodes.Status500InternalServerError,
                Detail = context.Exception.Message,
                Instance = context.HttpContext.Request.Path,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            };

            context.Result = new ObjectResult(problemDetails)
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };

            context.ExceptionHandled = true;
        }
    }
}
