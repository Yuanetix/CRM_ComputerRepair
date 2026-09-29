using System.Net;
using System.Text.Json;

namespace CRM_ComputerRepair.api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict");
            await WriteErrorAsync(context, HttpStatusCode.Conflict,
                "The record has been modified by another user. Please reload and try again.");
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database update violation");
            var detail = ex.InnerException?.Message ?? ex.Message;
            string msg = detail.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || detail.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                ? "A duplicate record with this unique code or identifier already exists."
                : detail.Contains("REFERENCE", StringComparison.OrdinalIgnoreCase) || detail.Contains("foreign key", StringComparison.OrdinalIgnoreCase)
                    ? "The requested operation violates database referential integrity constraints."
                    : "A database constraint violation occurred. Please check your inputs.";
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, msg);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation");
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument");
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again.");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context, HttpStatusCode status, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var payload = JsonSerializer.Serialize(new
        {
            status = (int)status,
            error = message
        });

        await context.Response.WriteAsync(payload);
    }
}