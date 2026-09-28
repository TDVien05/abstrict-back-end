using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace Abstrict.Api.Common;

public static class FlowProblemExtensions
{
    public static ObjectResult ToFlowProblem(this ControllerBase controller, ApiFlowException exception)
    {
        if (exception.RetryAfterSeconds is { } retryAfter)
            controller.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = exception.StatusCode,
            Title = exception.Code,
            Detail = exception.Message
        };
        problem.Extensions["code"] = exception.Code;
        problem.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;
        return controller.StatusCode(exception.StatusCode, problem);
    }
}
