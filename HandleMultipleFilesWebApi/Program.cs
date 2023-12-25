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

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
);

builder.Services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
{
    builder
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetIsOriginAllowed(_ => true)
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
if (app.Environment.IsDevelopment())
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

// Map SignalR Hub
app.MapHub<JobStatusHub>("/jobStatusHub");

app.UseCors("CorsPolicy");

app.MapControllers();

app.Run();
