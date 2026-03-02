# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 常用开发任务

### 构建项目
```bash
dotnet build YiPix.sln
```

### 启动基础设施 (如数据库、消息队列等)
本地开发推荐仅启动基础设施（PostgreSQL、Redis、RabbitMQ）：
```bash
cd docker
docker-compose -f docker-compose.infra.yml up -d
```

查看日志：
```bash
docker-compose -f docker-compose.infra.yml logs -f
```

停止基础设施：
```bash
docker-compose -f docker-compose.infra.yml down
```

### 运行单个服务
以 `AuthService` 服务为例：
```bash
dotnet run --project src/Services/AuthService
```

其他服务路径位于 `src/Services/<ServiceName>`。

### 全量部署（包括所有微服务和 Worker）
```bash
cd docker
docker-compose up --build -d
```

停止全量部署：
```bash
docker-compose down
```

## 高层代码架构

本仓库实现了一个企业级图片处理 SaaS 平台 YiPix，基于 .NET 9 微服务架构。代码组织围绕以下模块：

### Building Blocks
- `src/BuildingBlocks/Common`：存放基础通用类、异常、中间件等。
- `src/BuildingBlocks/EventBus`：基于 RabbitMQ 的事件总线封装。
- `src/BuildingBlocks/Logging`：日志的通用配置。
- `src/BuildingBlocks/Security`：安全相关的配置与实现（如 JWT 生成和验证）。

### 微服务
服务划分为核心服务和扩展服务：
- 核心服务：
  - `AuthService`：包含注册、登录、JWT Token 管理等功能。
  - `UserService`：用户数据 CRUD、行为日志等。
  - `SubscriptionService`：订阅的生命周期管理。
  - `PaymentService`：管理支付集成与 Webhook 处理。
  - `DownloadService`：提供安装包的下载链接生成与 CDN 分发。
- 扩展服务：
  - `ProductService`：管理产品信息。
  - `AnalyticsService`：统计用户与业务数据。
  - `TaskService`：调度并跟踪图片处理任务。
  - `FileService`：负责文件存储（支持 MinIO/S3）。

服务实现路径位于 `src/Services/<ServiceName>`。

每个服务遵循统一的分层结构：
- `Domain/Entities/`：领域层，存放核心领域逻辑。
- `Infrastructure/Data/`：数据层，存放 `DbContext` 和 `Repository` 实现。
- `Application/`：应用服务层，包含 DTOs 和核心业务逻辑。
- `Controllers/`：控制器层，提供 REST API。

### Worker 服务
处理异步任务的后台服务：
- `AIWorker`：处理图片增强任务。
- `WebhookWorker`：处理支付 Webhook。
- `AnalyticsWorker`：聚合统计数据。

### 事件驱动架构
通过事件总线（RabbitMQ）进行服务间松耦合通信。
常见事件包括：
- `UserCreatedEvent`：用户注册。
- `PaymentCompletedEvent`：支付完成。
- `TaskCreatedEvent`：图片处理任务创建。

订阅与发布关系可在 README 的事件驱动架构部分查看。

## 配置说明

所有服务的配置存放于 `appsettings.*.json`，开发环境建议使用 `appsettings.Development.json`。
- 数据库连接：
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=yipix;Username=postgres;Password=postgres"
  }
  ```
- JWT 配置：
  ```json
  "JwtSettings": {
    "Secret": "YiPix-Super-Secret-Key-Must-Be-At-Least-32-Characters-Long!",
    "Issuer": "YiPix",
    "Audience": "YiPixClients",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  }
  ```

## 部署环境
- 开发环境：仅启动基础设施，本地运行服务代码。
- 生产环境：使用全量 Docker Compose 部署。

更多部署命令详见 `docker/README.md` 文件。

## 其他注意事项
- 初次运行迁移数据库：服务启动时会自动执行数据库迁移。
- 确保敏感信息未提交至代码仓库，可通过环境变量注入生产配置。
