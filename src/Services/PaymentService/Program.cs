using Microsoft.EntityFrameworkCore;
using YiPix.BuildingBlocks.Common.Middleware;
using YiPix.BuildingBlocks.EventBus;
using YiPix.BuildingBlocks.Logging;
using YiPix.BuildingBlocks.Security;
using YiPix.BuildingBlocks.PayPal;
using Scalar.AspNetCore;
using YiPix.Services.Payment.Application;
using YiPix.Services.Payment.Infrastructure.Data;
using PayPalOptions = YiPix.BuildingBlocks.PayPal.PayPalOptions;

var builder = WebApplication.CreateBuilder(args);

// 结构化日志
builder.Host.UseYiPixSerilog("PaymentService");

// 数据库
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// PayPal 客户端（统一使用 BuildingBlocks.PayPal）
builder.Services.AddPayPalClient(
    builder.Configuration.GetSection(PayPalOptions.SectionName));

// 仓储
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
// 应用服务
builder.Services.AddScoped<IPaymentAppService, PaymentAppService>();

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
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
