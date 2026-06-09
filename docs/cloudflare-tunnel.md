# Cloudflare Tunnel 方案A

方案A的目标是只把 `Tun.Server` 的 public HTTP 入口交给 Cloudflare Tunnel：

```text
Public user
  -> https://tun.ttcc0313.ggff.net
  -> Cloudflare Tunnel / cloudflared
  -> http://127.0.0.1:8080
  -> Tun.Server
  -> Tun.Client
  -> local service
```

`http://127.0.0.1:8081` 是 Tun.Client 连接 Tun.Server 的 gRPC h2c 入口，不需要暴露给 Cloudflare。

## 代码侧已支持

- `Tun.Server` 默认启用 ASP.NET Core forwarded headers，中间件会在 static files、dashboard、API、tunnel route 之前运行。
- dashboard 的配置列表会按当前入口生成 tunnel 访问地址；通过 Cloudflare 域名打开 dashboard 时，会显示 `https://tun.ttcc0313.ggff.net/t/{tunnelId}/`。
- `/api/config/tunnels` 会返回 `publicOrigin` 和 `forwardedHeadersEnabled`，方便验证代理头是否生效。

## 当前本机配置

当前已经跑通的 tunnel 信息：

```text
Tunnel ID: 94d5f197-0a7c-416f-becd-5fb65ad7bb1a
Hostname:  tun.ttcc0313.ggff.net
Origin:    http://127.0.0.1:8080
Config:    C:\Users\KGMCW\.cloudflared\config.yml
```

注意：之前在 CMD 里用了单引号，导致 Cloudflare Tunnel 名字实际变成了 `'tun-server'`。后续命令建议直接使用 tunnel ID，避免名称里的引号造成歧义。

## 准备 Cloudflare Tunnel

先安装并登录 `cloudflared`：

```cmd
winget install --exact --id Cloudflare.cloudflared --source winget
cloudflared tunnel login
```

创建 tunnel，并把域名路由到 tunnel：

```cmd
cloudflared tunnel create "tun-server"
cloudflared tunnel route dns "tun-server" "tun.ttcc0313.ggff.net"
```

如果 tunnel 名字已经异常，直接用 tunnel ID 创建 DNS 路由：

```cmd
cloudflared tunnel route dns 94d5f197-0a7c-416f-becd-5fb65ad7bb1a tun.ttcc0313.ggff.net
```

当前 `C:\Users\KGMCW\.cloudflared\config.yml`：

```yaml
tunnel: 94d5f197-0a7c-416f-becd-5fb65ad7bb1a
credentials-file: C:\Users\KGMCW\.cloudflared\94d5f197-0a7c-416f-becd-5fb65ad7bb1a.json

ingress:
  - hostname: tun.ttcc0313.ggff.net
    service: http://127.0.0.1:8080
  - service: http_status:404
```

验证配置语法：

```cmd
cloudflared tunnel ingress validate
```

## 启动 Tun

启动 Tun.Server 时把 token 换掉，并限制 forwarded host：

```powershell
cd 'D:\Study\CSharp\Tun'
$env:Tun__Token = '<replace-with-strong-token>'
$env:Tun__ManagementToken = '<replace-with-strong-management-token>'
$env:Tun__ForwardedHeaders__AllowedHosts__0 = 'tun.ttcc0313.ggff.net'
dotnet run --project 'src\Tun.Server\Tun.Server.csproj'
```

启动 cloudflared：

```cmd
cloudflared tunnel run 94d5f197-0a7c-416f-becd-5fb65ad7bb1a
```

Tun.Client 仍然连接本机 gRPC 入口和本机管理入口：

```powershell
cd 'D:\Study\CSharp\Tun'
$env:Tun__Token = '<same-token-as-server>'
$env:Tun__ServerUrl = 'http://127.0.0.1:8081'
$env:Tun__ManagementUrl = 'http://127.0.0.1:8080'
dotnet run --project 'src\Tun.Client\Tun.Client.csproj'
```

## 验证

```cmd
curl https://tun.ttcc0313.ggff.net/healthz
curl https://tun.ttcc0313.ggff.net/t/demo/health
```

dashboard：

```text
https://tun.ttcc0313.ggff.net/dashboard/
```

LicenseServer 示例：

```text
https://tun.ttcc0313.ggff.net/t/LicenseServer/app/login
```

查看当前 tunnel 状态：

```cmd
cloudflared tunnel list
```

本机 origin 健康检查：

```cmd
curl http://127.0.0.1:8080/healthz
```

## CMD 引号注意

在 Windows CMD 里，不要使用单引号。CMD 不会把 `'...'` 当字符串引号，cloudflared 会收到带单引号的参数。

错误示例：

```cmd
cloudflared tunnel create 'tun-server'
cloudflared tunnel route dns 'tun-server' 'tun.ttcc0313.ggff.net'
```

正确示例：

```cmd
cloudflared tunnel create "tun-server"
cloudflared tunnel route dns "tun-server" "tun.ttcc0313.ggff.net"
```

## 安全注意

- 不要把 `dev-token` 暴露到公网。
- 先只发布 `8080`，不要发布 `8081`。
- 建议在 Cloudflare Zero Trust 里给 `/dashboard/` 加 Access 保护。
- 当前 V1 仍然是 path-prefix HTTP tunnel，不支持 WebSocket、SSE、raw TCP。

参考：

- Cloudflare Tunnel locally-managed tunnel: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/local-management/create-local-tunnel/
- Cloudflare Tunnel configuration file: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/local-management/configuration-file/
- Cloudflare Tunnel DNS routing: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/routing-to-tunnel/dns/
- ASP.NET Core proxy and forwarded headers: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0
