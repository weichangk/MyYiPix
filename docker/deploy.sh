#!/bin/bash
# ============================================================
# YiPix 安全部署脚本 (Linux/macOS)
# ============================================================
# 先构建统一镜像，再启动所有服务
# 避免 docker compose up --build 并行构建导致 OOM
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

echo ""
echo "[1/4] 构建统一镜像 (单进程，不会 OOM)..."
echo "----------------------------------------------"

# 关键：先只构建一个服务的镜像。
# 因为所有服务共享同一个 Dockerfile.unified 的 build stage，
# Docker BuildKit 会缓存 build stage，后续服务构建瞬间完成。
docker compose -f "$COMPOSE_FILE" build auth-service

echo ""
echo "[2/4] 基础设施启动..."
echo "----------------------------------------------"
docker compose -f "$COMPOSE_FILE" up -d postgres redis rabbitmq

echo ""
echo "等待基础设施就绪..."
sleep 10

# 等待 postgres 健康
until docker compose -f "$COMPOSE_FILE" exec -T postgres pg_isready -U postgres > /dev/null 2>&1; do
    echo "  等待 PostgreSQL..."
    sleep 3
done
echo "  PostgreSQL ✓"

# 等待 RabbitMQ 健康
until docker compose -f "$COMPOSE_FILE" exec -T rabbitmq rabbitmq-diagnostics -q ping > /dev/null 2>&1; do
    echo "  等待 RabbitMQ..."
    sleep 3
done
echo "  RabbitMQ ✓"

echo ""
echo "[3/4] 启动应用服务 (镜像已缓存，不会重新构建)..."
echo "----------------------------------------------"
docker compose -f "$COMPOSE_FILE" up -d --no-build

echo ""
echo "[4/4] 检查服务状态..."
echo "----------------------------------------------"
sleep 5
docker compose -f "$COMPOSE_FILE" ps

echo ""
echo "=== 部署完成 ==="
echo "提示: 使用 'docker compose -f $COMPOSE_FILE logs -f' 查看日志"
