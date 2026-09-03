using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartX.Api.Configuration;

namespace SmartX.Api.Filters;

public sealed class ProblemDetailsEnrichmentFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult
            {
                Value: ProblemDetails problemDetails
            })
        {
            ApiProblemDetailsDefaults.Apply(
                problemDetails,
                context.HttpContext);
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
        _ = context;
    }
}
