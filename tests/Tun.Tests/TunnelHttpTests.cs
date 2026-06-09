using Tun.Contracts.Http;

namespace Tun.Tests;

public sealed class TunnelHttpTests
{
    [Fact]
    public void BuildTargetUri_PreservesBasePathAndQueryString()
    {
        var uri = TunnelHttp.BuildTargetUri(new Uri("http://localhost:5000/base/"), "/api", "?a=1");

        Assert.Equal("http://localhost:5000/base/api?a=1", uri.ToString());
    }

    [Fact]
    public void ToTunnelHeaders_FiltersHopByHopHeaders()
    {
        var headers = new[]
        {
            new KeyValuePair<string, IEnumerable<string>>("Connection", ["keep-alive"]),
            new KeyValuePair<string, IEnumerable<string>>("Transfer-Encoding", ["chunked"]),
            new KeyValuePair<string, IEnumerable<string>>("X-Test", ["ok"])
        };

        var forwarded = TunnelHttp
            .ToTunnelHeaders(headers, TunnelHttp.ShouldForwardResponseHeader)
            .ToArray();

        var header = Assert.Single(forwarded);
        Assert.Equal("X-Test", header.Name);
        Assert.Equal("ok", Assert.Single(header.Values));
    }

    [Fact]
    public void ToTunnelHeaders_FiltersHostForRequests()
    {
        var headers = new[]
        {
            new KeyValuePair<string, IEnumerable<string>>("Host", ["public.example"]),
            new KeyValuePair<string, IEnumerable<string>>("X-Test", ["ok"])
        };

        var forwarded = TunnelHttp
            .ToTunnelHeaders(headers, TunnelHttp.ShouldForwardRequestHeader)
            .ToArray();

        var header = Assert.Single(forwarded);
        Assert.Equal("X-Test", header.Name);
    }

    [Fact]
    public void ToTunnelHeaders_FiltersConditionalCacheHeadersForRequests()
    {
        var headers = new[]
        {
            new KeyValuePair<string, IEnumerable<string>>("If-None-Match", ["\"abc\""]),
            new KeyValuePair<string, IEnumerable<string>>("If-Modified-Since", ["Tue, 09 Jun 2026 01:00:00 GMT"]),
            new KeyValuePair<string, IEnumerable<string>>("If-Range", ["\"range\""]),
            new KeyValuePair<string, IEnumerable<string>>("X-Test", ["ok"])
        };

        var forwarded = TunnelHttp
            .ToTunnelHeaders(headers, TunnelHttp.ShouldForwardRequestHeader)
            .ToArray();

        var header = Assert.Single(forwarded);
        Assert.Equal("X-Test", header.Name);
    }
}
