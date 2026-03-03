# ============================================================
# YiPix 安全部署脚本 (Windows PowerShell)
# ============================================================
# 先构建统一镜像，再启动所有服务
# 避免 docker compose up --build 并行构建导致 OOM
#
# 用法:
#   .\deploy.ps1              # 生产部署
#   .\deploy.ps1 -Mode dev    # 开发部署
# ============================================================

param(
    [ValidateSet("prod", "dev")]
    [string]$Mode = "prod"
)

$ErrorActionPreference = "Stop"
Push-Location $PSScriptRoot

$ComposeFile = "docker-compose.prod.yml"
if ($Mode -eq "dev") {
    $ComposeFile = "docker-compose.yml"
    Write-Host "=== YiPix 开发环境部署 ===" -ForegroundColor Cyan
} else {
    Write-Host "=== YiPix 生产环境部署 ===" -ForegroundColor Cyan
}

try {
    Write-Host ""
    Write-Host "[1/4] 构建统一镜像 (单进程，不会 OOM)..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"

    # 关键：先只构建一个服务。
    # 所有服务共享同一个 Dockerfile.unified 的 build stage，
    # Docker BuildKit 缓存后，后续服务构建瞬间完成。
    docker compose -f $ComposeFile build auth-service
    if ($LASTEXITCODE -ne 0) { throw "构建失败" }

    Write-Host ""
    Write-Host "[2/4] 启动基础设施..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"
    docker compose -f $ComposeFile up -d postgres redis rabbitmq
    if ($LASTEXITCODE -ne 0) { throw "基础设施启动失败" }

    Write-Host ""
    Write-Host "等待基础设施就绪..."
    Start-Sleep -Seconds 10

    # 等待 PostgreSQL
    $maxRetries = 30
    $retry = 0
    do {
        $retry++
        $result = docker compose -f $ComposeFile exec -T postgres pg_isready -U postgres 2>&1
        if ($LASTEXITCODE -eq 0) { break }
        Write-Host "  等待 PostgreSQL... ($retry/$maxRetries)"
        Start-Sleep -Seconds 3
    } while ($retry -lt $maxRetries)
    if ($retry -ge $maxRetries) { throw "PostgreSQL 启动超时" }
    Write-Host "  PostgreSQL OK" -ForegroundColor Green

    # 等待 RabbitMQ
    $retry = 0
    do {
        $retry++
        $result = docker compose -f $ComposeFile exec -T rabbitmq rabbitmq-diagnostics -q ping 2>&1
        if ($LASTEXITCODE -eq 0) { break }
        Write-Host "  等待 RabbitMQ... ($retry/$maxRetries)"
        Start-Sleep -Seconds 3
    } while ($retry -lt $maxRetries)
    if ($retry -ge $maxRetries) { throw "RabbitMQ 启动超时" }
    Write-Host "  RabbitMQ OK" -ForegroundColor Green

    Write-Host ""
    Write-Host "[3/4] 启动应用服务 (镜像已缓存，不会重新构建)..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"
    docker compose -f $ComposeFile up -d --no-build
    if ($LASTEXITCODE -ne 0) { throw "应用服务启动失败" }

    Write-Host ""
    Write-Host "[4/4] 检查服务状态..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"
    Start-Sleep -Seconds 5
    docker compose -f $ComposeFile ps

    Write-Host ""
    Write-Host "=== 部署完成 ===" -ForegroundColor Green
    Write-Host "提示: 使用 'docker compose -f $ComposeFile logs -f' 查看日志"
}
catch {
    Write-Host "部署失败: $_" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
