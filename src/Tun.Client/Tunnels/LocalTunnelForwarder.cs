using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Threading.Channels;
using Tun.Client.Configuration;
using Tun.Contracts.Grpc;
using Tun.Contracts.Http;

namespace Tun.Client.Tunnels;

public sealed class LocalTunnelForwarder
{
    private const int NotFound = 404;
    private const int Conflict = 409;
    private const int ClientClosedRequest = 499;
    private const int BadGateway = 502;

    private readonly object _localUrlsGate = new();
    private readonly Dictionary<string, Uri> _localUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LocalRequestState> _requests = new(StringComparer.Ordinal);
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalTunnelForwarder> _logger;
    private readonly int _chunkSize;

    public LocalTunnelForwarder(
        IOptions<TunnelClientOptions> options,
        ILogger<LocalTunnelForwarder> logger)
        : this(options, logger, new HttpClient())
    {
    }

    internal LocalTunnelForwarder(
        IOptions<TunnelClientOptions> options,
        ILogger<LocalTunnelForwarder> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _chunkSize = Math.Max(1024, options.Value.ChunkSize);
        SetTunnels(options.Value.Tunnels);
    }

    public void SetTunnels(IEnumerable<TunnelClientRegistration> tunnels)
    {
        var next = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
        foreach (var tunnel in tunnels)
        {
            if (string.IsNullOrWhiteSpace(tunnel.TunnelId) || string.IsNullOrWhiteSpace(tunnel.LocalUrl))
            {
                continue;
            }

            next[tunnel.TunnelId.Trim()] = new Uri(tunnel.LocalUrl.Trim(), UriKind.Absolute);
        }

        lock (_localUrlsGate)
        {
            _localUrls.Clear();
            foreach (var (tunnelId, localUrl) in next)
            {
                _localUrls[tunnelId] = localUrl;
            }
        }
    }

    public async Task HandleFrameAsync(
        TunnelServerFrame frame,
        ChannelWriter<TunnelClientFrame> outbound,
        CancellationToken cancellationToken)
    {
        switch (frame.KindCase)
        {
            case TunnelServerFrame.KindOneofCase.RequestStart:
                HandleRequestStart(frame.RequestId, frame.RequestStart, outbound, cancellationToken);
                break;

            case TunnelServerFrame.KindOneofCase.BodyChunk:
                if (_requests.TryGetValue(frame.RequestId, out var request))
                {
                    await request.WriteBodyAsync(frame.BodyChunk.Data, cancellationToken);
                }
                break;

            case TunnelServerFrame.KindOneofCase.Complete:
                if (_requests.TryGetValue(frame.RequestId, out var completed))
                {
                    completed.CompleteBody();
                }
                break;

            case TunnelServerFrame.KindOneofCase.Error:
                if (_requests.TryRemove(frame.RequestId, out var failed))
                {
                    failed.CompleteBody(new IOException(frame.Error.Message));
                }
                break;
        }
    }

    public void AbortAll(Exception? exception = null)
    {
        foreach (var (requestId, state) in _requests)
        {
            if (_requests.TryRemove(requestId, out _))
            {
                state.CompleteBody(exception);
            }
        }
    }

    private void HandleRequestStart(
        string requestId,
        HttpRequestStart start,
        ChannelWriter<TunnelClientFrame> outbound,
        CancellationToken cancellationToken)
    {
        Uri localUrl;
        lock (_localUrlsGate)
        {
            if (!_localUrls.TryGetValue(start.TunnelId, out localUrl!))
            {
                _ = outbound.WriteAsync(CreateError(requestId, NotFound, $"Unknown tunnel '{start.TunnelId}'."), cancellationToken);
                return;
            }
        }

        if (localUrl is null)
        {
            _ = outbound.WriteAsync(CreateError(requestId, NotFound, $"Unknown tunnel '{start.TunnelId}'."), cancellationToken);
            return;
        }

        var state = new LocalRequestState(requestId, start, localUrl);
        if (!_requests.TryAdd(requestId, state))
        {
            _ = outbound.WriteAsync(CreateError(requestId, Conflict, $"Duplicate request '{requestId}'."), cancellationToken);
            return;
        }

        _ = Task.Run(() => ForwardAsync(state, outbound, cancellationToken), CancellationToken.None);
    }

    private async Task ForwardAsync(
        LocalRequestState state,
        ChannelWriter<TunnelClientFrame> outbound,
        CancellationToken cancellationToken)
    {
        try
        {
            var start = state.Start;
            var targetUri = TunnelHttp.BuildTargetUri(state.LocalUrl, start.Path, start.QueryString);

            using var request = new HttpRequestMessage(new HttpMethod(start.Method), targetUri);
            if (HasRequestBody(start, out var contentLength))
            {
                request.Content = new ChannelHttpContent(state.Body, contentLength);
            }

            ApplyRequestHeaders(request, start.Headers);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var responseStart = new HttpResponseStart { StatusCode = (int)response.StatusCode };
            responseStart.Headers.AddRange(TunnelHttp.ToTunnelHeaders(
                response.Headers.Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value)),
                TunnelHttp.ShouldForwardResponseHeader));
            responseStart.Headers.AddRange(TunnelHttp.ToTunnelHeaders(
                response.Content.Headers.Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value)),
                TunnelHttp.ShouldForwardResponseHeader));

            await outbound.WriteAsync(new TunnelClientFrame
            {
                RequestId = state.RequestId,
                ResponseStart = responseStart
            }, cancellationToken);

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var buffer = new byte[_chunkSize];
            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await outbound.WriteAsync(new TunnelClientFrame
                {
                    RequestId = state.RequestId,
                    BodyChunk = new BodyChunk
                    {
                        Data = ByteString.CopyFrom(buffer.AsSpan(0, bytesRead))
                    }
                }, cancellationToken);
            }

            await outbound.WriteAsync(new TunnelClientFrame
            {
                RequestId = state.RequestId,
                Complete = new CompleteFrame()
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryWriteErrorAsync(outbound, state.RequestId, ClientClosedRequest, "Request was cancelled.", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to forward tunnel request {RequestId}.", state.RequestId);
            await TryWriteErrorAsync(outbound, state.RequestId, BadGateway, "Local forwarding failed.", CancellationToken.None);
        }
        finally
        {
            _requests.TryRemove(state.RequestId, out _);
            state.CompleteBody();
        }
    }

    private static void ApplyRequestHeaders(HttpRequestMessage request, IEnumerable<Header> headers)
    {
        foreach (var header in headers)
        {
            if (!TunnelHttp.ShouldForwardRequestHeader(header.Name))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Name, header.Values))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Name, header.Values);
            }
        }

        if (request.Content?.Headers.ContentType is null &&
            headers.FirstOrDefault(header => string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase)) is { Values.Count: > 0 } contentType)
        {
            request.Content!.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType.Values[0]);
        }
    }

    private static bool HasRequestBody(HttpRequestStart start, out long? contentLength)
    {
        contentLength = null;

        foreach (var header in start.Headers)
        {
            if (string.Equals(header.Name, "Content-Length", StringComparison.OrdinalIgnoreCase) &&
                header.Values.Count > 0 &&
                long.TryParse(header.Values[0], out var parsedLength))
            {
                contentLength = parsedLength;
                return parsedLength > 0;
            }
        }

        return start.Method is "POST" or "PUT" or "PATCH";
    }

    private static TunnelClientFrame CreateError(string requestId, int statusCode, string message) => new()
    {
        RequestId = requestId,
        Error = new ErrorFrame
        {
            StatusCode = statusCode,
            Message = message
        }
    };

    private static async Task TryWriteErrorAsync(
        ChannelWriter<TunnelClientFrame> outbound,
        string requestId,
        int statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await outbound.WriteAsync(CreateError(requestId, statusCode, message), cancellationToken);
        }
        catch (ChannelClosedException)
        {
        }
    }
}
