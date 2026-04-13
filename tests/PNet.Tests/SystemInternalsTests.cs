using FluentAssertions;
using PNet.System;
using Xunit;

namespace PNet.Tests;

public class SystemInternalsTests
{
    [Fact]
    public void Version_components_match_public_properties()
    {
        var v = new Version(5, 4, 3, 2);
        v.p_Major.Should().Be(5);
        v.p_Minor.Should().Be(4);
        v.p_Build.Should().Be(3);
        v.p_Revision.Should().Be(2);
    }

    [Fact]
    public void UriBuilder_components_round_trip_via_private_fields()
    {
        var b = new UriBuilder("https://user:pw@host.example.com:8443/api/v1?x=1#frag");

        b.p_scheme.Should().Be("https");
        b.p_username.Should().Be("user");
        b.p_password.Should().Be("pw");
        b.p_host.Should().Be("host.example.com");
        b.p_port.Should().Be(8443);
        b.p_path.Should().Be("/api/v1");
    }

    [Fact]
    public void UriBuilder_mutating_private_port_changes_uri()
    {
        var b = new UriBuilder("http://example.com/");
        b.p_port = 9090;
        b.Uri.Port.Should().Be(9090);
    }
}
