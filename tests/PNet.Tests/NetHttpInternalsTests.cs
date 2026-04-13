using System.Net.Http;
using FluentAssertions;
using PNet.System.Net.Http;
using Xunit;

namespace PNet.Tests;

public class NetHttpInternalsTests
{
    [Fact]
    public void HttpClient_disposed_flag_flips_after_dispose()
    {
        var client = new HttpClient();
        client.p_disposed.Should().BeFalse();
        client.Dispose();
        client.p_disposed.Should().BeTrue();
    }

    [Fact]
    public void HttpRequestMessage_method_and_uri_match_public()
    {
        var uri = new Uri("https://example.com/path");
        var msg = new HttpRequestMessage(HttpMethod.Post, uri);
        msg.p_method.Should().BeSameAs(msg.Method);
        msg.p_requestUri.Should().BeSameAs(msg.RequestUri);
        msg.p_method.Should().Be(HttpMethod.Post);
    }
}
