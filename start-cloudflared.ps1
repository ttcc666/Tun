# 启动 Cloudflare Tunnel
# 使用方式: .\start-cloudflared.ps1

Write-Host "=== 启动 Cloudflare Tunnel ===`n" -ForegroundColor Cyan

$tunnelId = "94d5f197-0a7c-416f-becd-5fb65ad7bb1a"

Write-Host "Tunnel ID: $tunnelId" -ForegroundColor Yellow
Write-Host "配置文件: C:\Users\KGMCW\.cloudflared\config.yml"
Write-Host ""
Write-Host "路由规则：" -ForegroundColor Cyan
Write-Host "  ✅ ttcc0313.ggff.net → http://127.0.0.1:8080"
Write-Host "  ✅ *.ttcc0313.ggff.net → http://127.0.0.1:8080"
Write-Host ""

cloudflared tunnel run $tunnelId
