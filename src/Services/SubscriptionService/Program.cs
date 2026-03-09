using Microsoft.EntityFrameworkCore;
using YiPix.BuildingBlocks.Common.Middleware;
using YiPix.BuildingBlocks.Contracts.Events;
using YiPix.BuildingBlocks.EventBus;
using YiPix.BuildingBlocks.EventBus.Abstractions;
using YiPix.BuildingBlocks.Logging;
using YiPix.BuildingBlocks.Security;
using YiPix.BuildingBlocks.PayPal;
using Scalar.AspNetCore;
using YiPix.Services.Subscription.Application;
using YiPix.Services.Subscription.Handlers;
using YiPix.Services.Subscription.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// 结构化日志
builder.Host.UseYiPixSerilog("SubscriptionService");

// 数据库
builder.Services.AddDbContext<SubscriptionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// PayPal 客户端（用于取消订阅等操作）
builder.Services.AddPayPalClient(
    builder.Configuration.GetSection(PayPalOptions.SectionName));

// 仓储
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
// 应用服务
builder.Services.AddScoped<ISubscriptionAppService, SubscriptionAppService>();
// 事件处理器
builder.Services.AddScoped<PaymentCompletedEventHandler>();

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
    var db = scope.ServiceProvider.GetRequiredService<SubscriptionDbContext>();
    await db.Database.MigrateAsync();
}

// 订阅事件：支付完成 → 自动激活/续期订阅
var eventBus = app.Services.GetRequiredService<IEventBus>();
eventBus.Subscribe<PaymentCompletedEvent, PaymentCompletedEventHandler>();

app.Run();
