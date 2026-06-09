using System.Text;
using System.Text.RegularExpressions;
using Tun.Contracts.Grpc;

namespace Tun.Server.Tunnels;

public static partial class TunnelPathRewriter
{
    public static bool CanRewrite(HttpResponseStart responseStart)
    {
        if (GetHeader(responseStart, "Content-Encoding") is not null)
        {
            return false;
        }

        var contentType = GetHeader(responseStart, "Content-Type");
        if (contentType is null)
        {
            return false;
        }

        return contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("text/css", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("application/javascript", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("text/javascript", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("application/ecmascript", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("text/ecmascript", StringComparison.OrdinalIgnoreCase);
    }

    public static byte[] Rewrite(HttpResponseStart responseStart, ReadOnlySpan<byte> body, string tunnelId)
    {
        var encoding = GetEncoding(responseStart) ?? Encoding.UTF8;
        var text = encoding.GetString(body);
        var rewritten = RewriteRootRelativeUrls(text, tunnelId);
        return encoding.GetBytes(rewritten);
    }

    public static string RewriteRootRelativeUrls(string content, string tunnelId)
    {
        var publicPrefix = $"/t/{Uri.EscapeDataString(tunnelId)}/";

        var rewritten = RootRelativeAttributeRegex().Replace(
            content,
            match => match.Groups["prefix"].Value + publicPrefix);

        rewritten = CssUrlRegex().Replace(
            rewritten,
            match => match.Groups["prefix"].Value + publicPrefix);

        rewritten = ScriptCallPathRegex().Replace(
            rewritten,
            match => match.Groups["prefix"].Value + publicPrefix);

        rewritten = ScriptStringPathRegex().Replace(
            rewritten,
            match => match.Groups["prefix"].Value + publicPrefix);

        return StaticAssetAttributeRegex().Replace(
            rewritten,
            match => AppendCacheBust(match, tunnelId));
    }

    public static bool IsContentLengthHeader(string name) =>
        string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldSkipRewrittenResponseHeader(string name) =>
        IsContentLengthHeader(name) ||
        string.Equals(name, "ETag", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Last-Modified", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Accept-Ranges", StringComparison.OrdinalIgnoreCase);

    private static Encoding? GetEncoding(HttpResponseStart responseStart)
    {
        var contentType = GetHeader(responseStart, "Content-Type");
        if (contentType is null)
        {
            return null;
        }

        var charsetPrefix = "charset=";
        var charsetIndex = contentType.IndexOf(charsetPrefix, StringComparison.OrdinalIgnoreCase);
        if (charsetIndex < 0)
        {
            return null;
        }

        var charset = contentType[(charsetIndex + charsetPrefix.Length)..]
            .Split(';', 2)[0]
            .Trim()
            .Trim('"', '\'');

        if (string.IsNullOrWhiteSpace(charset))
        {
            return null;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? GetHeader(HttpResponseStart responseStart, string name) =>
        responseStart.Headers
            .FirstOrDefault(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            ?.Values
            .FirstOrDefault();

    private static string AppendCacheBust(Match match, string tunnelId)
    {
        var query = match.Groups["query"].Value;
        if (query.Contains("tun=", StringComparison.OrdinalIgnoreCase))
        {
            return match.Value;
        }

        var separator = string.IsNullOrEmpty(query)
            ? "?"
            : query.EndsWith('?') || query.EndsWith('&') ? string.Empty : "&";

        return match.Groups["prefix"].Value +
               match.Groups["url"].Value +
               query +
               separator +
               "tun=" +
               Uri.EscapeDataString(tunnelId) +
               match.Groups["suffix"].Value;
    }

    [GeneratedRegex("(?<prefix>\\b(?:href|src|action|poster|data-src|data-href)\\s*=\\s*[\"'])/(?!/|t/)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RootRelativeAttributeRegex();

    [GeneratedRegex("(?<prefix>url\\(\\s*[\"']?)/(?!/|t/)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CssUrlRegex();

    [GeneratedRegex("(?<prefix>\\b(?:fetch|import)\\s*\\(\\s*[\"'`]|\\b(?:from|import)\\s*[\"'`])/(?!/|t/)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptCallPathRegex();

    [GeneratedRegex("(?<prefix>[\"'`])/(?!(?:/|t/|[A-Za-z][A-Za-z0-9+.-]*:))(?=(?:api|app|assets|admin|auth|static|images|img|css|js|fonts|favicon)\\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptStringPathRegex();

    [GeneratedRegex("(?<prefix>\\b(?:href|src)\\s*=\\s*[\"'])(?<url>/t/[^\"'?#]+\\.(?:js|css))(?<query>\\?[^\"']*)?(?<suffix>[\"'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StaticAssetAttributeRegex();
}
