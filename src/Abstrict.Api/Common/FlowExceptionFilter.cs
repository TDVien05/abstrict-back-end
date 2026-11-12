using Abstrict.Api.Services.Implementations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Abstrict.Api.Common;

/// <summary>Đổi lỗi nghiệp vụ (<see cref="BookingFlowException"/>, <see cref="KycFlowException"/>) thành ProblemDetails cho mọi controller.</summary>
public sealed class FlowExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var (statusCode, code) = context.Exception switch
        {
            BookingFlowException exception => (exception.StatusCode, exception.Code),
            KycFlowException exception => (exception.StatusCode, exception.Code),
            _ => (0, null)
        };
        if (code is null)
            return;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = code,
            Detail = context.Exception.Message
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        context.Result = new ObjectResult(problem) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }
}
