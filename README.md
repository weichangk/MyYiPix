# YiPix Backend

> 企业级图像处理 SaaS 平台后端系统 — 官网 + Admin 后台 + 桌面客户端一体化架构

## 目录

- [项目简介](#项目简介)
- [技术栈](#技术栈)
- [系统架构](#系统架构)
- [解决方案结构](#解决方案结构)
- [服务说明](#服务说明)
- [快速开始](#快速开始)
- [配置说明](#配置说明)
- [API 端点](#api-端点)
- [事件驱动架构](#事件驱动架构)
- [数据库设计](#数据库设计)
- [部署](#部署)
- [开发路线](#开发路线)

---

## 项目简介

YiPix 是一款图像处理软件，核心功能包括图片转换、压缩、裁剪、AI 增强和批量处理。后端系统采用 .NET 9 微服务架构，统一支撑官网、Admin 管理后台和桌面客户端三端业务。

**核心能力：**

- 🌍 全球用户访问，多端统一账号体系
- 💳 PayPal 订阅支付（月 / 年 / 终身）
- 📦 CDN 驱动的安装包下载分发
- 🤖 AI 图像增强异步任务系统
- 📊 用户行为与业务数据统计分析

## 技术栈

| 层级 | 技术 |
|------|------|
| **运行时** | .NET 9, ASP.NET Core Web API |
| **ORM** | Entity Framework Core 9 |
| **数据库** | PostgreSQL 16 |
| **缓存** | Redis 7 |
| **消息队列** | RabbitMQ 3 |
| **对象存储** | S3 / Azure Blob / MinIO（可切换） |
| **认证** | JWT Bearer Token + Refresh Token |
| **日志** | Serilog（控制台 + 文件滚动） |
| **容器化** | Docker + Docker Compose |

## 系统架构

```
┌─────────────────────────────────────────────────────┐
│                     Clients                         │
│   Website Frontend  │  Admin Frontend  │  Desktop   │
└──────────┬──────────┴────────┬─────────┴──────┬─────┘
           │                   │                │
           ▼                   ▼                ▼
┌─────────────────────────────────────────────────────┐
│                   CDN Layer                         │
└──────────────────────┬──────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────┐
│              Backend API Gateway (Future)            │
└──────────────────────┬──────────────────────────────┘
                       ▼
┌─────────────────────────────────────────────────────┐
│                 Microservices                        │
│                                                     │
│  AuthService ─── UserService ─── SubscriptionService│
│  PaymentService ─ DownloadService ─ ProductService  │
│  AnalyticsService ─ TaskService ─── FileService     │
└──────────┬───────────┬──────────────────────────────┘
           │           │
           ▼           ▼
┌──────────────┐  ┌──────────────────────────────────┐
│   Workers    │  │         Infrastructure            │
│  AIWorker    │  │  PostgreSQL │ Redis │ RabbitMQ    │
│  Webhook     │  │  Object Storage (S3/MinIO)        │
│  Analytics   │  │  PayPal API                       │
└──────────────┘  └──────────────────────────────────┘
```

## 解决方案结构

```
YiPix.sln
│
├── src/
│   ├── BuildingBlocks/                    # 基础设施（共享库）
│   │   ├── Common/                        # 基类、异常、中间件、通用模型
│   │   │   ├── Domain/                    #   BaseEntity, AggregateRoot, IDomainEvent
│   │   │   ├── Exceptions/               #   YiPixException, NotFoundException, ...
│   │   │   ├── Interfaces/               #   IRepository<T>, IUnitOfWork
│   │   │   ├── Middleware/               #   GlobalExceptionMiddleware
│   │   │   └── Models/                   #   ApiResponse<T>, PagedResult<T>
│   │   ├── Contracts/                     # 跨服务共享契约
│   │   │   ├── Auth/                     #   RegisterRequest, LoginRequest, AuthResponse
│   │   │   ├── Events/                   #   IntegrationEvent 定义（全部集成事件）
│   │   │   ├── Payment/                  #   PaymentDto, WebhookPayload
│   │   │   └── Subscription/            #   SubscriptionDto, SubscriptionPlan 枚举
│   │   ├── EventBus/                      # RabbitMQ 事件总线
│   │   │   ├── Abstractions/             #   IEventBus, IIntegrationEventHandler<T>
│   │   │   └── RabbitMQ/                 #   RabbitMQEventBus 实现
│   │   ├── Logging/                       # Serilog 配置扩展
│   │   └── Security/                      # JWT 认证配置
│   │       ├── JwtSettings.cs
│   │       ├── JwtTokenService.cs
│   │       └── SecurityServiceExtensions.cs
│   │
│   ├── Services/                          # 9 个微服务
│   │   ├── AuthService/                   # 端口 5001
│   │   ├── UserService/                   # 端口 5002
│   │   ├── SubscriptionService/           # 端口 5003
│   │   ├── PaymentService/                # 端口 5004
│   │   ├── DownloadService/               # 端口 5005
│   │   ├── ProductService/                # 端口 5006
│   │   ├── AnalyticsService/              # 端口 5007
│   │   ├── TaskService/                   # 端口 5008
│   │   └── FileService/                   # 端口 5009
│   │
│   └── Workers/                           # 3 个后台 Worker
│       ├── AIWorker/                      # AI 图像处理任务消费
│       ├── WebhookWorker/                 # PayPal Webhook 异步处理
│       └── AnalyticsWorker/               # 统计数据聚合
│
└── docker/
    ├── docker-compose.yml                 # 全量部署
    ├── docker-compose.infra.yml           # 仅基础设施（开发用）
    └── Dockerfile.*                       # 各服务 Dockerfile
```

每个微服务内部遵循统一的分层结构：

```
ServiceName/
├── Domain/Entities/         # 领域实体
├── Infrastructure/Data/     # DbContext + Repository
├── Application/             # 应用服务层 + DTO
├── Controllers/             # REST API 控制器
├── Program.cs               # 启动配置
└── appsettings.*.json       # 配置文件
```

## 服务说明

### 核心服务（第一阶段）

| 服务 | 职责 | 关键端点 |
|------|------|----------|
| **AuthService** | 注册、登录、JWT/RefreshToken、多端登录 | `POST /api/auth/register` `POST /api/auth/login` `POST /api/auth/refresh` |
| **UserService** | 用户资料 CRUD、用户行为记录 | `GET /api/users/{id}` `PUT /api/users/{id}` |
| **SubscriptionService** | 订阅生命周期、状态机、权限判断 | `GET /api/subscriptions/user/{id}/status` `POST /api/subscriptions` |
| **PaymentService** | PayPal 支付集成、Webhook 幂等处理 | `POST /api/payments` `POST /api/payments/webhook` |
| **DownloadService** | 安装包版本管理、CDN Signed URL | `GET /api/downloads/latest/{platform}` `GET /api/downloads/link/{version}/{platform}` |

### 扩展服务（第二阶段）

| 服务 | 职责 |
|------|------|
| **ProductService** | 产品信息管理、定价方案、版本发布 |
| **AnalyticsService** | 事件追踪、每日统计聚合、管理仪表板 |
| **TaskService** | 图片处理任务调度、进度跟踪、状态管理 |
| **FileService** | 文件上传下载、对象存储抽象（Local/S3/Azure） |

### Worker 服务

| Worker | 职责 |
|--------|------|
| **AIWorker** | 消费 `TaskCreatedEvent`，执行 AI 图像增强 |
| **WebhookWorker** | 消费 `PaymentCompletedEvent`，激活/续期订阅 |
| **AnalyticsWorker** | 消费 `DownloadStartedEvent`，执行统计聚合 |

## 快速开始

### 前置要求

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) （用于运行基础设施）
- [Git](https://git-scm.com/)

### 1. 启动基础设施

```bash
cd docker
docker-compose -f docker-compose.infra.yml up -d
```

这将启动：
- **PostgreSQL** — `localhost:5432`
- **Redis** — `localhost:6379`
- **RabbitMQ** — `localhost:5672`（管理界面: `localhost:15672`，用户名/密码: guest/guest）

### 2. 编译项目

```bash
dotnet build YiPix.sln
```

### 3. 运行单个服务

```bash
# 运行 AuthService
dotnet run --project src/Services/AuthService

# 运行 UserService
dotnet run --project src/Services/UserService
```

首次运行会自动执行数据库迁移（开发环境）。

### 4. 全量 Docker 部署

```bash
cd docker
docker-compose up --build -d
```

## 配置说明

每个服务通过 `appsettings.Development.json` 配置，关键配置项：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=yipix;Username=postgres;Password=postgres",
    "RabbitMQ": "amqp://guest:guest@localhost:5672",
    "Redis": "localhost:6379"
  },
  "JwtSettings": {
    "Secret": "YiPix-Super-Secret-Key-Must-Be-At-Least-32-Characters-Long!",
    "Issuer": "YiPix",
    "Audience": "YiPixClients",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  }
}
```

> ⚠️ 生产环境请通过环境变量或密钥管理服务注入敏感配置，切勿将密钥提交到代码仓库。

## API 端点

所有服务统一使用 `ApiResponse<T>` 封装响应：

```json
{
  "success": true,
  "message": "Login successful.",
  "data": { ... },
  "errors": null
}
```

### 认证

大部分 API 需要在请求头携带 JWT：

```
Authorization: Bearer <access_token>
```

### OpenAPI 文档

每个服务在开发模式下暴露 OpenAPI 端点：

```
GET /openapi/v1.json
```

### Docker Compose 端口映射

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
| PostgreSQL | 5432 |
| Redis | 6379 |
| RabbitMQ | 5672 / 15672 |

## 事件驱动架构

服务间通过 RabbitMQ 发布/订阅集成事件实现松耦合通信：

```
┌──────────┐   UserCreatedEvent   ┌──────────────┐
│AuthService├────────────────────►│ UserService   │
└──────────┘                      └──────────────┘

┌──────────────┐  PaymentCompletedEvent  ┌───────────────────┐
│PaymentService├────────────────────────►│SubscriptionService│
└──────────────┘                         └───────────────────┘
         │
         │  PaymentCompletedEvent
         ▼
┌──────────────┐
│WebhookWorker │
└──────────────┘

┌──────────────┐  DownloadStartedEvent  ┌─────────────────┐
│DownloadService├──────────────────────►│AnalyticsWorker  │
└──────────────┘                        └─────────────────┘

┌───────────┐  TaskCreatedEvent  ┌──────────┐
│TaskService├───────────────────►│ AIWorker │
└───────────┘                    └──────────┘
```

### 集成事件清单

| 事件 | 发布者 | 消费者 |
|------|--------|--------|
| `UserCreatedEvent` | AuthService | UserService |
| `UserLoggedInEvent` | AuthService | AnalyticsWorker |
| `SubscriptionActivatedEvent` | SubscriptionService | — |
| `SubscriptionCancelledEvent` | SubscriptionService | — |
| `PaymentCompletedEvent` | PaymentService | WebhookWorker |
| `PaymentFailedEvent` | PaymentService | — |
| `DownloadStartedEvent` | DownloadService | AnalyticsWorker |
| `TaskCreatedEvent` | TaskService | AIWorker |
| `TaskCompletedEvent` | TaskService | — |

## 数据库设计

采用**单数据库 + 多 Schema** 策略，为未来按服务拆库做准备：

| Schema | 服务 | 核心表 |
|--------|------|--------|
| `auth` | AuthService | `Users`, `RefreshTokens` |
| `user` | UserService | `UserProfiles`, `UserActivities` |
| `subscription` | SubscriptionService | `Subscriptions`, `SubscriptionHistories` |
| `payment` | PaymentService | `Payments`, `WebhookLogs` |
| `download` | DownloadService | `Releases`, `DownloadRecords` |
| `product` | ProductService | `Products`, `PricingPlans` |
| `analytics` | AnalyticsService | `Events`, `DailyStats` |
| `task` | TaskService | `Tasks` |
| `file` | FileService | `Files` |

## 部署

### 开发环境

```bash
# 仅启动基础设施
docker-compose -f docker/docker-compose.infra.yml up -d

# 各服务本地启动
dotnet run --project src/Services/AuthService
```

### 生产环境

```bash
# 全量 Docker 部署
docker-compose -f docker/docker-compose.yml up --build -d
```

### 未来规划

- Docker + Kubernetes 编排
- Helm Charts 部署模板
- CI/CD Pipeline（GitHub Actions）
- 多环境配置管理

## 开发路线

### 第一阶段（核心上线） ✅ 已搭建
- [x] AuthService — 注册、登录、JWT
- [x] UserService — 用户资料管理
- [x] SubscriptionService — 订阅生命周期
- [x] PaymentService — PayPal 支付集成
- [x] DownloadService — 安装包下载分发

### 第二阶段（功能扩展） ✅ 已搭建
- [x] ProductService — 产品与定价管理
- [x] AnalyticsService — 数据统计分析
- [x] TaskService — 任务调度管理

### 第三阶段（高级功能）
- [ ] AI 图像处理引擎集成
- [ ] 通知系统（邮件 / 推送）
- [ ] API Gateway（Ocelot / YARP）
- [ ] 分布式缓存策略优化
- [ ] Kubernetes 部署
- [ ] 多租户 SaaS 架构

## 核心设计原则

- **API 无状态** — 所有状态通过 JWT 和数据库管理
- **订阅状态中心化** — SubscriptionService 为唯一权限判断源
- **耗时操作异步化** — 通过 RabbitMQ + Worker 处理
- **下载走 CDN** — DownloadService 仅生成 Signed URL
- **Webhook 幂等** — 通过 WebhookLog 表去重，确保不重复处理
- **全局异常处理** — 统一 `ApiResponse` 格式，中间件拦截

## License

Private — All rights reserved.
