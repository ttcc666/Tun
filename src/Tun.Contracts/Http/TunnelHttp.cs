using System.Collections.Frozen;
using Tun.Contracts.Grpc;

namespace Tun.Contracts.Http;

public static class TunnelHttp
{
    public const int DefaultChunkSize = 64 * 1024;

    public static readonly string[] CommonHttpMethods =
    [
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
        "HEAD",
        "OPTIONS"
    ];

    private static readonly FrozenSet<string> HopByHopHeaders = new[]
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsHopByHopHeader(string name) => HopByHopHeaders.Contains(name);

    public static bool ShouldForwardRequestHeader(string name) =>
        !IsHopByHopHeader(name) &&
        !string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(name, "If-None-Match", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(name, "If-Modified-Since", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(name, "If-Range", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldForwardResponseHeader(string name) => !IsHopByHopHeader(name);

    public static bool TryExtractTunnelPath(string requestPath, out string tunnelId, out string localPath)
    {
        tunnelId = string.Empty;
        localPath = "/";

        var path = requestPath.AsSpan();
        if (!path.StartsWith("/t/".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = path[3..];
        if (rest.IsEmpty)
        {
            return false;
        }

        var nextSlash = rest.IndexOf('/');
        if (nextSlash < 0)
        {
            tunnelId = rest.ToString();
            return !string.IsNullOrWhiteSpace(tunnelId);
        }

        tunnelId = rest[..nextSlash].ToString();
        var remaining = rest[nextSlash..].ToString();
        localPath = string.IsNullOrEmpty(remaining) ? "/" : remaining;

        return !string.IsNullOrWhiteSpace(tunnelId);
    }

    public static IEnumerable<Header> ToTunnelHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers,
        Func<string, bool> shouldForward)
    {
        foreach (var (name, values) in headers)
        {
            if (!shouldForward(name))
            {
                continue;
            }

            var header = new Header { Name = name };
            header.Values.AddRange(values);
            yield return header;
        }
    }

    public static Uri BuildTargetUri(Uri localBaseUri, string path, string queryString)
    {
        var builder = new UriBuilder(localBaseUri);
        var basePath = builder.Path.TrimEnd('/');
        var requestPath = string.IsNullOrEmpty(path) ? "/" : path;

        if (!requestPath.StartsWith('/'))
        {
            requestPath = "/" + requestPath;
        }

        builder.Path = string.IsNullOrEmpty(basePath)
            ? requestPath
            : basePath + requestPath;
        builder.Query = queryString.StartsWith('?') ? queryString[1..] : queryString;

        return builder.Uri;
    }
}
