# Tun

Tun is a .NET 10 local-first HTTP tunnel management system. `Tun.Server` exposes public HTTP routes, a Web Dashboard, management APIs, and a gRPC tunnel endpoint. `Tun.Client` connects back to the server, pulls its managed tunnel configuration, and forwards traffic to local services. `Tun.SampleApp` is a local app for manual verification.

## Verify

```powershell
cd 'D:\Study\CSharp\Tun'
dotnet build 'Tun.slnx'
dotnet test 'Tun.slnx'
```

## Manual Run

Open three terminals:

```powershell
cd 'D:\Study\CSharp\Tun'
dotnet run --project 'samples\Tun.SampleApp\Tun.SampleApp.csproj'
```

```powershell
cd 'D:\Study\CSharp\Tun'
dotnet run --project 'src\Tun.Server\Tun.Server.csproj'
```

```powershell
cd 'D:\Study\CSharp\Tun'
dotnet run --project 'src\Tun.Client\Tun.Client.csproj'
```

Then call the public HTTP endpoint:

```powershell
# Using subdomain mode (default)
$request = @{
    Uri = 'http://127.0.0.1:8080/health'
    Headers = @{ Host = 'demo.localhost' }
}
Invoke-WebRequest @request
```

Open the management dashboard:

```powershell
start 'http://127.0.0.1:8080/dashboard/'
```

Default management token: `dev-token`.

Default ports:

- Public HTTP entry: `http://127.0.0.1:8080`
- gRPC tunnel entry: `http://127.0.0.1:8081`
- Sample local app: `http://localhost:5000`

Server-managed tunnel config is stored at `src/Tun.Server/data/tunnels.json` by default. Change `Tun:Token` and `Tun:ManagementToken` before any non-local deployment.

## Subdomain Mode

Tun uses subdomain-based routing: `{tunnelId}.yourdomain.com` routes to the configured local service.

- Root domain (e.g., `ttcc0313.ggff.net`) hosts the management dashboard and APIs
- Subdomains (e.g., `demo.ttcc0313.ggff.net`) route to tunnel targets

Reserved subdomain names: `www`, `api`, `admin`, `dashboard`, `console`, `healthz`, `health`, `status`, `metrics`, `grpc`, `ws`, `websocket`, `cdn`, `static`

## Cloudflare Tunnel

Use scheme A for public relay: expose only `Tun.Server` public HTTP `8080` through Cloudflare Tunnel, keep gRPC `8081` local for `Tun.Client`.

See `docs/cloudflare-tunnel.md` and `deploy/cloudflare/`.
