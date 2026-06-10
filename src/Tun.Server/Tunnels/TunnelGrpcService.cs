using Grpc.Core;
using Microsoft.Extensions.Options;
using Tun.Contracts.Grpc;
using Tun.Server.Application.Services.Interfaces;
using Tun.Server.Domain.Configuration;

namespace Tun.Server.Tunnels;

public sealed class TunnelGrpcService(
    TunnelRegistry registry,
    IOptions<ServerOptions> serverOptions,
    ILogger<TunnelGrpcService> logger) : Tunnel.TunnelBase
{
    public override async Task Connect(
        IAsyncStreamReader<TunnelClientFrame> requestStream,
        IServerStreamWriter<TunnelServerFrame> responseStream,
        ServerCallContext context)
    {
        var cancellationToken = context.CancellationToken;

        if (!await requestStream.MoveNext(cancellationToken))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Registration frame is required."));
        }

        var registerFrame = requestStream.Current;
        if (registerFrame.KindCase != TunnelClientFrame.KindOneofCase.Register)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "First frame must be Register."));
        }

        var register = registerFrame.Register;
        if (!string.Equals(register.Token, serverOptions.Value.Token, StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid tunnel token."));
        }

        if (string.IsNullOrWhiteSpace(register.ClientId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "ClientId is required."));
        }

        var connection = new TunnelConnection(register.ClientId.Trim());
        registry.Register(connection, register.Tunnels);
        logger.LogInformation(
            "Tunnel client {ClientId} registered {TunnelCount} tunnel(s).",
            connection.ClientId,
            connection.Tunnels.Count);

        var writeTask = WriteOutboundAsync(connection, responseStream, cancellationToken);

        try
        {
            while (await requestStream.MoveNext(cancellationToken))
            {
                connection.Dispatch(requestStream.Current);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Tunnel client {ClientId} disconnected by cancellation.", connection.ClientId);
        }
        finally
        {
            registry.Remove(connection);
            connection.Complete();

            try
            {
                await writeTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }

            logger.LogInformation("Tunnel client {ClientId} disconnected.", connection.ClientId);
        }
    }

    private static async Task WriteOutboundAsync(
        TunnelConnection connection,
        IServerStreamWriter<TunnelServerFrame> responseStream,
        CancellationToken cancellationToken)
    {
        await foreach (var frame in connection.Outbound.ReadAllAsync(cancellationToken))
        {
            await responseStream.WriteAsync(frame, cancellationToken);
        }
    }
}
