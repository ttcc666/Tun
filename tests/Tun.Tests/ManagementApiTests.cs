using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Tun.Contracts.Management;

namespace Tun.Tests;

public sealed class ManagementApiTests
{
    [Fact]
    public async Task Dashboard_IsServedAsStaticPage()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/dashboard/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Tun 管理台", html);
    }

    [Fact]
    public async Task ConfigApi_RequiresManagementToken()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/config/tunnels");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfigApi_UsesForwardedHeadersForPublicOrigin()
    {
        await using var factory = CreateFactory();
        var client = CreateManagementClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/config/tunnels");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "tun.example.com");

        using var response = await client.SendAsync(request);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://tun.example.com", json.RootElement.GetProperty("publicOrigin").GetString());
        Assert.True(json.RootElement.GetProperty("forwardedHeadersEnabled").GetBoolean());
    }

    [Fact]
    public async Task ConfigApi_CreatesListsAndDeletesTunnel()
    {
        await using var factory = CreateFactory();
        var client = CreateManagementClient(factory);
        var request = new UpsertTunnelConfigRequest
        {
            TunnelId = "admin-demo",
            ClientId = "client-a",
            LocalUrl = "http://localhost:5010",
            Enabled = true,
            Description = "Created from test"
        };

        using var createResponse = await client.PostAsJsonAsync("/api/config/tunnels", request);
        var created = await createResponse.Content.ReadFromJsonAsync<ManagedTunnelConfig>();

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("admin-demo", created!.TunnelId);
        Assert.Equal("client-a", created.ClientId);

        var listJson = await client.GetStringAsync("/api/config/tunnels");
        Assert.Contains("admin-demo", listJson);
        Assert.Contains("Created from test", listJson);

        using var deleteResponse = await client.DeleteAsync("/api/config/tunnels/admin-demo");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await client.GetStringAsync("/api/config/tunnels");
        Assert.DoesNotContain("admin-demo", afterDelete);
    }

    [Fact]
    public async Task ClientConfig_ReturnsOnlyEnabledTunnelsForClient()
    {
        await using var factory = CreateFactory();
        var client = CreateManagementClient(factory);

        await client.PostAsJsonAsync("/api/config/tunnels", new UpsertTunnelConfigRequest
        {
            TunnelId = "enabled-one",
            ClientId = "client-a",
            LocalUrl = "http://localhost:5011",
            Enabled = true
        });
        await client.PostAsJsonAsync("/api/config/tunnels", new UpsertTunnelConfigRequest
        {
            TunnelId = "disabled-one",
            ClientId = "client-a",
            LocalUrl = "http://localhost:5012",
            Enabled = false
        });
        await client.PostAsJsonAsync("/api/config/tunnels", new UpsertTunnelConfigRequest
        {
            TunnelId = "other-client",
            ClientId = "client-b",
            LocalUrl = "http://localhost:5013",
            Enabled = true
        });

        var config = await client.GetFromJsonAsync<ClientTunnelConfigResponse>("/api/client-config/client-a");

        Assert.NotNull(config);
        var tunnel = Assert.Single(config!.Tunnels);
        Assert.Equal("enabled-one", tunnel.TunnelId);
        Assert.Equal("http://localhost:5011", tunnel.LocalUrl);
    }

    [Fact]
    public async Task ClientConfig_DeduplicatesPersistedTunnelIds()
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"tun-test-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(configPath, """
            [
              {
                "tunnelId": "demo",
                "clientId": "dev-client",
                "localUrl": "http://localhost:5000",
                "enabled": true
              },
              {
                "tunnelId": "demo",
                "clientId": "dev-client",
                "localUrl": "http://localhost:5001",
                "enabled": true
              }
            ]
            """);

        await using var factory = CreateFactory(configPath);
        var client = CreateManagementClient(factory);

        var config = await client.GetFromJsonAsync<ClientTunnelConfigResponse>("/api/client-config/dev-client");

        Assert.NotNull(config);
        var tunnel = Assert.Single(config!.Tunnels);
        Assert.Equal("demo", tunnel.TunnelId);
        Assert.Equal("http://localhost:5001", tunnel.LocalUrl);

        var persisted = JsonSerializer.Deserialize<List<ManagedTunnelConfig>>(
            await File.ReadAllTextAsync(configPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Single(persisted!);
    }

    private static WebApplicationFactory<Program> CreateFactory(string? configPath = null)
    {
        configPath ??= Path.Combine(Path.GetTempPath(), $"tun-test-{Guid.NewGuid():N}.json");

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Tun:Token"] = "test-token",
                        ["Tun:ManagementToken"] = "test-token",
                        ["Tun:ConfigPath"] = configPath,
                        ["Tun:ConfiguredTunnels:0:TunnelId"] = "seed",
                        ["Tun:ConfiguredTunnels:0:ClientId"] = "seed-client",
                        ["Tun:ConfiguredTunnels:0:LocalUrl"] = "http://localhost:5000",
                        ["Tun:ConfiguredTunnels:0:Enabled"] = "true"
                    });
                });
            });
    }

    private static HttpClient CreateManagementClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tun-Token", "test-token");
        return client;
    }
}
