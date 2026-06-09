using Google.Protobuf;
using System.Threading.Channels;
using Tun.Contracts.Grpc;

namespace Tun.Client.Tunnels;

internal sealed class LocalRequestState
{
    private readonly Channel<ByteString> _body =
        Channel.CreateUnbounded<ByteString>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public LocalRequestState(string requestId, HttpRequestStart start, Uri localUrl)
    {
        RequestId = requestId;
        Start = start;
        LocalUrl = localUrl;
    }

    public string RequestId { get; }

    public HttpRequestStart Start { get; }

    public Uri LocalUrl { get; }

    public ChannelReader<ByteString> Body => _body.Reader;

    public ValueTask WriteBodyAsync(ByteString data, CancellationToken cancellationToken) =>
        _body.Writer.WriteAsync(data, cancellationToken);

    public void CompleteBody(Exception? exception = null) => _body.Writer.TryComplete(exception);
}
