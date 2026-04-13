using FluentAssertions;
using PNet.System.Threading;
using Xunit;

namespace PNet.Tests;

public class ThreadingInternalsTests
{
    [Fact]
    public void SemaphoreSlim_currentCount_matches_public_property()
    {
        using var sem = new SemaphoreSlim(initialCount: 3, maxCount: 5);
        sem.p_m_currentCount.Should().Be(sem.CurrentCount);
        sem.p_m_currentCount.Should().Be(3);
        sem.p_m_maxCount.Should().Be(5);
    }

    [Fact]
    public async Task SemaphoreSlim_currentCount_decreases_after_wait()
    {
        using var sem = new SemaphoreSlim(initialCount: 2, maxCount: 2);
        await sem.WaitAsync();
        sem.p_m_currentCount.Should().Be(1);
        await sem.WaitAsync();
        sem.p_m_currentCount.Should().Be(0);
    }

    [Fact]
    public void CancellationTokenSource_disposed_flag_flips()
    {
        var cts = new CancellationTokenSource();
        cts.p_disposed.Should().BeFalse();
        cts.Dispose();
        cts.p_disposed.Should().BeTrue();
    }
}
