using HandleMultipleFilesWebApi;
using HandleMultipleFilesWebApi.Hubs;
using HandleMultipleFilesWebApi.Models;
using HandleMultipleFilesWebApi.Service.Files;
using HandleMultipleFilesWebApi.Service.Minio;
using HandleMultipleFilesWebApi.Service.Process;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Serilog;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Any, 443, listenOptions =>
    {
        // Adjust the path and password as necessary
        listenOptions.UseHttps("/app/data/certs/smartx.ir_2023_2.pfx", "1123");
    });
});

builder.Services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
{
    builder
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetIsOriginAllowed(_ => true)
    .WithOrigins("https://backup.smartx.ir")
    .AllowCredentials();
}));

builder.Services.AddSignalR();

var minioConfig = builder.Configuration.GetSection("MinioConfig").Get<MinioConfigViewModel>();
builder.Services.AddMinio(configureClient => configureClient
           .WithEndpoint(minioConfig.Endpoint)
           .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey)
           .WithSSL(false));

// Register MinioService
builder.Services.AddScoped<IMinioService, MinioService>();

builder.Services.AddScoped<MinIOFileService>(serviceProvider =>
{
    return new MinIOFileService(minioConfig.RootDirectory);
});


builder.Services.AddTransient<FileProcessingService>();
builder.Services.AddTransient<IProcessService, ProcessService>();

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

builder.Services.AddHangfire(configuration => configuration
          .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseMemoryStorage());

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() && app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
    });
}

app.UseHangfireServer();

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors("CorsPolicy");
// Map SignalR Hub
app.MapHub<JobStatusHub>("/jobStatusHub");

app.MapControllers();

app.Run();
