# YiPix Token 认证指南

## 一、JWT 配置说明

项目使用 JWT（JSON Web Token）实现跨服务的统一身份认证。核心配置有三个：

| 配置项 | 说明 |
|--------|------|
| `JWT_SECRET` | 签名密钥，用于对 AccessToken 进行 HMAC-SHA256 签名和验证，必须至少 32 字符 |
| `JWT_ISSUER` | 签发者标识，写入 Token 的 `iss` 声明，验证时检查 Token 是否由该签发者签发 |
| `JWT_AUDIENCE` | 受众标识，写入 Token 的 `aud` 声明，标明 Token 的目标受众 |

### 配置位置

- **开发环境**：`src/Services/<ServiceName>/appsettings.Development.json`
  ```json
  "JwtSettings": {
    "Secret": "YiPix-Super-Secret-Key-Must-Be-At-Least-32-Characters-Long!",
    "Issuer": "YiPix",
    "Audience": "YiPixClients",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  }
  ```
- **生产环境**：通过 `docker/.env` 注入环境变量
  ```env
  JWT_SECRET=请替换为至少32字符的随机字符串
  JWT_ISSUER=YiPix
  JWT_AUDIENCE=YiPixClients
  ```

### 注意事项

1. **每个部署环境应使用不同的 Secret**（开发、测试、生产各一个）
2. **服务启动后不要随意更换 Secret**，否则所有已签发的 AccessToken 立即失效
3. **同一环境内所有微服务必须使用同一个 Secret**，否则 AuthService 签发的 Token 其他服务无法验证
4. **生产环境绝不能使用开发环境的默认值**，因为它是公开在代码仓库中的

---

## 二、Token 类型

登录/注册成功后，AuthService 返回两个 Token：

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "base64随机字符串",
  "expiresAt": "2026-03-05T11:00:00Z"
}
```

| Token 类型 | 格式 | 有效期 | 用途 |
|-----------|------|--------|------|
| **AccessToken** | JWT（含用户 Claims） | 60 分钟 | 请求受保护的 API 时携带 |
| **RefreshToken** | Base64 随机字符串 | 30 天 | AccessToken 过期后用于换取新 Token 对 |

### AccessToken 中包含的 Claims

| Claim | 说明 |
|-------|------|
| `sub` / `NameIdentifier` | 用户 ID（Guid） |
| `Email` | 用户邮箱 |
| `Role` | 用户角色（如 "User"） |
| `subscription_plan` | 订阅计划（可选） |

---

## 三、认证流程

### 3.1 登录获取 Token

```
客户端                          AuthService
  │                                │
  │  POST /api/auth/login          │
  │  { email, password }           │
  │───────────────────────────────>│
  │                                │ 1. 验证邮箱密码（BCrypt）
  │                                │ 2. 生成 AccessToken + RefreshToken
  │                                │ 3. RefreshToken 持久化到数据库
  │  { accessToken, refreshToken,  │
  │    expiresAt }                 │
  │<───────────────────────────────│
  │                                │
  │  客户端保存 Token              │
```

> 注册（`POST /api/auth/register`）成功后也会直接返回 Token，无需再调用登录接口。

### 3.2 携带 Token 请求其他服务

```
客户端                                      UserService / TaskService / ...
  │                                                │
  │  GET /api/users/xxx                            │
  │  Header: Authorization: Bearer eyJhbG...       │
  │───────────────────────────────────────────────>│
  │                                                │ JWT 中间件自动验签
  │                                                │ 解析 Claims → HttpContext.User
  │  { 用户数据 }                                   │
  │<───────────────────────────────────────────────│
```

**重要：Token 不会自动携带，客户端必须在每次请求时手动添加 `Authorization` Header。**

### 3.3 Token 刷新

```
客户端                          AuthService
  │                                │
  │  POST /api/auth/refresh        │
  │  { refreshToken }              │
  │───────────────────────────────>│
  │                                │ 1. 验证 RefreshToken 是否有效
  │                                │ 2. 吊销旧 RefreshToken
  │                                │ 3. 签发新的 AccessToken + RefreshToken
  │  { accessToken, refreshToken,  │
  │    expiresAt }                 │
  │<───────────────────────────────│
```

采用 **Refresh Token Rotation** 策略：每次刷新都会吊销旧 Token、签发新 Token，防止重放攻击。

### 3.4 Token 吊销（登出）

```
客户端                          AuthService
  │                                │
  │  POST /api/auth/revoke         │
  │  { refreshToken }              │
  │───────────────────────────────>│
  │                                │ 将 RefreshToken 标记为已吊销
  │  200 OK                        │
  │<───────────────────────────────│
```

---

## 四、服务端验证机制

### 为什么其他服务不需要调用 AuthService 来验证 Token？

因为 JWT 是**自包含**的。所有微服务通过共享的 `BuildingBlocks/Security` 模块，使用相同的 Secret 独立验签：

```csharp
// 每个服务的 Program.cs 中都有这一行
builder.Services.AddYiPixJwtAuth(builder.Configuration);
```

该扩展方法会：
1. 读取 `JwtSettings` 配置（Secret、Issuer、Audience）
2. 注册 JWT Bearer 认证中间件
3. 配置 Token 验证参数（验签、验证签发者、验证受众、验证过期时间）

### 受保护的服务接口一览

| 服务 | Controller | 保护方式 |
|------|-----------|---------|
| UserService | `UsersController` | `[Authorize]` 类级别 — 所有接口需要 Token |
| TaskService | `TasksController` | `[Authorize]` 类级别 |
| SubscriptionService | `SubscriptionsController` | `[Authorize]` 类级别 |
| FileService | `FilesController` | `[Authorize]` 类级别 |
| PaymentService | `PaymentsController` | `[Authorize]` 方法级别（Webhook 端点用 `[AllowAnonymous]` 排除） |
| AuthService | `AuthController` | 无 `[Authorize]` — 登录注册等接口本身不需要认证 |

---

## 五、客户端集成指南

### 客户端需要做的事

1. **存储 Token**：登录成功后将 `AccessToken` 和 `RefreshToken` 保存到安全存储中
2. **携带 Token**：每次请求受保护接口时，在 HTTP Header 中添加：
   ```
   Authorization: Bearer <AccessToken>
   ```
3. **自动刷新**：检测到 Token 过期（通过 `ExpiresAt` 判断或收到 401 响应）时，调用 `/api/auth/refresh` 获取新 Token
4. **统一处理**：建议在 HTTP 客户端层（如 Axios 拦截器、HttpClient DelegatingHandler）统一处理 Token 的携带和刷新逻辑

### 示例：前端 Axios 拦截器

```javascript
// 请求拦截器 — 自动附加 Token
axios.interceptors.request.use(config => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 响应拦截器 — 自动刷新 Token
axios.interceptors.response.use(
  response => response,
  async error => {
    if (error.response?.status === 401) {
      const refreshToken = localStorage.getItem('refreshToken');
      const { data } = await axios.post('/api/auth/refresh', { refreshToken });
      localStorage.setItem('accessToken', data.data.accessToken);
      localStorage.setItem('refreshToken', data.data.refreshToken);
      // 用新 Token 重试原请求
      error.config.headers.Authorization = `Bearer ${data.data.accessToken}`;
      return axios(error.config);
    }
    return Promise.reject(error);
  }
);
```

### API 端点汇总

| 方法 | 路由 | 说明 | 需要 Token |
|------|------|------|-----------|
| POST | `/api/auth/register` | 用户注册 | 否 |
| POST | `/api/auth/login` | 用户登录 | 否 |
| POST | `/api/auth/refresh` | 刷新 Token | 否 |
| POST | `/api/auth/revoke` | 吊销 Token | 否 |
