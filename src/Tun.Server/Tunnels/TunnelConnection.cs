using System.Collections.Concurrent;
using System.Threading.Channels;
using Tun.Contracts.Grpc;

namespace Tun.Server.Tunnels;

public sealed class TunnelConnection
{
    private readonly Channel<TunnelServerFrame> _outbound =
        Channel.CreateUnbounded<TunnelServerFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<string, Channel<TunnelClientFrame>> _responses = new(StringComparer.Ordinal);
    private readonly List<(string TunnelId, string LocalUrl)> _tunnels = [];
    private long _requestCount;

    public TunnelConnection(string clientId)
    {
        ClientId = clientId;
        ConnectedAt = DateTimeOffset.UtcNow;
        LastActivityAt = ConnectedAt;
    }

    public string ClientId { get; }

    public DateTimeOffset ConnectedAt { get; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public long RequestCount => Interlocked.Read(ref _requestCount);

    public ChannelReader<TunnelServerFrame> Outbound => _outbound.Reader;

    public IReadOnlyList<(string TunnelId, string LocalUrl)> Tunnels => _tunnels;

    public void SetTunnels(IEnumerable<(string TunnelId, string LocalUrl)> tunnels)
    {
        _tunnels.Clear();
        _tunnels.AddRange(tunnels);
        Touch();
    }

    public ChannelReader<TunnelClientFrame> RegisterRequest(string requestId)
    {
        var channel = Channel.CreateUnbounded<TunnelClientFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        if (!_responses.TryAdd(requestId, channel))
        {
            throw new InvalidOperationException($"Duplicate request id: {requestId}");
        }

        Interlocked.Increment(ref _requestCount);
        Touch();
        return channel.Reader;
    }

    public async ValueTask SendAsync(TunnelServerFrame frame, CancellationToken cancellationToken)
    {
        Touch();
        await _outbound.Writer.WriteAsync(frame, cancellationToken);
    }

    public void Dispatch(TunnelClientFrame frame)
    {
        Touch();
        if (string.IsNullOrWhiteSpace(frame.RequestId))
        {
            return;
        }

        if (_responses.TryGetValue(frame.RequestId, out var channel))
        {
            channel.Writer.TryWrite(frame);

            if (frame.KindCase is TunnelClientFrame.KindOneofCase.Complete or TunnelClientFrame.KindOneofCase.Error)
            {
                CompleteRequest(frame.RequestId);
            }
        }
    }

    public void CompleteRequest(string requestId)
    {
        if (_responses.TryRemove(requestId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }

    public void Complete(Exception? exception = null)
    {
        _outbound.Writer.TryComplete(exception);

        foreach (var (requestId, channel) in _responses)
        {
            if (_responses.TryRemove(requestId, out _))
            {
                channel.Writer.TryComplete(exception);
            }
        }
    }

    private void Touch() => LastActivityAt = DateTimeOffset.UtcNow;
}
