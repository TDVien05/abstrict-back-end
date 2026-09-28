namespace Abstrict.Api.Common;

public sealed class ApiFlowException(
    int statusCode,
    string code,
    string message,
    int? retryAfterSeconds = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
