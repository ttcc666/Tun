using Google.Protobuf;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using System.Threading.Channels;
using Tun.Contracts.Grpc;
using Tun.Contracts.Http;
using Tun.Server.Configuration;

namespace Tun.Server.Tunnels;

public sealed class TunnelRequestDispatcher(
    TunnelRegistry registry,
    IOptions<TunnelServerOptions> options,
    ILogger<TunnelRequestDispatcher> logger)
{
    public async Task HandleAsync(HttpContext context, string tunnelId, string? path)
    {
        if (!registry.TryGet(tunnelId, out var tunnel))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = $"Tunnel '{tunnelId}' is not online." });
            return;
        }

        var requestId = Guid.NewGuid().ToString("N");
        var responses = tunnel.Connection.RegisterRequest(requestId);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, options.Value.RequestTimeoutSeconds)));

        try
        {
            await SendRequestAsync(context, tunnel, requestId, path, timeout.Token);
            await RelayResponseAsync(context, tunnelId, requestId, responses, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                await context.Response.WriteAsJsonAsync(new { error = "Tunnel request timed out." });
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tunnel request {RequestId} failed for {Url}.", requestId, context.Request.GetDisplayUrl());

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                await context.Response.WriteAsJsonAsync(new { error = "Tunnel request failed." });
            }
        }
        finally
        {
            tunnel.Connection.CompleteRequest(requestId);
        }
    }

    private async Task SendRequestAsync(
        HttpContext context,
        RegisteredTunnel tunnel,
        string requestId,
        string? path,
        CancellationToken cancellationToken)
    {
        var start = new HttpRequestStart
        {
            TunnelId = tunnel.TunnelId,
            Method = context.Request.Method,
            Path = string.IsNullOrEmpty(path) ? "/" : "/" + path.TrimStart('/'),
            QueryString = context.Request.QueryString.Value ?? string.Empty,
            Scheme = context.Request.Scheme
        };

        start.Headers.AddRange(TunnelHttp.ToTunnelHeaders(
            context.Request.Headers.Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value)),
            TunnelHttp.ShouldForwardRequestHeader));

        await tunnel.Connection.SendAsync(new TunnelServerFrame
        {
            RequestId = requestId,
            RequestStart = start
        }, cancellationToken);

        var buffer = new byte[Math.Max(1024, options.Value.ChunkSize)];
        int bytesRead;
        while ((bytesRead = await context.Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await tunnel.Connection.SendAsync(new TunnelServerFrame
            {
                RequestId = requestId,
                BodyChunk = new BodyChunk
                {
                    Data = ByteString.CopyFrom(buffer.AsSpan(0, bytesRead))
                }
            }, cancellationToken);
        }

        await tunnel.Connection.SendAsync(new TunnelServerFrame
        {
            RequestId = requestId,
            Complete = new CompleteFrame()
        }, cancellationToken);
    }

    private async Task RelayResponseAsync(
        HttpContext context,
        string tunnelId,
        string requestId,
        ChannelReader<TunnelClientFrame> responses,
        CancellationToken cancellationToken)
    {
        var responseStarted = false;
        HttpResponseStart? responseStart = null;
        MemoryStream? rewriteBuffer = null;

        await foreach (var frame in responses.ReadAllAsync(cancellationToken))
        {
            switch (frame.KindCase)
            {
                case TunnelClientFrame.KindOneofCase.ResponseStart:
                    responseStart = frame.ResponseStart;
                    if (TunnelPathRewriter.CanRewrite(responseStart))
                    {
                        rewriteBuffer = new MemoryStream();
                    }
                    else
                    {
                        ApplyResponseStart(context, responseStart);
                    }

                    responseStarted = true;
                    break;

                case TunnelClientFrame.KindOneofCase.BodyChunk:
                    if (!responseStarted)
                    {
                        context.Response.StatusCode = StatusCodes.Status502BadGateway;
                        responseStarted = true;
                    }

                    if (rewriteBuffer is not null)
                    {
                        rewriteBuffer.Write(frame.BodyChunk.Data.Span);
                    }
                    else
                    {
                        await context.Response.Body.WriteAsync(frame.BodyChunk.Data.Memory, cancellationToken);
                    }

                    break;

                case TunnelClientFrame.KindOneofCase.Complete:
                    if (rewriteBuffer is not null && responseStart is not null)
                    {
                        ApplyResponseStart(context, responseStart, rewrittenResponse: true);
                        var rewritten = TunnelPathRewriter.Rewrite(responseStart, rewriteBuffer.ToArray(), tunnelId);
                        await context.Response.Body.WriteAsync(rewritten, cancellationToken);
                    }

                    return;

                case TunnelClientFrame.KindOneofCase.Error:
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = frame.Error.StatusCode == 0
                            ? StatusCodes.Status502BadGateway
                            : frame.Error.StatusCode;
                        await context.Response.WriteAsJsonAsync(new { error = frame.Error.Message }, cancellationToken);
                    }
                    return;
            }
        }

        throw new IOException($"Tunnel disconnected before request {requestId} completed.");
    }

    private static void ApplyResponseStart(
        HttpContext context,
        HttpResponseStart responseStart,
        bool rewrittenResponse = false)
    {
        context.Response.StatusCode = responseStart.StatusCode == 0
            ? StatusCodes.Status200OK
            : responseStart.StatusCode;

        foreach (var header in responseStart.Headers)
        {
            if (!TunnelHttp.ShouldForwardResponseHeader(header.Name))
            {
                continue;
            }

            if (rewrittenResponse && TunnelPathRewriter.ShouldSkipRewrittenResponseHeader(header.Name))
            {
                continue;
            }

            context.Response.Headers[header.Name] = header.Values.ToArray();
        }

        if (rewrittenResponse)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }
    }
}
