using MeetingMind.Application.Actions;

namespace MeetingMind.Api;

public sealed class ActionExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    public ActionExceptionHandlerMiddleware(RequestDelegate next) => _next = next;
    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (ActionValidationException ex) { await Write(context, 400, "action_validation_failed", ex.Message); }
        catch (ActionNotFoundException) { await Write(context, 404, "action_not_found", "The action was not found."); }
        catch (ActionConflictException) { await Write(context, 409, "action_version_conflict", "The action was changed by another request. Reload the latest version."); }
    }
    private static async Task Write(HttpContext context, int status, string code, string message) { context.Response.StatusCode = status; await context.Response.WriteAsJsonAsync(new { code, message }); }
}
