using System.Collections.Concurrent;
using FluentAssertions;
using PNet.System.Collections.Concurrent;
using Xunit;

namespace PNet.Tests;

public class ConcurrentInternalsTests
{
    [Fact]
    public void ConcurrentDictionary_initialCapacity_is_observable()
    {
        var d = new ConcurrentDictionary<string, int>(concurrencyLevel: 4, capacity: 31);
        // initialCapacity stores what was requested at construction.
        d.p_initialCapacity.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ConcurrentDictionary_growLockArray_default_is_true_for_default_ctor()
    {
        var d = new ConcurrentDictionary<int, int>();
        d.p_growLockArray.Should().BeTrue();
    }
}
