# 启动完整的 Tun 系统（生产配置）
# 使用方式: .\start-all-production.ps1

Write-Host "=== 启动 Tun 完整系统 (生产配置) ===`n" -ForegroundColor Cyan

$projectRoot = $PSScriptRoot

# 启动 Sample App
Write-Host "1️⃣ 启动 Sample App..." -ForegroundColor Yellow
Start-Process pwsh -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd '$projectRoot'; dotnet run --project 'samples\Tun.SampleApp\Tun.SampleApp.csproj'"
)

Start-Sleep -Seconds 2

# 启动 Server
Write-Host "2️⃣ 启动 Tun.Server..." -ForegroundColor Yellow
Start-Process pwsh -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd '$projectRoot'; .\start-server-production.ps1"
)

Start-Sleep -Seconds 3

# 启动 Client
Write-Host "3️⃣ 启动 Tun.Client..." -ForegroundColor Yellow
$env:Tun__Token = "dev-token"  # ⚠️ 与 Server 保持一致
$env:Tun__ServerUrl = "http://127.0.0.1:8081"
$env:Tun__ManagementUrl = "http://127.0.0.1:8080"

Start-Process pwsh -ArgumentList @(
    "-NoExit",
    "-Command",
    "cd '$projectRoot'; `$env:Tun__Token='dev-token'; `$env:Tun__ServerUrl='http://127.0.0.1:8081'; `$env:Tun__ManagementUrl='http://127.0.0.1:8080'; dotnet run --project 'src\Tun.Client\Tun.Client.csproj'"
)

Write-Host "`n✅ 所有服务已在新窗口中启动`n" -ForegroundColor Green
Write-Host "📋 访问信息：" -ForegroundColor Cyan
Write-Host "   本地 Dashboard: http://127.0.0.1:8080/dashboard/"
Write-Host "   公网 Dashboard: https://ttcc0313.ggff.net/dashboard/"
Write-Host "   示例 Tunnel: https://demo.ttcc0313.ggff.net/health"
Write-Host ""
Write-Host "⚠️  记得启动 cloudflared tunnel！" -ForegroundColor Yellow
Write-Host "   cloudflared tunnel run 94d5f197-0a7c-416f-becd-5fb65ad7bb1a"
