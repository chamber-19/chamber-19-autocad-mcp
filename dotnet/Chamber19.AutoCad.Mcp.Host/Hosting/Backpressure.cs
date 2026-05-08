using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Chamber19.AutoCad.Mcp.Hosting;

/// <summary>
/// HTTP middleware that returns HTTP 429 when the AutoCAD application-thread dispatcher's
/// queue is at or above capacity. Prevents requests from piling up indefinitely behind a
/// busy AutoCAD; clients see a structured "retry shortly" response instead.
/// </summary>
/// <remarks>
/// The depth and capacity are passed as <see cref="Func{Int32}"/> getters so the middleware
/// is testable without depending on the static <c>AutoCadThreadDispatcher</c> state.
/// In production, wire them to <c>AutoCadThreadDispatcher.QueueDepth</c> and
/// <c>AutoCadThreadDispatcher.QueueCapacity</c>.
///
/// This is a soft hint: the dispatcher itself enforces the same cap and will throw on
/// over-enqueue, so this middleware just gives clients a clean 429 instead of letting the
/// MCP request fail mid-tool. Best-effort under bursty load (race between the check and the
/// enqueue inside the tool handler).
/// </remarks>
internal static class Backpressure
{
    public static IApplicationBuilder UseBackpressure(
        this IApplicationBuilder app,
        Func<int> getDepth,
        Func<int> getCapacity)
    {
        if (getDepth is null) throw new ArgumentNullException(nameof(getDepth));
        if (getCapacity is null) throw new ArgumentNullException(nameof(getCapacity));

        return app.Use(async (context, next) =>
        {
            var depth = getDepth();
            var capacity = getCapacity();
            if (capacity > 0 && depth >= capacity)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = "1";
                context.Response.ContentType = "application/json; charset=utf-8";
                var body = JsonSerializer.Serialize(new
                {
                    error = "queue_full",
                    error_description = $"AutoCAD application thread queue is full ({depth}/{capacity}). Retry shortly.",
                    queueDepth = depth,
                    queueCapacity = capacity,
                });
                await context.Response.WriteAsync(body);
                return;
            }
            await next();
        });
    }
}
