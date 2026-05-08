using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Chamber19.AutoCad.Mcp.Diagnostics;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Chamber19.AutoCad.Mcp.Threading;

/// <summary>
/// Marshals work onto the AutoCAD application (UI) thread via the <see cref="Application.Idle"/> event.
/// </summary>
/// <remarks>
/// AutoCAD's managed API is not thread-safe — most database/document operations must execute on the
/// thread that ran <c>IExtensionApplication.Initialize</c>. MCP tool requests arrive on Kestrel
/// background threads, so any tool that touches AutoCAD state must dispatch through this class.
///
/// Pattern lifted from Suite's <c>SuiteCadPipeHost.InvokeOnApplicationThread</c>. The application
/// thread id is captured during <see cref="Initialize"/>; subsequent
/// <see cref="InvokeOnApplicationThreadAsync{T}(Func{T})"/> calls from other threads enqueue a callback
/// and return a <see cref="Task{T}"/> that completes when the next <see cref="Application.Idle"/>
/// tick drains the queue and runs the work on the application thread.
/// </remarks>
internal static class AutoCadThreadDispatcher
{
    private static readonly object SyncRoot = new();
    private static readonly Queue<PendingWork> Pending = new();
    private static int _applicationThreadId;
    private static bool _idleHandlerAttached;
    private static bool _initialized;

    /// <summary>
    /// Captures the calling thread as the AutoCAD application thread and attaches the idle handler.
    /// Must be called from <c>IExtensionApplication.Initialize()</c>, after the MCP host has started.
    /// </summary>
    public static void Initialize()
    {
        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }
            _applicationThreadId = Environment.CurrentManagedThreadId;
            _initialized = true;
        }

        Application.Idle += OnApplicationIdle;
        lock (SyncRoot)
        {
            _idleHandlerAttached = true;
        }
        Log.Write($"[dispatcher] initialized on application thread (tid={_applicationThreadId}); idle handler attached.");
    }

    /// <summary>
    /// Detaches the idle handler and faults any pending callbacks with <see cref="OperationCanceledException"/>.
    /// Must be called from <c>IExtensionApplication.Terminate()</c>, before the MCP host is stopped.
    /// </summary>
    public static void Shutdown()
    {
        bool wasAttached;
        lock (SyncRoot)
        {
            wasAttached = _idleHandlerAttached;
            _idleHandlerAttached = false;
        }
        if (wasAttached)
        {
            Application.Idle -= OnApplicationIdle;
        }

        PendingWork[] toCancel;
        lock (SyncRoot)
        {
            toCancel = Pending.ToArray();
            Pending.Clear();
            _initialized = false;
            _applicationThreadId = 0;
        }

        var cancellation = new OperationCanceledException("AutoCadThreadDispatcher is shutting down.");
        foreach (var work in toCancel)
        {
            work.Cancel(cancellation);
        }
        Log.Write($"[dispatcher] shut down; idle handler detached; cancelled {toCancel.Length} pending action(s).");
    }

    /// <summary>
    /// True if the calling thread is the captured AutoCAD application thread.
    /// </summary>
    public static bool IsOnApplicationThread =>
        _applicationThreadId != 0 && Environment.CurrentManagedThreadId == _applicationThreadId;

    /// <summary>
    /// Runs <paramref name="action"/> on the AutoCAD application thread.
    /// Returns a <see cref="Task"/> that completes when the action runs (or faults if it throws).
    /// </summary>
    public static Task InvokeOnApplicationThreadAsync(Action action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new PendingWork(
            () =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            },
            ex => tcs.TrySetException(ex)));
        return tcs.Task;
    }

    /// <summary>
    /// Runs <paramref name="func"/> on the AutoCAD application thread and returns its result.
    /// </summary>
    public static Task<T> InvokeOnApplicationThreadAsync<T>(Func<T> func)
    {
        if (func is null)
        {
            throw new ArgumentNullException(nameof(func));
        }

        if (IsOnApplicationThread)
        {
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(new PendingWork(
            () =>
            {
                try
                {
                    tcs.TrySetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            },
            ex => tcs.TrySetException(ex)));
        return tcs.Task;
    }

    private static void Enqueue(PendingWork work)
    {
        int depth;
        lock (SyncRoot)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "AutoCadThreadDispatcher is not initialized; ensure Extension.Initialize ran on the AutoCAD application thread.");
            }
            Pending.Enqueue(work);
            depth = Pending.Count;
        }
        Log.Write($"[dispatcher] enqueued; queue depth={depth}");
    }

    private static void OnApplicationIdle(object? sender, EventArgs args)
    {
        PendingWork? next = null;
        int remaining = 0;
        lock (SyncRoot)
        {
            if (Pending.Count > 0)
            {
                next = Pending.Dequeue();
                remaining = Pending.Count;
            }
        }

        if (next is null)
        {
            return;
        }

        Log.Write($"[dispatcher] dequeued on idle (tid={Environment.CurrentManagedThreadId}); remaining={remaining}");
        next.Run();
    }

    private sealed record PendingWork(Action Run, Action<Exception> Cancel);
}
