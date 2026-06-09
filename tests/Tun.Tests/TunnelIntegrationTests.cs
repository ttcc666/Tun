using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Threading.Channels;
using Tun.Client.Configuration;
using Tun.Client.Tunnels;
using Tun.Contracts.Grpc;
using Tun.Contracts.Http;

namespace Tun.Tests;

public sealed class TunnelIntegrationTests
{
    [Fact]
    public async Task Tunnel_ForwardsGetRequestAndResponse()
    {
        await using var localApp = await StartLocalAppAsync();
        await using var factory = new WebApplicationFactory<Program>();
        await using var harness = await TunnelClientHarness.StartAsync(factory, CreateClientOptions(GetServerUrl(localApp)));
        var publicClient = factory.CreateClient();

        await WaitForAsync(async () => (await publicClient.GetStringAsync("/api/tunnels")).Contains("demo", StringComparison.OrdinalIgnoreCase));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/t/demo/echo?x=1");
        request.Headers.Add("X-Test", "abc");

        using var response = await publicClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("GET /echo?x=1 body=", body);
        Assert.Equal("abc", response.Headers.GetValues("X-Seen-Test").Single());
    }

    [Fact]
    public async Task Tunnel_ForwardsPostBodyAsStream()
    {
        await using var localApp = await StartLocalAppAsync();
        await using var factory = new WebApplicationFactory<Program>();
        await using var harness = await TunnelClientHarness.StartAsync(factory, CreateClientOptions(GetServerUrl(localApp)));
        var publicClient = factory.CreateClient();
        var payload = new string('a', TunnelHttp.DefaultChunkSize + 100);

        await WaitForAsync(async () => (await publicClient.GetStringAsync("/api/tunnels")).Contains("demo", StringComparison.OrdinalIgnoreCase));

        using var response = await publicClient.PostAsync("/t/demo/upload?mode=raw", new StringContent(payload));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"POST /upload?mode=raw body={payload.Length}", body);
    }

    [Fact]
    public async Task Tunnel_RewritesRootRelativeAssetUrlsInHtml()
    {
        await using var localApp = await StartLocalHtmlAppAsync();
        await using var factory = new WebApplicationFactory<Program>();
        await using var harness = await TunnelClientHarness.StartAsync(factory, CreateClientOptions(GetServerUrl(localApp)));
        var publicClient = factory.CreateClient();

        await WaitForAsync(async () => (await publicClient.GetStringAsync("/api/tunnels")).Contains("demo", StringComparison.OrdinalIgnoreCase));

        using var response = await publicClient.GetAsync("/t/demo/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("href=\"/t/demo/app/assets/index.css?tun=demo\"", html);
        Assert.Contains("src=\"/t/demo/app/assets/index.js?tun=demo\"", html);
        Assert.DoesNotContain("href=\"/app/assets/index.css\"", html);
        Assert.False(response.Headers.Contains("ETag"));
        Assert.False(response.Content.Headers.Contains("Content-Length"));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        using var assetResponse = await publicClient.GetAsync("/t/demo/app/assets/index.css");
        var css = await assetResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);
        Assert.Contains("color", css);
        Assert.Equal("no-store", assetResponse.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UnknownTunnel_Returns404()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var publicClient = factory.CreateClient();

        using var response = await publicClient.GetAsync("/t/missing/health");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_RejectsRegistration()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var channel = CreateGrpcChannel(factory);
        var client = new Tunnel.TunnelClient(channel);
        using var call = client.Connect();

        await call.RequestStream.WriteAsync(new TunnelClientFrame
        {
            Register = new RegisterRequest
            {
                ClientId = "bad-client",
                Token = "wrong-token"
            }
        });

        var ex = await Assert.ThrowsAsync<RpcException>(() => call.ResponseStream.MoveNext(CancellationToken.None));
        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [Fact]
    public async Task Disconnect_RemovesTunnelStatus()
    {
        await using var localApp = await StartLocalAppAsync();
        await using var factory = new WebApplicationFactory<Program>();
        var publicClient = factory.CreateClient();
        var harness = await TunnelClientHarness.StartAsync(factory, CreateClientOptions(GetServerUrl(localApp)));

        await WaitForAsync(async () => (await publicClient.GetStringAsync("/api/tunnels")).Contains("demo", StringComparison.OrdinalIgnoreCase));

        await harness.DisposeAsync();

        await WaitForAsync(async () => !(await publicClient.GetStringAsync("/api/tunnels")).Contains("demo", StringComparison.OrdinalIgnoreCase));
    }

    private static TunnelClientOptions CreateClientOptions(string localUrl) => new()
    {
        ClientId = "test-client",
        ServerUrl = "http://localhost",
        Token = "dev-token",
        Tunnels =
        [
            new TunnelClientRegistration
            {
                TunnelId = "demo",
                LocalUrl = localUrl
            }
        ]
    };

    private static GrpcChannel CreateGrpcChannel(WebApplicationFactory<Program> factory) =>
        GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = factory.Server.CreateHandler()
        });

    private static async Task<WebApplication> StartLocalAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();

        app.MapMethods("/{**path}", TunnelHttp.CommonHttpMethods, async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync(context.RequestAborted);

            context.Response.StatusCode = context.Request.Method == HttpMethods.Post
                ? StatusCodes.Status201Created
                : StatusCodes.Status200OK;
            context.Response.Headers["X-Seen-Test"] = context.Request.Headers["X-Test"].ToString();

            var bodySummary = context.Request.Method == HttpMethods.Post ? body.Length.ToString() : body;
            await context.Response.WriteAsync(
                $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} body={bodySummary}",
                context.RequestAborted);
        });

        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> StartLocalHtmlAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });

        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();

        app.MapGet("/", async context =>
        {
            const string html = """
                <!doctype html>
                <html>
                <head>
                  <link rel="stylesheet" href="/app/assets/index.css">
                </head>
                <body>
                  <script type="module" src="/app/assets/index.js"></script>
                </body>
                </html>
                """;

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(html);
            context.Response.Headers.ETag = "\"test-html\"";
            context.Response.Headers.LastModified = DateTimeOffset.UtcNow.ToString("R");
            await context.Response.WriteAsync(html, context.RequestAborted);
        });

        app.MapGet("/app/assets/index.css", () => Results.Text("body{color:#111}", "text/css"));
        app.MapGet("/app/assets/index.js", () => Results.Text("console.log('ok')", "text/javascript"));

        await app.StartAsync();
        return app;
    }

    private static string GetServerUrl(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        return Assert.Single(addresses!.Addresses);
    }

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("Timed out waiting for condition.");
    }

    private sealed class TunnelClientHarness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly GrpcChannel _channel;
        private readonly AsyncDuplexStreamingCall<TunnelClientFrame, TunnelServerFrame> _call;
        private readonly Channel<TunnelClientFrame> _outbound;
        private readonly LocalTunnelForwarder _forwarder;
        private readonly Task _reader;
        private readonly Task _writer;

        private TunnelClientHarness(
            GrpcChannel channel,
            AsyncDuplexStreamingCall<TunnelClientFrame, TunnelServerFrame> call,
            Channel<TunnelClientFrame> outbound,
            LocalTunnelForwarder forwarder)
        {
            _channel = channel;
            _call = call;
            _outbound = outbound;
            _forwarder = forwarder;
            _reader = Task.Run(ReadLoopAsync);
            _writer = Task.Run(WriteLoopAsync);
        }

        public static async Task<TunnelClientHarness> StartAsync(
            WebApplicationFactory<Program> factory,
            TunnelClientOptions options)
        {
            var channel = CreateGrpcChannel(factory);
            var client = new Tunnel.TunnelClient(channel);
            var call = client.Connect();
            var outbound = Channel.CreateUnbounded<TunnelClientFrame>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            var forwarder = new LocalTunnelForwarder(
                Options.Create(options),
                NullLogger<LocalTunnelForwarder>.Instance);

            var harness = new TunnelClientHarness(channel, call, outbound, forwarder);
            var register = new RegisterRequest
            {
                ClientId = options.ClientId,
                Token = options.Token
            };
            register.Tunnels.AddRange(options.Tunnels.Select(tunnel => new TunnelRegistration
            {
                TunnelId = tunnel.TunnelId,
                LocalUrl = tunnel.LocalUrl
            }));

            await outbound.Writer.WriteAsync(new TunnelClientFrame { Register = register });
            return harness;
        }

        public async ValueTask DisposeAsync()
        {
            _forwarder.AbortAll();
            _outbound.Writer.TryComplete();
            _cts.Cancel();
            _call.Dispose();
            _channel.Dispose();
            _cts.Dispose();

            await IgnoreCancellationAsync(Task.WhenAll(_reader, _writer).WaitAsync(TimeSpan.FromSeconds(2)));
        }

        private async Task ReadLoopAsync()
        {
            await foreach (var frame in _call.ResponseStream.ReadAllAsync(_cts.Token))
            {
                await _forwarder.HandleFrameAsync(frame, _outbound.Writer, _cts.Token);
            }
        }

        private async Task WriteLoopAsync()
        {
            await foreach (var frame in _outbound.Reader.ReadAllAsync(_cts.Token))
            {
                await _call.RequestStream.WriteAsync(frame, _cts.Token);
            }
        }

        private static async Task IgnoreCancellationAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or RpcException or TimeoutException)
            {
            }
        }
    }
}
