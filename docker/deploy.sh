#!/bin/bash
# ============================================================
# YiPix 安全部署脚本 (Linux/macOS)
# ============================================================
# 适配低配服务器 (2GB RAM):
#   - 自动创建 Swap (如不存在)
#   - 分批启动服务，避免启动峰值 OOM
#   - 所有服务使用统一 Dockerfile，只 restore 一次
#
# 用法:
#   chmod +x deploy.sh
#   ./deploy.sh              # 生产部署
#   ./deploy.sh dev          # 开发部署
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

MODE="${1:-prod}"
COMPOSE_FILE="docker-compose.prod.yml"

if [ "$MODE" = "dev" ]; then
    COMPOSE_FILE="docker-compose.yml"
    echo "=== YiPix 开发环境部署 ==="
else
    echo "=== YiPix 生产环境部署 ==="
fi

# ============================================================
# [0/5] 检查并创建 Swap (低内存服务器必需)
# ============================================================
echo ""
echo "[0/5] 检查 Swap..."
echo "----------------------------------------------"

TOTAL_MEM_MB=$(free -m | awk '/^Mem:/{print $2}')
SWAP_SIZE_MB=$(free -m | awk '/^Swap:/{print $2}')

echo "  物理内存: ${TOTAL_MEM_MB}MB, Swap: ${SWAP_SIZE_MB}MB"

if [ "$SWAP_SIZE_MB" -lt 1024 ]; then
    if [ -f /swapfile ]; then
        echo "  /swapfile 已存在但 Swap 未启用，尝试启用..."
        sudo swapon /swapfile 2>/dev/null || true
    else
        SWAP_TARGET=4096  # 4GB swap
        echo "  Swap 不足，创建 ${SWAP_TARGET}MB Swap 文件..."
        sudo fallocate -l ${SWAP_TARGET}M /swapfile 2>/dev/null || sudo dd if=/dev/zero of=/swapfile bs=1M count=$SWAP_TARGET status=progress
        sudo chmod 600 /swapfile
        sudo mkswap /swapfile
        sudo swapon /swapfile
        # 持久化
        if ! grep -q '/swapfile' /etc/fstab; then
            echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
        fi
        echo "  Swap 已创建并启用"
    fi
    # 优化 swappiness: 低内存服务器积极使用 swap
    sudo sysctl vm.swappiness=60 2>/dev/null || true
else
    echo "  Swap 充足 ✓"
fi

# ============================================================
# [1/5] 构建所有服务镜像
# ============================================================
echo ""
echo "[1/5] 构建所有服务镜像 (共享 build stage，不会 OOM)..."
echo "----------------------------------------------"
docker compose -f "$COMPOSE_FILE" build

# ============================================================
# [2/5] 启动基础设施
# ============================================================
echo ""
echo "[2/5] 启动基础设施..."
echo "----------------------------------------------"
docker compose -f "$COMPOSE_FILE" up -d postgres redis rabbitmq

echo ""
echo "等待基础设施就绪..."
sleep 10

until docker compose -f "$COMPOSE_FILE" exec -T postgres pg_isready -U postgres > /dev/null 2>&1; do
    echo "  等待 PostgreSQL..."
    sleep 3
done
echo "  PostgreSQL ✓"

until docker compose -f "$COMPOSE_FILE" exec -T rabbitmq rabbitmq-diagnostics -q ping > /dev/null 2>&1; do
    echo "  等待 RabbitMQ..."
    sleep 3
done
echo "  RabbitMQ ✓"

# ============================================================
# [3/5] 分批启动核心服务 (避免同时启动 12 个进程)
# ============================================================
echo ""
echo "[3/5] 分批启动应用服务..."
echo "----------------------------------------------"

# 第 1 批：核心认证 + 用户
echo "  批次 1/3: auth, user, subscription..."
docker compose -f "$COMPOSE_FILE" up -d --no-build auth-service user-service subscription-service
sleep 15

# 第 2 批：业务服务
echo "  批次 2/3: payment, download, product, analytics, task, file..."
docker compose -f "$COMPOSE_FILE" up -d --no-build payment-service download-service product-service analytics-service task-service file-service
sleep 15

# 第 3 批：后台 Worker
echo "  批次 3/3: workers..."
docker compose -f "$COMPOSE_FILE" up -d --no-build ai-worker webhook-worker analytics-worker
sleep 5

# ============================================================
# [4/5] 启动 Nginx (仅 prod)
# ============================================================
if [ "$MODE" = "prod" ]; then
    echo ""
    echo "[4/5] 启动 Nginx..."
    echo "----------------------------------------------"
    docker compose -f "$COMPOSE_FILE" up -d --no-build nginx
    sleep 3
else
    echo ""
    echo "[4/5] 跳过 Nginx (开发模式)"
fi

# ============================================================
# [5/5] 检查服务状态
# ============================================================
echo ""
echo "[5/5] 检查服务状态..."
echo "----------------------------------------------"
docker compose -f "$COMPOSE_FILE" ps
echo ""
echo "内存使用："
docker stats --no-stream --format "table {{.Name}}\t{{.MemUsage}}\t{{.MemPerc}}" 2>/dev/null || true

echo ""
echo "=== 部署完成 ==="
echo "提示: 使用 'docker compose -f $COMPOSE_FILE logs -f' 查看日志"
