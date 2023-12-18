using HandleMultipleFilesWebApi.Models;
using HandleMultipleFilesWebApi.Service.Minio;
using Microsoft.Extensions.Configuration;
using Minio;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .WriteTo.Console()
    .WriteTo.File("logs/myapp.txt", rollingInterval: RollingInterval.Day)
);

var minioConfig = builder.Configuration.GetSection("MinioConfig").Get<MinioConfigViewModel>();
builder.Services.AddMinio(configureClient => configureClient
           .WithEndpoint(minioConfig.Endpoint)
           .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey)
           .WithSSL(false));

// Register MinioService
builder.Services.AddScoped<IMinioService, MinioService>();

builder.Services.AddScoped<MinIOFileService>(serviceProvider =>
{
    var configuration = serviceProvider.GetService<IConfiguration>();
    var yourConfigValue = configuration.GetValue<string>("MinioRootConfig");
    return new MinIOFileService(yourConfigValue);
});


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
