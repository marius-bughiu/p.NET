using System.Diagnostics;
using FluentAssertions;
using PNet.System.Diagnostics;
using Xunit;

namespace PNet.Tests;

public class DiagnosticsInternalsTests
{
    [Fact]
    public void Stopwatch_isRunning_matches_public_state()
    {
        var sw = new Stopwatch();
        sw.p_isRunning.Should().BeFalse();
        sw.Start();
        sw.p_isRunning.Should().BeTrue();
        sw.Stop();
        sw.p_isRunning.Should().BeFalse();
    }

    [Fact]
    public void Stopwatch_elapsed_advances_after_run()
    {
        var sw = Stopwatch.StartNew();
        Thread.Sleep(5);
        sw.Stop();
        sw.p_elapsed.Should().BeGreaterThan(0);
        sw.p_startTimeStamp.Should().BeGreaterThan(0);
    }
}
