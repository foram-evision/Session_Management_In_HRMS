using System.Net;
using System.Text.Json;
using HRMS.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HRMS.API.Middleware;

/// <summary>
/// The Ultimate Global Exception Middleware
/// Catches all unhandled exceptions, logs them securely, applies production guardrails,
/// and returns the project's standardized ApiResponse structure.
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request execution: {Message}", ex.Message);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("The response has already started. Global exception middleware cannot modify the response.");
                throw;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.Clear();
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // Determine environment-specific messages safely
        var message = _env.IsDevelopment()
            ? exception.Message
            : "An unexpected error occurred on the server.";

        var errors = _env.IsDevelopment()
            ? new List<string> { exception.StackTrace ?? string.Empty }
            : new List<string>();

        // Cleanest approach: Utilize your project's built-in Failure factory method
        var response = ApiResponse<object>.Failure(message, errors);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(response, options);

        await context.Response.WriteAsync(json);
    }
}