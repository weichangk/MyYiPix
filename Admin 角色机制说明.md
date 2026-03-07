# Admin 角色机制说明

## 概述

DownloadService 中的 `CreateRelease`（发布新版本）和 `GetCount`（下载统计）接口使用了 `[Authorize(Roles = "Admin")]` 权限控制，要求调用者必须是 Admin 角色的已登录用户。

## 当前机制

### 角色存储

角色存储在 AuthService 的 `User` 实体中，是一个简单的字符串字段：

```csharp
// AuthService/Domain/Entities/User.cs
[MaxLength(20)]
public string Role { get; set; } = "User";
```

数据库默认值也是 `"User"`：

```csharp
// AuthService/Infrastructure/Data/AuthDbContext.cs
entity.Property(e => e.Role).HasMaxLength(20).HasDefaultValue("User");
```

### 注册时角色分配

注册时角色被**硬编码为 `"User"`**，不接受外部传入：

```csharp
// AuthService/Application/AuthService.cs
var user = new User
{
    Email = request.Email,
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
    DisplayName = request.DisplayName,
    Role = "User"  // 硬编码，无法通过注册接口指定角色
};
```

### JWT Token 中的角色写入

登录/注册/刷新 Token 时，将 `user.Role` 写入 JWT 的标准 `ClaimTypes.Role` 声明：

```csharp
// BuildingBlocks/Security/JwtTokenService.cs
public string GenerateAccessToken(Guid userId, string email, string role, string? subscriptionPlan = null)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, userId.ToString()),
        new(ClaimTypes.Email, email),
        new(ClaimTypes.Role, role),   // 角色写入标准 ClaimTypes.Role
        new("sub", userId.ToString())
    };
    // ...
}
```

### 权限检查

`[Authorize(Roles = "Admin")]` 执行两层检查：

| 检查 | 条件 | 未通过返回 |
|------|------|-----------|
| 第一层：认证 | 请求必须携带有效 JWT Token | 401 Unauthorized |
| 第二层：授权 | Token 中 `ClaimTypes.Role` 必须包含 `"Admin"` | 403 Forbidden |

### 使用 Admin 角色的接口

| 服务 | 接口 | 用途 |
|------|------|------|
| DownloadService | `POST /api/downloads/releases` | 发布新版本 |
| DownloadService | `GET /api/downloads/stats/count` | 下载统计 |
| ProductService | `POST /api/products` | 创建产品 |
| ProductService | `PUT /api/products/{id}` | 更新产品 |
| AnalyticsService | `GET /api/analytics/overview` | 概览统计 |
| AnalyticsService | `GET /api/analytics/users` | 用户统计 |
| AnalyticsService | `GET /api/analytics/revenue` | 营收统计 |

### 修改角色的 API

**当前不存在。** 没有任何接口可以修改用户角色。

## 当前唯一获得 Admin 角色的方式

直接修改数据库：

```sql
-- 连接到 auth 数据库，将某个用户设为 Admin
UPDATE auth."Users" SET "Role" = 'Admin' WHERE "Email" = 'your-admin@example.com';
```

修改后该用户重新登录，拿到的 JWT 中就会携带 `Role = Admin`，即可调用所有 Admin 接口。

## 完善建议

| 方案 | 说明 | 适用场景 | 复杂度 |
|------|------|---------|--------|
| **种子数据** | 系统启动时自动创建默认 Admin 账号 | 最简单，推荐优先实现 | 低 |
| **Admin 管理接口** | 新增 `PUT /api/users/{id}/role`，仅 Admin 可调用 | 多管理员场景 | 中 |
| **CLI 命令** | 写一个命令行工具设置角色 | 运维友好 | 中 |

### 方案一：种子数据（推荐优先实现）

在 AuthService 启动时，自动检查并创建默认管理员账号：

```csharp
// AuthService/Program.cs 中 app.Run() 之前添加
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await db.Database.MigrateAsync();

    // 创建默认管理员
    if (!await db.Users.AnyAsync(u => u.Role == "Admin"))
    {
        db.Users.Add(new User
        {
            Email = "admin@yipix.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            DisplayName = "System Admin",
            Role = "Admin"
        });
        await db.SaveChangesAsync();
    }
}
```

### 方案二：Admin 管理接口

在 UserService 中新增角色管理端点：

```csharp
[HttpPut("{id:guid}/role")]
[Authorize(Roles = "Admin")]
public async Task<ActionResult<ApiResponse>> UpdateRole(
    Guid id, [FromBody] UpdateRoleRequest request, CancellationToken ct)
{
    await _service.UpdateRoleAsync(id, request.Role, ct);
    return Ok(ApiResponse.Ok("Role updated."));
}

public record UpdateRoleRequest(string Role);
```

此接口本身需要 Admin 权限，因此第一个 Admin 仍需通过种子数据或数据库创建。

### 方案三：CLI 命令

创建一个命令行工具，运维人员在服务器上执行：

```bash
dotnet run --project src/Services/AuthService -- set-role --email admin@yipix.com --role Admin
```

## 安全注意事项

- 生产环境的默认 Admin 密码必须在首次登录后立即修改
- 建议通过环境变量注入默认 Admin 的邮箱和密码，不要硬编码在代码中
- Admin 操作建议增加审计日志记录
