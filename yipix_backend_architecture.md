# YiPix 企业级后端系统架构设计文档
## （官网 + Admin 后台 + 桌面客户端一体化 SaaS 平台）

# 1. 项目概述

## 1.1 项目背景
YiPix 是一款图像处理软件，核心功能包括：

- 图片转换
- 图片压缩
- 图片裁剪
- AI 图像增强
- 批量处理

系统需要支撑：

- 官网系统（产品展示 + 购买 + 下载）
- Admin 后台系统（管理 + 数据分析）
- 桌面客户端（登录 + 订阅校验 + 云功能）
- 支付订阅系统（PayPal）
- 下载分发系统（CDN）
- AI 强步任务系统

## 1.2 架构目标

### 业务目标
- 支持全球用户访问
- 支持订阅商业模式（月 / 年 / 终身）
- 支持多端统一账号体系
- 支持 SaaS 扩展能力

### 技术目标
- 企业级可扩展架构
- 云原生部署能力
- 高可用高并发支持
- 可平滑升级微服务架构

# 2. 总体架构设计

## 2.1 架构模式
- Modular Monolith（当前阶段）
- 事件驱动架构（EDA）
- 云原生架构设计
- 微服务可演进架构

## 2.2 系统总体拓扑
```
User
 ├ 官网 Website
 ├ Admin 后台
 └ 桌面客户端

        ↓

CDN Layer

        ↓

Frontend Layer
 ├ Website Frontend
 ├ Admin Frontend

        ↓

Backend API Layer (.NET)

        ↓

Core Business Services

        ↓

Infrastructure Layer

        ↓

External Services
 ├ PayPal
 ├ Object Storage
 ├ AI Processing
```

# 3. 技术栈选型

## 后端技术
- .NET 9
- ASP.NET Core Web API
- EF Core

## 数据层
- PostgreSQL
- Redis

## 消息系统
- RabbitMQ

## 存储
- S3 / Azure Blob / MinIO

## 日志与监控
- Serilog
- OpenTelemetry
- Prometheus
- Grafana

# 4. 服务架构设计

## 4.1 服务清单

### AuthService
职责：
- 用户注册登录
- JWT Token
- Refresh Token
- 多端登录支持

### UserService
职责：
- 用户资料管理
- 用户状态管理
- 用户行为数据

### SubscriptionService
职责：
- 订阅生命周期管理
- 订阅状态机
- 权限判断核心服务

### PaymentService
职责：
- PayPal API 调用
- Webhook 接收
- 支付记录管理

### DownloadService
职责：
- 安装包版本管理
- 下载权限控制
- CDN Signed URL 生成

### ProductService
职责：
- 产品信息管理
- 版本发布管理
- Release 管理

### AnalyticsService
职责：
- 下载统计
- 转化统计
- 用户行为统计

### TaskService
职责：
- 图片处理任务管理
- 任务调度
- 状态跟踪

### FileService
职责：
- 文件上传下载
- 对象存储访问

# 5. Worker 服务

### AIWorker
执行 AI 图像任务

### WebhookWorker
处理支付 Webhook 异步任务

### AnalyticsWorker
处理统计数据聚合

# 6. 数据库设计策略

## 当前阶段
单数据库 + 多 Schema：
```
auth
user
subscription
payment
download
analytics
task
```

## 未来阶段
按服务拆库：
```
AuthDB
UserDB
SubscriptionDB
PaymentDB
```

# 7. 支付系统架构（PayPal）

## 支付模型
```
Product → Plan → Subscription
```

## 支付流程
```
Client → PaymentService → PayPal
                         ↓
                      Webhook
                         ↓
                  SubscriptionService
```

# 8. 下载系统架构

## 下载流程
```
Client → DownloadService → 返回 CDN URL → 下载
```

# 9. 事件驱动架构

## 核心事件
```
UserCreatedEvent
SubscriptionActivatedEvent
PaymentCompletedEvent
DownloadStartedEvent
TaskCompletedEvent
```

# 10. 缓存设计

Redis 用于：
- 用户权限缓存
- 订阅状态缓存
- 热数据缓存

# 11. 安全设计

## API 安全
- JWT 鉴权
- HTTPS强制

## 支付安全
- Webhook 签名验证
- 幂等处理

# 12. CDN 架构

CDN 内容：
- 官网静态资源
- 安装包下载
- 图片资源

# 13. 部署架构

## 当前阶段
```
Docker + Cloud VM
```

## 成长阶段
```
Docker + Kubernetes
```

# 14. .NET 解决方案结构

```
YiPix.sln

src/
 ├ Services/
 │   ├ AuthService
 │   ├ UserService
 │   ├ SubscriptionService
 │   ├ PaymentService
 │   ├ DownloadService
 │   ├ ProductService
 │   ├ AnalyticsService
 │   ├ TaskService
 │   └ FileService
 │
 ├ Workers/
 │   ├ AIWorker
 │   ├ WebhookWorker
 │   └ AnalyticsWorker
 │
 └ BuildingBlocks/
     ├ EventBus
     ├ Logging
     ├ Common
     ├ Contracts
     └ Security
```

# 15. 第一阶段开发优先级

## 第一阶段（必须上线）
- AuthService
- UserService
- SubscriptionService
- PaymentService
- DownloadService

## 第二阶段
- ProductService
- AnalyticsService
- TaskService

## 第三阶段
- AIWorker
- 通知系统
- 推荐系统

# 16. 核心设计原则

- API 无状态设计
- 本地订阅状态中心化
- 所有耗时操作异步化
- 下载必须走 CDN
- Webhook 必须幂等

# 17. 未来扩展能力

支持扩展：
- 多支付渠道
- SaaS企业版
- 云 AI 服务
- 多租户架构

# 18. 架构总结

YiPix 后端系统是一个：
- 官网 + Admin + 客户端统一账号体系
- PayPal 驱动订阅中心
- CDN 驱动下载分发
- 事件驱动异步任务系统
- 云原生 SaaS 架构

