using System.Security.Claims;
using Abstrict.Api.Services.Implementations;
using Abstrict.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

public abstract class BookingControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new BookingFlowException(StatusCodes.Status401Unauthorized, "INVALID_TOKEN", "Phiên đăng nhập không hợp lệ.");

    protected async Task<ActionResult<T>> Run<T>(Func<Task<T>> action, int successStatusCode = StatusCodes.Status200OK)
    {
        try
        {
            return StatusCode(successStatusCode, await action());
        }
        catch (BookingFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    protected async Task<IActionResult> RunNoContent(Func<Task> action)
    {
        try
        {
            await action();
            return NoContent();
        }
        catch (BookingFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    protected IActionResult ImageFile(BookingPhotoContent content)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content.Stream, content.ContentType);
    }

    protected ObjectResult ToProblem(BookingFlowException exception)
    {
        var problem = new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Code,
            Detail = exception.Message
        };
        problem.Extensions["code"] = exception.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(exception.StatusCode, problem);
    }
}
