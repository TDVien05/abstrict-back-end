using System.Security.Claims;
using Abstrict.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Controllers;

public abstract class KycControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new KycFlowException(StatusCodes.Status401Unauthorized, "INVALID_TOKEN", "Phiên đăng nhập không hợp lệ.");

    protected async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KycFlowException exception)
        {
            return ToProblem(exception);
        }
    }

    protected ObjectResult ToProblem(KycFlowException exception)
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

    protected IActionResult ImageFile(Abstrict.Api.Services.Interfaces.KycDocumentContent content)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content.Stream, content.ContentType);
    }
}
