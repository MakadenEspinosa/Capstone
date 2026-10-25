using Microsoft.AspNetCore.Antiforgery;

namespace ESEvents.Web.Middleware;

public sealed class AntiforgeryValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery, ILogger<AntiforgeryValidationMiddleware> logger)
    {
        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method) ||
            HttpMethods.IsTrace(context.Request.Method))
        {
            await next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            await next(context);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogWarning(ex, "Antiforgery validation failed for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "A valid CSRF token is required." });
        }
    }
}