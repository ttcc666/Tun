using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tun.Client.Configuration;
using Tun.Client.Tunnels;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("tun.client.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.Configure<TunnelClientOptions>(builder.Configuration.GetSection("Tun"));
builder.Services.AddSingleton<LocalTunnelForwarder>();
builder.Services.AddHostedService<TunnelClientWorker>();

await builder.Build().RunAsync();
