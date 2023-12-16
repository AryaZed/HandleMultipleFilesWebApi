using HandleMultipleFilesWebApi.Models;
using HandleMultipleFilesWebApi.Service.Minio;
using Microsoft.Extensions.Configuration;
using Minio;

var builder = WebApplication.CreateBuilder(args);

var minioConfig = builder.Configuration.GetSection("MinioConfig").Get<MinioConfigViewModel>();
builder.Services.AddMinio(configureClient => configureClient
           .WithEndpoint(minioConfig.Endpoint)
           .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey));

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
