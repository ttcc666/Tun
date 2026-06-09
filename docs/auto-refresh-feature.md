# 配置自动刷新功能

## 功能概述

实现了 Server 推送配置更新通知机制，使得在 Dashboard 中新增或修改 tunnel 配置后，Client 能够自动接收通知并重新注册，无需手动重启。

## 实现方案

使用 **方案 B：Server 推送**，通过 gRPC 双向流实现配置变更的实时推送。

### 架构流程

```
Dashboard 新增配置
    ↓
ManagedTunnelStore.Upsert()
    ↓
触发 ConfigChanged 事件
    ↓
TunnelGrpcService 接收事件
    ↓
通过 gRPC 推送 ConfigUpdateNotification
    ↓
TunnelClientWorker 接收通知
    ↓
断开当前连接（触发重连）
    ↓
重新拉取配置并注册所有 tunnel
    ↓
新 tunnel 自动上线
```

## 代码修改

### 1. proto 定义 (`src/Tun.Contracts/Protos/tunnel.proto`)

```protobuf
message TunnelServerFrame {
  string request_id = 1;
  oneof kind {
    HttpRequestStart request_start = 2;
    BodyChunk body_chunk = 3;
    CompleteFrame complete = 4;
    ErrorFrame error = 5;
    ConfigUpdateNotification config_update = 6;  // 新增
  }
}

message ConfigUpdateNotification {
  string message = 1;
}
```

### 2. Server 端配置变更事件 (`src/Tun.Server/Management/ManagedTunnelStore.cs`)

```csharp
public sealed class ManagedTunnelStore
{
    // 新增事件
    public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

    public ManagedTunnelConfig Upsert(UpsertTunnelConfigRequest request)
    {
        // ... 保存配置
        SaveLocked();
        NotifyConfigChanged(updated.ClientId);  // 触发事件
        return updated;
    }

    private void NotifyConfigChanged(string clientId)
    {
        ConfigChanged?.Invoke(this, new ConfigChangedEventArgs(clientId));
    }
}

public sealed class ConfigChangedEventArgs(string clientId) : EventArgs
{
    public string ClientId { get; } = clientId;
}
```

### 3. Server 端推送通知 (`src/Tun.Server/Tunnels/TunnelGrpcService.cs`)

```csharp
public sealed class TunnelGrpcService(
    TunnelRegistry registry,
    ManagedTunnelStore tunnelStore,  // 注入 ManagedTunnelStore
    IOptions<TunnelServerOptions> options,
    ILogger<TunnelGrpcService> logger) : Tunnel.TunnelBase
{
    public override async Task Connect(...)
    {
        var connection = new TunnelConnection(register.ClientId.Trim());
        registry.Register(connection, register.Tunnels);

        // 订阅配置变更事件
        void OnConfigChanged(object? sender, ConfigChangedEventArgs e)
        {
            if (string.Equals(e.ClientId, connection.ClientId, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Notifying client {ClientId} of config change.", connection.ClientId);
                connection.SendConfigUpdateNotification();
            }
        }

        tunnelStore.ConfigChanged += OnConfigChanged;

        try
        {
            // ... 处理连接
        }
        finally
        {
            tunnelStore.ConfigChanged -= OnConfigChanged;  // 取消订阅
            registry.Remove(connection);
            connection.Complete();
        }
    }
}
```

### 4. Server 端发送通知 (`src/Tun.Server/Tunnels/TunnelConnection.cs`)

```csharp
public void SendConfigUpdateNotification()
{
    Touch();
    var frame = new TunnelServerFrame
    {
        ConfigUpdate = new ConfigUpdateNotification
        {
            Message = "Configuration updated"
        }
    };
    _outbound.Writer.TryWrite(frame);
}
```

### 5. Client 端接收并处理通知 (`src/Tun.Client/Tunnels/TunnelClientWorker.cs`)

```csharp
try
{
    await foreach (var frame in call.ResponseStream.ReadAllAsync(cancellationToken))
    {
        // 检查是否为配置更新通知
        if (frame.KindCase == TunnelServerFrame.KindOneofCase.ConfigUpdate)
        {
            logger.LogInformation("Received config update notification. Reconnecting to apply new configuration...");
            break;  // 退出当前连接，触发重连并重新拉取配置
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
```

## 测试步骤

1. **启动服务**
   ```powershell
   .\start-all-production.ps1
   .\start-cloudflared.ps1
   ```

2. **添加新配置**
   ```powershell
   $newTunnel = @{
       tunnelId = "test-tunnel"
       clientId = "dev-client"
       localUrl = "http://localhost:5000"
       enabled = $true
   } | ConvertTo-Json

   Invoke-RestMethod -Uri 'http://127.0.0.1:8080/api/config/tunnels' `
       -Method Post `
       -Headers @{ 'X-Tun-Token' = 'dev-token'; 'Content-Type' = 'application/json' } `
       -Body $newTunnel
   ```

3. **观察日志**
   - **Server 窗口**：应该看到 `Notifying client dev-client of config change.`
   - **Client 窗口**：应该看到 `Received config update notification. Reconnecting...`

4. **验证结果**
   ```powershell
   # 检查在线 tunnels
   Invoke-RestMethod -Uri 'http://127.0.0.1:8080/api/tunnels'

   # 测试新 tunnel
   curl -H "Host: test-tunnel.ttcc0313.ggff.net" http://127.0.0.1:8080/
   ```

## 日志示例

### Server 日志
```
info: Tun.Server.Tunnels.TunnelGrpcService[0]
      Tunnel client dev-client registered 1 tunnel(s).
info: Tun.Server.Management.ManagedTunnelStore[0]
      Notifying client dev-client of config change.
info: Tun.Server.Tunnels.TunnelGrpcService[0]
      Tunnel client dev-client disconnected.
info: Tun.Server.Tunnels.TunnelGrpcService[0]
      Tunnel client dev-client registered 2 tunnel(s).
```

### Client 日志
```
info: Tun.Client.Tunnels.TunnelClientWorker[0]
      Connected to tunnel server http://127.0.0.1:8081 as dev-client with 1 tunnel(s).
info: Tun.Client.Tunnels.TunnelClientWorker[0]
      Received config update notification. Reconnecting to apply new configuration...
info: Tun.Client.Tunnels.TunnelClientWorker[0]
      Connected to tunnel server http://127.0.0.1:8081 as dev-client with 2 tunnel(s).
```

## 优势

- ✅ **实时生效**：配置更新后立即推送，无延迟
- ✅ **自动化**：无需手动重启 Client
- ✅ **可靠性**：使用 gRPC 双向流，连接断开会自动重连
- ✅ **精确通知**：只通知相关的 Client（根据 ClientId 过滤）

## 注意事项

1. **事件订阅生命周期**：事件在连接建立时订阅，断开时取消订阅，避免内存泄漏
2. **重连机制**：Client 使用指数退避重连策略，保证稳定性
3. **配置拉取**：重连时会重新调用 `/api/client-config/{clientId}` 拉取最新配置
4. **并发安全**：ManagedTunnelStore 使用锁保证线程安全

## 对比

| 方案 | 延迟 | 实现复杂度 | 资源消耗 | 可靠性 |
|-----|------|----------|---------|--------|
| 方案 A：定期轮询 | 30-60秒 | 低 | 中等 | 中等 |
| **方案 B：Server 推送** | **实时** | **中等** | **低** | **高** |
| 方案 C：手动触发 | 无限 | 极低 | 极低 | 低 |

## 后续优化

- [ ] 添加配置版本号，避免重复推送
- [ ] 支持批量配置变更（延迟推送）
- [ ] 添加 Prometheus 指标监控配置变更频率
- [ ] Dashboard UI 显示实时刷新状态
