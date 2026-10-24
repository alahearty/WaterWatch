using Microsoft.EntityFrameworkCore;

namespace WaterWatch.Middleware;

public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (context.Request.Path.StartsWithSegments("/api"))
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var (status, error) = ex switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
                InvalidOperationException => (StatusCodes.Status400BadRequest, "Invalid operation"),
                DbUpdateException => (StatusCodes.Status409Conflict, "Database constraint conflict"),
                _ => (StatusCodes.Status500InternalServerError, "Server error")
            };

            if (status >= 500)
            {
                _logger.LogError(ex, "Unhandled API exception for {Path}.", context.Request.Path);
            }
            else
            {
                _logger.LogWarning(ex, "API request rejected ({Status}) for {Path}.", status, context.Request.Path);
            }

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error,
                message = ex.Message,
                status
            });
        }
    }
}
