using System.Text;
using FluentAssertions;
using PNet.System.Text;
using Xunit;

namespace PNet.Tests;

public class TextInternalsTests
{
    [Fact]
    public void StringBuilder_chunk_offsets_track_appends()
    {
        var sb = new StringBuilder("abc");
        sb.p_m_ChunkLength.Should().Be(3);
        sb.p_m_ChunkOffset.Should().Be(0);

        sb.Append('d');
        sb.p_m_ChunkLength.Should().Be(4);
    }

    [Fact]
    public void StringBuilder_max_capacity_is_observable()
    {
        var sb = new StringBuilder(capacity: 16, maxCapacity: 1024);
        sb.p_m_MaxCapacity.Should().Be(1024);
    }

    [Fact]
    public void Encoding_codePage_matches_public_property()
    {
        Encoding.UTF8.p_codePage.Should().Be(Encoding.UTF8.CodePage);
        Encoding.ASCII.p_codePage.Should().Be(Encoding.ASCII.CodePage);
    }

    [Fact]
    public void Encoding_isReadOnly_true_for_built_in_encodings()
    {
        Encoding.UTF8.p_isReadOnly.Should().BeTrue();
    }
}
