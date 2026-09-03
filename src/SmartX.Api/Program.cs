using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using SmartX.Api.Configuration;
using SmartX.Api.Filters;
using SmartX.Infrastructure;
using SmartX.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        ApiRequestLimits.MaximumRequestBodySizeBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        ApiRequestLimits.MaximumRequestBodySizeBytes;
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        ApiProblemDetailsDefaults.Apply(
            context.ProblemDetails,
            context.HttpContext);
});

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ProblemDetailsEnrichmentFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(
                new ValidationProblemDetails(context.ModelState)
                {
                    Title = "Request validation failed.",
                    Status = StatusCodes.Status400BadRequest
                });
    });
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(
    builder.Configuration,
    builder.Environment.ContentRootPath);

builder.Services.AddCors(options =>
{
    options.AddPolicy("SmartXClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = ApiExceptionStatusCodeSelector.Select
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await app.Services.InitialiseSmartXDatabaseAsync();
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("SmartXClient");

app.MapControllers();

app.Run();
