using Google.Protobuf;
using System.Net;
using System.Threading.Channels;

namespace Tun.Client.Tunnels;

internal sealed class ChannelHttpContent(ChannelReader<ByteString> chunks, long? contentLength) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken)
    {
        await foreach (var chunk in chunks.ReadAllAsync(cancellationToken))
        {
            await stream.WriteAsync(chunk.Memory, cancellationToken);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        if (contentLength is { } knownLength)
        {
            length = knownLength;
            return true;
        }

        length = -1;
        return false;
    }
}
