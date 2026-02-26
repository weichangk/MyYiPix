# Docker 部署说明

## 两个 Compose 文件的区别

| | `docker-compose.infra.yml` | `docker-compose.yml` |
|---|---|---|
| **用途** | 本地开发——只启动基础设施 | 全量部署——基础设施 + 所有微服务 + Worker |
| **包含内容** | PostgreSQL、Redis、RabbitMQ（3 个容器） | PostgreSQL、Redis、RabbitMQ + 9 个 API 服务 + 3 个 Worker（共 15 个容器） |
| **微服务运行方式** | 不包含微服务，由你本地 `dotnet run` 启动 | 每个微服务都构建 Docker 镜像并在容器中运行 |
| **典型场景** | 日常开发调试（改代码 → 热重载 → 调试） | 集成测试、演示、或生产部署 |

---

## 日常开发（推荐）

只拉起数据库、缓存、消息队列，微服务本地运行：

```bash
# 启动基础设施（后台运行）
docker compose -f docker-compose.infra.yml up -d

# 本地启动你要开发的服务
dotnet run --project ../src/Services/AuthService
```

### 基础设施访问地址

| 服务 | 地址 |
|------|------|
| PostgreSQL | `localhost:5432`（用户: postgres / 密码: postgres / 数据库: yipix） |
| Redis | `localhost:6379` |
| RabbitMQ | `localhost:5672`（AMQP） |
| RabbitMQ 管理界面 | `http://localhost:15672`（用户: guest / 密码: guest） |

### 常用命令

```bash
# 查看容器运行状态
docker compose -f docker-compose.infra.yml ps

# 查看实时日志
docker compose -f docker-compose.infra.yml logs -f

# 查看某个服务的日志
docker compose -f docker-compose.infra.yml logs -f postgres

# 停止并移除容器（数据保留）
docker compose -f docker-compose.infra.yml down

# 停止并删除容器 + 数据卷（清空所有数据）
docker compose -f docker-compose.infra.yml down -v
```

---

## 全量部署

一键启动整个系统（基础设施 + 所有微服务 + Worker）：

```bash
# 构建镜像并后台启动
docker compose up --build -d

# 查看所有容器状态
docker compose ps

# 查看实时日志
docker compose logs -f

# 停止所有服务
docker compose down
```

### 服务端口映射

| 服务 | 端口 |
|------|------|
| AuthService | 5001 |
| UserService | 5002 |
| SubscriptionService | 5003 |
| PaymentService | 5004 |
| DownloadService | 5005 |
| ProductService | 5006 |
| AnalyticsService | 5007 |
| TaskService | 5008 |
| FileService | 5009 |

---

## 命令速查

| 操作 | 命令 |
|------|------|
| 启动基础设施 | `docker compose -f docker-compose.infra.yml up -d` |
| 停止基础设施 | `docker compose -f docker-compose.infra.yml down` |
| 清空数据重建 | `docker compose -f docker-compose.infra.yml down -v` |
| 全量启动 | `docker compose up --build -d` |
| 全量停止 | `docker compose down` |
| 查看状态 | `docker compose ps` |
| 查看日志 | `docker compose logs -f` |
| 重启某服务 | `docker compose restart <service-name>` |
| 进入容器 | `docker compose exec <service-name> sh` |
| 进入 PostgreSQL | `docker compose exec postgres psql -U postgres -d yipix` |
| 进入 Redis CLI | `docker compose exec redis redis-cli` |
