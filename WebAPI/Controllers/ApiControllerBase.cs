using Azure.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
        private ISender? _mediator;
        private ILogger? _logger;

        protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

        protected ILogger Logger => _logger ??= HttpContext.RequestServices.GetRequiredService<ILogger<ApiControllerBase>>();

        protected ActionResult<TResult> OkOrNotFound<TResult>(TResult? result)
        {
            if (result == null)
            {
                Logger.LogWarning("Resource not found: {Path}", Request.Path);
                return NotFound();
            }

            return Ok(result);
        }

        protected Guid GetCurrentUserId()
        {
            // For future implementation if needed
            return Guid.Empty;
        }
    }
}
