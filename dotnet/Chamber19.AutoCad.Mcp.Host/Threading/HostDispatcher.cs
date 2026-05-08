using System;
using System.Threading.Tasks;

namespace Chamber19.AutoCad.Mcp.Threading;

/// <summary>
/// Bridges the host assembly's tools to the AutoCAD application thread.
/// The shell sets this up by passing a delegate wrapping
/// <c>AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync</c> via the
/// <c>StartHost</c> reflection contract.
/// </summary>
/// <remarks>
/// All values crossing the ALC boundary via the stored delegate are boxed to
/// <c>object?</c> to avoid generic ALC boundary issues. The host's generic
/// <see cref="InvokeOnApplicationThreadAsync{T}"/> wrapper unboxes on the host side.
/// </remarks>
internal static class HostDispatcher
{
    private static Func<Func<object?>, Task<object?>>? _dispatcher;

    /// <summary>
    /// Initializes the dispatcher. Called once by <c>McpHostEntry.StartHost</c>
    /// with the delegate provided by the shell.
    /// </summary>
    internal static void Initialize(Func<Func<object?>, Task<object?>> dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the AutoCAD application thread.
    /// </summary>
    internal static Task InvokeOnApplicationThreadAsync(Action action)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("HostDispatcher is not initialized.");
        }
        return _dispatcher(() => { action(); return null; });
    }

    /// <summary>
    /// Runs <paramref name="func"/> on the AutoCAD application thread and returns its result.
    /// </summary>
    internal static async Task<T> InvokeOnApplicationThreadAsync<T>(Func<T> func)
    {
        if (_dispatcher is null)
        {
            throw new InvalidOperationException("HostDispatcher is not initialized.");
        }
        var result = await _dispatcher(() => (object?)func());
        return (T)result!;
    }

    /// <summary>Test-only: reset dispatcher state so tests can run in isolation.</summary>
    internal static void ResetForTest()
    {
        _dispatcher = null;
    }

    /// <summary>Test-only: install a synchronous inline dispatcher so tests that exercise
    /// tool dispatch logic don't need a running AutoCAD idle loop.</summary>
    internal static void InitializeInlineForTest()
    {
        _dispatcher = func =>
        {
            try
            {
                var result = func();
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromException<object?>(ex);
            }
        };
    }
}
