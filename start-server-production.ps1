# Tun.Server 生产环境启动脚本
# 使用方式: .\start-server-production.ps1

Write-Host "=== 启动 Tun.Server (生产配置) ===`n" -ForegroundColor Cyan

# 配置环境变量
$env:Tun__BaseDomain = "ttcc0313.ggff.net"
$env:Tun__Token = "dev-token"  # ⚠️ 生产环境请修改为强密码
$env:Tun__ManagementToken = "dev-token"  # ⚠️ 生产环境请修改为强密码
$env:Tun__ValidateHostHeader = "true"
$env:Tun__ForwardedHeaders__AllowedHosts__0 = "ttcc0313.ggff.net"
$env:Tun__ForwardedHeaders__AllowedHosts__1 = "*.ttcc0313.ggff.net"

Write-Host "✅ 环境变量已设置" -ForegroundColor Green
Write-Host "   BaseDomain: $env:Tun__BaseDomain"
Write-Host "   Token: $env:Tun__Token"
Write-Host ""
Write-Host "⚠️  生产环境提醒：请在部署前修改 Token 为强密码！`n" -ForegroundColor Yellow

# 启动服务
Set-Location $PSScriptRoot
dotnet run --project 'src\Tun.Server\Tun.Server.csproj' --configuration Release
