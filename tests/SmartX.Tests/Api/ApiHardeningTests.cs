using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Configuration;
using SmartX.Api.Controllers;

namespace SmartX.Tests.Api;

public sealed class ApiHardeningTests
{
    [Fact]
    public void ExceptionSelector_PreservesBadRequestStatusCode()
    {
        var exception = new BadHttpRequestException(
            "Request body too large.",
            StatusCodes.Status413PayloadTooLarge);

        var statusCode = ApiExceptionStatusCodeSelector.Select(exception);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusCode);
    }

    [Fact]
    public void ExceptionSelector_UsesInternalServerErrorForUnexpectedException()
    {
        var statusCode = ApiExceptionStatusCodeSelector.Select(
            new InvalidOperationException("Unexpected failure."));

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            statusCode);
    }

    [Fact]
    public void Apply_AddsRequestPathAndTraceIdentifier()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "smartx-test-trace"
        };
        httpContext.Request.Path = "/api/telemetry/bulk";
        var problemDetails = new ProblemDetails();

        ApiProblemDetailsDefaults.Apply(problemDetails, httpContext);

        Assert.Equal(
            "/api/telemetry/bulk",
            problemDetails.Instance);
        Assert.Equal(
            "smartx-test-trace",
            problemDetails.Extensions[
                ApiProblemDetailsDefaults.TraceIdExtensionName]);
    }

    [Fact]
    public void Apply_PreservesExistingProblemDetailsValues()
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "new-trace"
        };
        httpContext.Request.Path = "/new-path";
        var problemDetails = new ProblemDetails
        {
            Instance = "/existing-path"
        };
        problemDetails.Extensions.Add(
            ApiProblemDetailsDefaults.TraceIdExtensionName,
            "existing-trace");

        ApiProblemDetailsDefaults.Apply(problemDetails, httpContext);

        Assert.Equal("/existing-path", problemDetails.Instance);
        Assert.Equal(
            "existing-trace",
            problemDetails.Extensions[
                ApiProblemDetailsDefaults.TraceIdExtensionName]);
    }

    [Fact]
    public void RequestLimit_AllowsAttachmentMultipartOverhead()
    {
        Assert.True(
            ApiRequestLimits.MaximumRequestBodySizeBytes >
            SensorAttachmentsController.MaximumFileSizeBytes);
    }
}
