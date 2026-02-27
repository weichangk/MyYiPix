using Microsoft.EntityFrameworkCore;
using YiPix.BuildingBlocks.Common.Middleware;
using YiPix.BuildingBlocks.Logging;
using YiPix.BuildingBlocks.Security;
using Scalar.AspNetCore;
using YiPix.Services.FileStorage.Application;
using YiPix.Services.FileStorage.Infrastructure.Data;
using YiPix.Services.FileStorage.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseYiPixSerilog("FileService");

builder.Services.AddDbContext<FileDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddSingleton<IStorageService>(new LocalStorageService("uploads"));
builder.Services.AddScoped<IFileAppService, FileAppService>();

builder.Services.AddYiPixJwtAuth(builder.Configuration);

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
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FileDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
