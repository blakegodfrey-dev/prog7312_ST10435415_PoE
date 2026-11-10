using Microsoft.AspNetCore.Mvc;

namespace SmartX.Api.Configuration;

public static class ApiProblemDetailsDefaults
{
    public const string TraceIdExtensionName = "traceId";

    public static void Apply(
        ProblemDetails problemDetails,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(problemDetails);
        ArgumentNullException.ThrowIfNull(httpContext);

        problemDetails.Instance ??=
            httpContext.Request.Path.HasValue
                ? httpContext.Request.Path.Value
                : "/";

        problemDetails.Extensions.TryAdd(
            TraceIdExtensionName,
            httpContext.TraceIdentifier);
    }
}
