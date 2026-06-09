using Tun.Contracts.Grpc;
using Tun.Contracts.Http;
using Tun.Server.Tunnels;

namespace Tun.Tests;

public sealed class TunnelHttpTests
{
    [Fact]
    public void TryExtractTunnelPath_StripsTunnelPrefix()
    {
        var ok = TunnelHttp.TryExtractTunnelPath("/t/app/api", out var tunnelId, out var localPath);

        Assert.True(ok);
        Assert.Equal("app", tunnelId);
        Assert.Equal("/api", localPath);
    }

    [Fact]
    public void TryExtractTunnelPath_DefaultsToRootForTunnelOnlyPath()
    {
        var ok = TunnelHttp.TryExtractTunnelPath("/t/app", out var tunnelId, out var localPath);

        Assert.True(ok);
        Assert.Equal("app", tunnelId);
        Assert.Equal("/", localPath);
    }

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

    [Fact]
    public void RewriteRootRelativeUrls_AddsTunnelPrefix()
    {
        const string html = """
            <base href="/">
            <link rel="stylesheet" href="/app/assets/index.css">
            <script type="module" src="/app/assets/index.js"></script>
            <a href="/orders">Orders</a>
            <img src="//cdn.example/logo.png">
            <a href="/t/demo/already">Already prefixed</a>
            """;

        var rewritten = TunnelPathRewriter.RewriteRootRelativeUrls(html, "LicenseServer");

        Assert.Contains("href=\"/t/LicenseServer/\"", rewritten);
        Assert.Contains("href=\"/t/LicenseServer/app/assets/index.css?tun=LicenseServer\"", rewritten);
        Assert.Contains("src=\"/t/LicenseServer/app/assets/index.js?tun=LicenseServer\"", rewritten);
        Assert.Contains("href=\"/t/LicenseServer/orders\"", rewritten);
        Assert.Contains("src=\"//cdn.example/logo.png\"", rewritten);
        Assert.Contains("href=\"/t/demo/already\"", rewritten);
    }

    [Fact]
    public void RewriteRootRelativeUrls_RewritesCssUrls()
    {
        const string css = "body{background:url('/images/bg.png')}@font-face{src:url(/fonts/app.woff2)}";

        var rewritten = TunnelPathRewriter.RewriteRootRelativeUrls(css, "demo");

        Assert.Contains("url('/t/demo/images/bg.png')", rewritten);
        Assert.Contains("url(/t/demo/fonts/app.woff2)", rewritten);
    }

    [Fact]
    public void RewriteRootRelativeUrls_RewritesScriptStringUrls()
    {
        const string script = """
            await request(`/api/admin/auth/session`);
            navigate("/app/login");
            fetch('/api/orders');
            import("/assets/chunk.js");
            const external = "https://example.com/api";
            const prefixed = "/t/demo/api";
            """;

        var rewritten = TunnelPathRewriter.RewriteRootRelativeUrls(script, "LicenseServer");

        Assert.Contains("`/t/LicenseServer/api/admin/auth/session`", rewritten);
        Assert.Contains("\"/t/LicenseServer/app/login\"", rewritten);
        Assert.Contains("'/t/LicenseServer/api/orders'", rewritten);
        Assert.Contains("\"/t/LicenseServer/assets/chunk.js\"", rewritten);
        Assert.Contains("\"https://example.com/api\"", rewritten);
        Assert.Contains("\"/t/demo/api\"", rewritten);
    }
}
