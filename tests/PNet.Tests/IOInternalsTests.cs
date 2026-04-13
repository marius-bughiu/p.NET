using FluentAssertions;
using PNet.System.IO;
using Xunit;

namespace PNet.Tests;

public class IOInternalsTests
{
    [Fact]
    public void MemoryStream_exposes_capacity_and_length()
    {
        using var ms = new MemoryStream(capacity: 64);
        ms.WriteByte(0xAB);
        ms.WriteByte(0xCD);

        ms.p_capacity.Should().BeGreaterThanOrEqualTo(64);
        ms.p_length.Should().Be((int)ms.Length);
        ms.p_position.Should().Be((int)ms.Position);
    }

    [Fact]
    public void MemoryStream_writable_flag_matches_public_property()
    {
        using var ms = new MemoryStream();
        ms.p_writable.Should().Be(ms.CanWrite);
    }

    [Fact]
    public void MemoryStream_buffer_byref_lets_us_observe_bytes()
    {
        using var ms = new MemoryStream(8);
        ms.WriteByte(1);
        ms.WriteByte(2);
        ms.WriteByte(3);

        ref var buf = ref ms.p_buffer;
        buf.Length.Should().BeGreaterThanOrEqualTo(3);
        buf[0].Should().Be(1);
        buf[1].Should().Be(2);
        buf[2].Should().Be(3);
    }

    [Fact]
    public void StringReader_pos_advances_with_reads()
    {
        var sr = new StringReader("abcdef");
        sr.p_pos.Should().Be(0);
        sr.Read();
        sr.p_pos.Should().Be(1);
        sr.Read();
        sr.Read();
        sr.p_pos.Should().Be(3);
    }

    [Fact]
    public void StringReader_underlying_string_is_visible()
    {
        var sr = new StringReader("hello");
        sr.p_s.Should().Be("hello");
    }

    [Fact]
    public void StringWriter_isOpen_starts_true_then_closes()
    {
        var sw = new StringWriter();
        sw.p_isOpen.Should().BeTrue();
        sw.Close();
        sw.p_isOpen.Should().BeFalse();
    }
}
