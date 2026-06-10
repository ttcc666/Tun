# 启动完整的 Tun 系统（生产配置）
# 使用方式: .\start-all-production.ps1

Write-Host "=== 启动 Tun 完整系统 (生产配置) ===`n" -ForegroundColor Cyan

$projectRoot = $PSScriptRoot
$logsDir = Join-Path $projectRoot "logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

$sampleLog = Join-Path $logsDir "sample-app.log"
$serverLog = Join-Path $logsDir "server.log"
$clientLog = Join-Path $logsDir "client.log"

function Test-HttpEndpoint {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Uri,

        [int] $Retries = 10,

        [int] $DelaySeconds = 3
    )

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 3
            Write-Host "   ✅ ${Name}: $($response.StatusCode) $Uri" -ForegroundColor Green
            return
        }
        catch {
            if ($attempt -eq $Retries) {
                Write-Host "   ❌ ${Name}: $Uri 未就绪 - $($_.Exception.Message)" -ForegroundColor Red
                return
            }

            Start-Sleep -Seconds $DelaySeconds
        }
    }
}

# 启动 Sample App
Write-Host "1️⃣ 启动 Sample App..." -ForegroundColor Yellow
Start-Process pwsh -ArgumentList @(
    "-NoProfile",
    "-Command",
    "cd '$projectRoot'; dotnet run --project 'samples\Tun.SampleApp\Tun.SampleApp.csproj' *>&1 | Tee-Object -FilePath '$sampleLog' -Append"
) -WindowStyle Hidden

Start-Sleep -Seconds 2

# 启动 Server
Write-Host "2️⃣ 启动 Tun.Server..." -ForegroundColor Yellow
Start-Process pwsh -ArgumentList @(
    "-NoProfile",
    "-Command",
    "cd '$projectRoot'; .\start-server-production.ps1"
) -WindowStyle Hidden

Start-Sleep -Seconds 5

# 启动 Client
Write-Host "3️⃣ 启动 Tun.Client..." -ForegroundColor Yellow
$env:Tun__ClientId = "dev-client"
$env:Tun__Token = "dev-token"  # ⚠️ 与 Server 保持一致
$env:Tun__ServerUrl = "http://127.0.0.1:8081"
$env:Tun__ManagementUrl = "http://127.0.0.1:8080"
$env:Tun__UseServerConfig = "true"
$env:Tun__RequireServerConfig = "true"

Start-Process pwsh -ArgumentList @(
    "-NoProfile",
    "-Command",
    "cd '$projectRoot'; `$env:Tun__ClientId='dev-client'; `$env:Tun__Token='dev-token'; `$env:Tun__ServerUrl='http://127.0.0.1:8081'; `$env:Tun__ManagementUrl='http://127.0.0.1:8080'; `$env:Tun__UseServerConfig='true'; `$env:Tun__RequireServerConfig='true'; dotnet run --project 'src\Tun.Client\Tun.Client.csproj' *>&1 | Tee-Object -FilePath '$clientLog' -Append"
) -WindowStyle Hidden

Start-Sleep -Seconds 5

Write-Host "`n✅ 所有服务已在后台启动`n" -ForegroundColor Green
Write-Host "🩺 本地健康检查：" -ForegroundColor Cyan
Test-HttpEndpoint -Name "Sample App" -Uri "http://localhost:5000/health"
Test-HttpEndpoint -Name "Tun.Server" -Uri "http://127.0.0.1:8080/healthz"

Write-Host "📋 访问信息：" -ForegroundColor Cyan
Write-Host "   本地 Dashboard: http://127.0.0.1:8080/dashboard/"
Write-Host "   公网 Dashboard: https://ttcc0313.ggff.net/dashboard/"
Write-Host "   示例 Tunnel: https://sample.ttcc0313.ggff.net/health"
Write-Host ""
Write-Host "📄 日志：" -ForegroundColor Cyan
Write-Host "   Sample App: $sampleLog"
Write-Host "   Server:     $serverLog"
Write-Host "   Client:     $clientLog"
Write-Host ""
Write-Host "⚠️  记得启动 cloudflared tunnel！" -ForegroundColor Yellow
Write-Host "   .\start-cloudflared.ps1"
