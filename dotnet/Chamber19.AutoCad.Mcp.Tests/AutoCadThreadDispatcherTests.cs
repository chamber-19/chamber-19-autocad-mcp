using System;
using Chamber19.AutoCad.Mcp.Threading;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Queue-cap behavior tests for <see cref="AutoCadThreadDispatcher"/> using its
/// test-only surface (<c>InitializeForTest</c>, <c>SetQueueCapacityForTest</c>,
/// <c>EnqueueForTest</c>, <c>ResetForTest</c>). These bypass <see cref="AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync(Action)"/>'s
/// IsOnApplicationThread fast path so capacity can be exercised deterministically from
/// the xUnit thread without queueing real callbacks that need <see cref="System.Reactive"/>-style scheduling.
/// </summary>
public sealed class AutoCadThreadDispatcherTests : IDisposable
{
    public AutoCadThreadDispatcherTests()
    {
        AutoCadThreadDispatcher.ResetForTest();
        AutoCadThreadDispatcher.InitializeForTest();
    }

    public void Dispose() => AutoCadThreadDispatcher.ResetForTest();

    [Fact]
    public void DefaultQueueCapacity_Is32()
    {
        // Re-initialize from a clean state (the constructor's InitializeForTest already ran).
        Assert.Equal(32, AutoCadThreadDispatcher.DefaultQueueCapacity);
        Assert.Equal(32, AutoCadThreadDispatcher.QueueCapacity);
    }

    [Fact]
    public void QueueDepth_StartsAtZero()
    {
        Assert.Equal(0, AutoCadThreadDispatcher.QueueDepth);
    }

    [Fact]
    public void Enqueue_BelowCapacity_IncrementsDepth()
    {
        AutoCadThreadDispatcher.SetQueueCapacityForTest(3);

        AutoCadThreadDispatcher.EnqueueForTest(() => { });
        Assert.Equal(1, AutoCadThreadDispatcher.QueueDepth);

        AutoCadThreadDispatcher.EnqueueForTest(() => { });
        Assert.Equal(2, AutoCadThreadDispatcher.QueueDepth);
    }

    [Fact]
    public void Enqueue_AtCapacity_Throws_AndDepthDoesNotGrow()
    {
        AutoCadThreadDispatcher.SetQueueCapacityForTest(2);
        AutoCadThreadDispatcher.EnqueueForTest(() => { });
        AutoCadThreadDispatcher.EnqueueForTest(() => { });
        Assert.Equal(2, AutoCadThreadDispatcher.QueueDepth);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AutoCadThreadDispatcher.EnqueueForTest(() => { }));

        Assert.Contains("queue is full", ex.Message);
        Assert.Contains("2/2", ex.Message);
        Assert.Equal(2, AutoCadThreadDispatcher.QueueDepth);
    }

    [Fact]
    public void Enqueue_WhenNotInitialized_Throws()
    {
        AutoCadThreadDispatcher.ResetForTest();
        // Note: ResetForTest leaves dispatcher uninitialized.

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AutoCadThreadDispatcher.EnqueueForTest(() => { }));

        Assert.Contains("not initialized", ex.Message);
    }

    [Fact]
    public void SetQueueCapacityForTest_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AutoCadThreadDispatcher.SetQueueCapacityForTest(-1));
    }
}
