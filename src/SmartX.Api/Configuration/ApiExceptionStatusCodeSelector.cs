using Microsoft.AspNetCore.Http;

namespace SmartX.Api.Configuration;

public static class ApiExceptionStatusCodeSelector
{
    public static int Select(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is BadHttpRequestException badRequestException
            ? badRequestException.StatusCode
            : StatusCodes.Status500InternalServerError;
    }
}
