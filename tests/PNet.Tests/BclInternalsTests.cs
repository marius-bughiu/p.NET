using FluentAssertions;
using PNet.System.Collections.Generic;
using Xunit;

namespace PNet.Tests;

public class BclInternalsTests
{
    [Fact]
    public void List_p_size_reads_private_size()
    {
        var list = new List<int> { 1, 2, 3 };
        list.p_size.Should().Be(3);
    }

    [Fact]
    public void List_p_size_can_shrink_list_by_ref_assignment()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        list.p_size = 2;
        list.Count.Should().Be(2);
        list.Should().Equal(1, 2);
    }

    [Fact]
    public void List_p_version_increments_after_mutation()
    {
        var list = new List<int> { 1 };
        var before = list.p_version;
        list.Add(2);
        list.p_version.Should().BeGreaterThan(before);
    }

    [Fact]
    public void Dictionary_private_count_matches_public_count()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        dict.p_count.Should().Be(dict.Count);
    }

    [Fact]
    public void Dictionary_p_buckets_length_at_least_capacity()
    {
        var dict = new Dictionary<string, int>(capacity: 16) { ["a"] = 1 };
        dict.p_buckets.Length.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void HashSet_private_count_matches_public_count()
    {
        var set = new HashSet<int> { 1, 2, 3, 4 };
        set.p_count.Should().Be(set.Count);
    }
}
