# ============================================================
# YiPix 安全部署脚本 (Windows PowerShell)
# ============================================================
# 适配低配服务器 (2GB RAM):
#   - 分批启动服务，避免启动峰值 OOM
#   - 所有服务使用统一 Dockerfile，只 restore 一次
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
    # ============================================================
    # [1/5] 构建所有服务镜像
    # ============================================================
    Write-Host ""
    Write-Host "[1/5] 构建所有服务镜像 (共享 build stage，不会 OOM)..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"
    docker compose -f $ComposeFile build
    if ($LASTEXITCODE -ne 0) { throw "构建失败" }

    # ============================================================
    # [2/5] 启动基础设施
    # ============================================================
    Write-Host ""
    Write-Host "[2/5] 启动基础设施..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"
    docker compose -f $ComposeFile up -d postgres redis rabbitmq
    if ($LASTEXITCODE -ne 0) { throw "基础设施启动失败" }

    Write-Host ""
    Write-Host "等待基础设施就绪..."
    Start-Sleep -Seconds 10

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

    # ============================================================
    # [3/5] 分批启动应用服务
    # ============================================================
    Write-Host ""
    Write-Host "[3/5] 分批启动应用服务..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"

    # 批次 1: 核心认证 + 用户
    Write-Host "  批次 1/3: auth, user, subscription..."
    docker compose -f $ComposeFile up -d --no-build auth-service user-service subscription-service
    if ($LASTEXITCODE -ne 0) { throw "批次 1 启动失败" }
    Start-Sleep -Seconds 15

    # 批次 2: 业务服务
    Write-Host "  批次 2/3: payment, download, product, analytics, task, file..."
    docker compose -f $ComposeFile up -d --no-build payment-service download-service product-service analytics-service task-service file-service
    if ($LASTEXITCODE -ne 0) { throw "批次 2 启动失败" }
    Start-Sleep -Seconds 15

    # 批次 3: 后台 Worker
    Write-Host "  批次 3/3: workers..."
    docker compose -f $ComposeFile up -d --no-build ai-worker webhook-worker analytics-worker
    if ($LASTEXITCODE -ne 0) { throw "批次 3 启动失败" }
    Start-Sleep -Seconds 5

    # ============================================================
    # [4/5] 启动 Nginx (仅 prod)
    # ============================================================
    if ($Mode -eq "prod") {
        Write-Host ""
        Write-Host "[4/5] 启动 Nginx..." -ForegroundColor Yellow
        Write-Host "----------------------------------------------"
        docker compose -f $ComposeFile up -d --no-build nginx
        if ($LASTEXITCODE -ne 0) { throw "Nginx 启动失败" }
        Start-Sleep -Seconds 3
    } else {
        Write-Host ""
        Write-Host "[4/5] 跳过 Nginx (开发模式)" -ForegroundColor DarkGray
    }

    # ============================================================
    # [5/5] 检查服务状态
    # ============================================================
    Write-Host ""
    Write-Host "[5/5] 检查服务状态..." -ForegroundColor Yellow
    Write-Host "----------------------------------------------"
    docker compose -f $ComposeFile ps

    Write-Host ""
    Write-Host "内存使用：" -ForegroundColor Yellow
    docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}"

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
