# YiPix - 启动所有服务
# 使用方式: 在项目根目录运行 .\start-all.ps1
# 停止所有: 按 Ctrl+C 或运行 .\stop-all.ps1

$rootDir = $PSScriptRoot

$services = @(
    "src/Services/AuthService",
    "src/Services/UserService",
    "src/Services/SubscriptionService",
    "src/Services/PaymentService",
    "src/Services/DownloadService",
    "src/Services/ProductService",
    "src/Services/AnalyticsService",
    "src/Services/TaskService",
    "src/Services/FileService",
    "src/Workers/AIWorker",
    "src/Workers/WebhookWorker",
    "src/Workers/AnalyticsWorker"
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  YiPix Backend - Starting All Services" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# 检查 Docker 基础设施
$dockerRunning = docker compose -f docker/docker-compose.infra.yml ps --format json 2>$null
if (-not $dockerRunning) {
    Write-Host "[!] Docker infrastructure not running. Starting..." -ForegroundColor Yellow
    docker compose -f docker/docker-compose.infra.yml up -d
    Write-Host "[OK] Infrastructure started." -ForegroundColor Green
    Start-Sleep -Seconds 5
}

$jobs = @()

foreach ($svc in $services) {
    $name = Split-Path $svc -Leaf
    Write-Host "[>] Starting $name ..." -ForegroundColor Yellow
    $job = Start-Job -Name $name -ScriptBlock {
        param($root, $project)
        Set-Location $root
        dotnet run --project $project --no-build 2>&1
    } -ArgumentList $rootDir, $svc
    $jobs += $job
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  All 12 services starting in background!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Commands:" -ForegroundColor Cyan
Write-Host "  Get-Job              # View all jobs" -ForegroundColor Gray
Write-Host "  Receive-Job -Name AuthService  # View service output" -ForegroundColor Gray
Write-Host "  Stop-Job *; Remove-Job *       # Stop all services" -ForegroundColor Gray
Write-Host ""

# 等待并显示输出
Write-Host "Press Ctrl+C to stop all services." -ForegroundColor Yellow
Write-Host ""

try {
    while ($true) {
        foreach ($job in $jobs) {
            $output = Receive-Job -Job $job -ErrorAction SilentlyContinue
            if ($output) {
                foreach ($line in $output) {
                    Write-Host "[$($job.Name)] $line"
                }
            }
        }
        Start-Sleep -Milliseconds 500
    }
} finally {
    Write-Host ""
    Write-Host "Stopping all services..." -ForegroundColor Yellow
    Stop-Job *
    Remove-Job *
    Write-Host "All services stopped." -ForegroundColor Green
}
