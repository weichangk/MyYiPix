using Microsoft.EntityFrameworkCore;
using YiPix.BuildingBlocks.Common.Middleware;
using YiPix.BuildingBlocks.EventBus;
using YiPix.BuildingBlocks.Logging;
using YiPix.BuildingBlocks.Security;
using YiPix.Services.Download.Application;
using YiPix.Services.Download.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseYiPixSerilog("DownloadService");

builder.Services.AddDbContext<DownloadDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDownloadRepository, DownloadRepository>();
builder.Services.AddScoped<IDownloadAppService, DownloadAppService>();

builder.Services.AddYiPixJwtAuth(builder.Configuration);
builder.Services.AddRabbitMQEventBus(
    builder.Configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DownloadDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
