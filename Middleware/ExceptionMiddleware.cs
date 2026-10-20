using System.Text.Json;
using CampGearBackend.Models;

namespace CampGearBackend.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
            catch (AppException e)
            {
                await WriteAsync(context, e.StatusCode, "Request Failed", e.Message);
            }
            catch (JsonException e)
            {
                await WriteAsync(context, 400, "Bad Request", e.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
                await WriteAsync(context, 500, "Internal Server Error", "An unexpected error occurred");
            }
        }

        private static async Task WriteAsync(HttpContext context, int statusCode, string title, string detail)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                title,
                status = statusCode,
                detail,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }
}
