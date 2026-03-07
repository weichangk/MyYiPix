using Microsoft.EntityFrameworkCore;
using YiPix.BuildingBlocks.Common.Middleware;
using YiPix.BuildingBlocks.EventBus;
using YiPix.BuildingBlocks.Logging;
using YiPix.BuildingBlocks.Security;
using Scalar.AspNetCore;
using YiPix.Services.Download.Application;
using YiPix.Services.Download.Infrastructure.Cdn;
using YiPix.Services.Download.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 结构化日志
builder.Host.UseYiPixSerilog("DownloadService");

// 数据库
builder.Services.AddDbContext<DownloadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 仓储
builder.Services.AddScoped<IDownloadRepository, DownloadRepository>();
// 应用服务
builder.Services.AddScoped<IDownloadAppService, DownloadAppService>();

// CDN 签名服务（阿里云 CDN 类型A 鉴权）
builder.Services.Configure<CdnSettings>(builder.Configuration.GetSection("CdnSettings"));
builder.Services.AddSingleton<ICdnSignService, AliyunCdnSignService>();

// JWT 认证
builder.Services.AddYiPixJwtAuth(builder.Configuration);
// 事件总线（RabbitMQ）
builder.Services.AddRabbitMQEventBus(
    builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");

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
    var db = scope.ServiceProvider.GetRequiredService<DownloadDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
