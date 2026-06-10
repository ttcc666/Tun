# 启动 Cloudflare Tunnel
# 使用方式: .\start-cloudflared.ps1

Write-Host "=== 启动 Cloudflare Tunnel ===`n" -ForegroundColor Cyan

$projectRoot = $PSScriptRoot
$logsDir = Join-Path $projectRoot "logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
$cloudflaredLog = Join-Path $logsDir "cloudflared.log"

$tunnelId = "94d5f197-0a7c-416f-becd-5fb65ad7bb1a"

Write-Host "Tunnel ID: $tunnelId" -ForegroundColor Yellow
Write-Host "配置文件: C:\Users\KGMCW\.cloudflared\config.yml"
Write-Host "日志文件: $cloudflaredLog"
Write-Host ""
Write-Host "路由规则：" -ForegroundColor Cyan
Write-Host "  ✅ ttcc0313.ggff.net → http://127.0.0.1:8080"
Write-Host "  ✅ *.ttcc0313.ggff.net → http://127.0.0.1:8080"
Write-Host ""

try {
    Invoke-WebRequest -Uri "http://127.0.0.1:8080/healthz" -UseBasicParsing -TimeoutSec 3 | Out-Null
    Write-Host "✅ Tun.Server origin 已就绪: http://127.0.0.1:8080" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Tun.Server origin 当前不可达: http://127.0.0.1:8080" -ForegroundColor Yellow
    Write-Host "   这会导致公网域名返回 Cloudflare 502。请先运行 .\start-all-production.ps1 并检查 logs\server.log" -ForegroundColor Yellow
}

Write-Host "`n验证 cloudflared ingress 配置..." -ForegroundColor Cyan
cloudflared tunnel ingress validate
if ($LASTEXITCODE -ne 0) {
    throw "cloudflared ingress 配置验证失败"
}

Write-Host "`n启动 cloudflared..." -ForegroundColor Cyan
cloudflared tunnel run $tunnelId *>&1 | Tee-Object -FilePath $cloudflaredLog -Append
