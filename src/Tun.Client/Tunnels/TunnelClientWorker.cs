using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Threading.Channels;
using Tun.Client.Configuration;
using Tun.Contracts.Grpc;
using Tun.Contracts.Management;

namespace Tun.Client.Tunnels;

public sealed class TunnelClientWorker(
    IOptions<TunnelClientOptions> options,
    LocalTunnelForwarder forwarder,
    ILogger<TunnelClientWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, options.Value.InitialReconnectDelaySeconds));
        var maxDelay = TimeSpan.FromSeconds(Math.Max(1, options.Value.MaxReconnectDelaySeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Max(1, options.Value.InitialReconnectDelaySeconds));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tunnel connection failed. Reconnecting in {DelaySeconds}s.", delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(maxDelay.TotalSeconds, delay.TotalSeconds * 2));
            }
        }
    }

    private async Task RunConnectionAsync(CancellationToken cancellationToken)
    {
        ValidateOptions();
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var tunnels = await ResolveTunnelsAsync(cancellationToken);
        forwarder.SetTunnels(tunnels);

        using var channel = GrpcChannel.ForAddress(options.Value.ServerUrl);
        var client = new Tunnel.TunnelClient(channel);
        using var call = client.Connect(cancellationToken: cancellationToken);

        var outbound = Channel.CreateUnbounded<TunnelClientFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var writeTask = WriteOutboundAsync(call.RequestStream, outbound.Reader, cancellationToken);

        await outbound.Writer.WriteAsync(CreateRegisterFrame(tunnels), cancellationToken);
        logger.LogInformation(
            "Connected to tunnel server {ServerUrl} as {ClientId} with {TunnelCount} tunnel(s).",
            options.Value.ServerUrl,
            options.Value.ClientId,
            tunnels.Count);

        try
        {
            await foreach (var frame in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                if (frame.KindCase == TunnelServerFrame.KindOneofCase.ConfigUpdate)
                {
                    logger.LogInformation("Received config update notification. Reconnecting to apply new configuration...");
                    break; // 退出当前连接，触发重连并重新拉取配置
                }

                await forwarder.HandleFrameAsync(frame, outbound.Writer, cancellationToken);
            }
        }
        finally
        {
            forwarder.AbortAll(new IOException("Tunnel connection closed."));
            outbound.Writer.TryComplete();
            await writeTask;
        }
    }

    private static async Task WriteOutboundAsync(
        IClientStreamWriter<TunnelClientFrame> requestStream,
        ChannelReader<TunnelClientFrame> outbound,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var frame in outbound.ReadAllAsync(cancellationToken))
            {
                await requestStream.WriteAsync(frame, cancellationToken);
            }

            await requestStream.CompleteAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<IReadOnlyList<TunnelClientRegistration>> ResolveTunnelsAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.UseServerConfig)
        {
            return options.Value.Tunnels;
        }

        try
        {
            using var httpClient = new HttpClient { BaseAddress = new Uri(options.Value.ManagementUrl, UriKind.Absolute) };
            httpClient.DefaultRequestHeaders.Add("X-Tun-Token", options.Value.Token);

            var response = await httpClient.GetFromJsonAsync<ClientTunnelConfigResponse>(
                $"/api/client-config/{Uri.EscapeDataString(options.Value.ClientId)}",
                cancellationToken);

            var managedTunnels = response?.Tunnels
                .Select(tunnel => new TunnelClientRegistration
                {
                    TunnelId = tunnel.TunnelId,
                    LocalUrl = tunnel.LocalUrl
                })
                .Where(tunnel => !string.IsNullOrWhiteSpace(tunnel.TunnelId) && !string.IsNullOrWhiteSpace(tunnel.LocalUrl))
                .ToArray() ?? [];

            if (managedTunnels.Length > 0 || options.Value.RequireServerConfig)
            {
                return managedTunnels;
            }

            logger.LogWarning("No server-managed tunnels found for {ClientId}. Falling back to local client config.", options.Value.ClientId);
        }
        catch (Exception ex) when (!options.Value.RequireServerConfig && ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to load server-managed tunnel config. Falling back to local client config.");
        }

        return options.Value.Tunnels;
    }

    private TunnelClientFrame CreateRegisterFrame(IReadOnlyList<TunnelClientRegistration> tunnels)
    {
        var register = new RegisterRequest
        {
            ClientId = options.Value.ClientId,
            Token = options.Value.Token
        };

        register.Tunnels.AddRange(tunnels.Select(tunnel => new TunnelRegistration
        {
            TunnelId = tunnel.TunnelId,
            LocalUrl = tunnel.LocalUrl
        }));

        return new TunnelClientFrame { Register = register };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(options.Value.ServerUrl))
        {
            throw new InvalidOperationException("Tun:ServerUrl is required.");
        }

        if (options.Value.UseServerConfig && string.IsNullOrWhiteSpace(options.Value.ManagementUrl))
        {
            throw new InvalidOperationException("Tun:ManagementUrl is required when Tun:UseServerConfig is true.");
        }

        if (string.IsNullOrWhiteSpace(options.Value.Token))
        {
            throw new InvalidOperationException("Tun:Token is required.");
        }

        if (!options.Value.UseServerConfig && options.Value.Tunnels.Count == 0)
        {
            throw new InvalidOperationException("At least one Tun:Tunnels entry is required.");
        }
    }
}
