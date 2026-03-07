using Microsoft.EntityFrameworkCore;
using YiPix.BuildingBlocks.Common.Middleware;
using YiPix.BuildingBlocks.Logging;
using YiPix.BuildingBlocks.Security;
using Scalar.AspNetCore;
using YiPix.Services.FileStorage.Application;
using YiPix.Services.FileStorage.Infrastructure.Data;
using YiPix.Services.FileStorage.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// 结构化日志
builder.Host.UseYiPixSerilog("FileService");

// 数据库
builder.Services.AddDbContext<FileDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 仓储
builder.Services.AddScoped<IFileRepository, FileRepository>();
// 文件存储服务（本地存储，生产环境可替换为 MinIO/S3）
builder.Services.AddSingleton<IStorageService>(new LocalStorageService("uploads"));
// 应用服务
builder.Services.AddScoped<IFileAppService, FileAppService>();

// JWT 认证（无事件总线，FileService 不发布/订阅事件）
builder.Services.AddYiPixJwtAuth(builder.Configuration);

// API
builder.Services.AddControllers();
builder.Services.AddOpenApi();
// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// 全局异常处理中间件
app.UseMiddleware<GlobalExceptionMiddleware>();

// 开发环境启用 OpenAPI 文档和 Scalar UI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// HTTP 请求管道
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 开发环境自动执行数据库迁移
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FileDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
