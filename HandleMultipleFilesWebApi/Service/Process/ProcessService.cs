using HandleMultipleFilesWebApi.Controllers;
using HandleMultipleFilesWebApi.Hubs;
using HandleMultipleFilesWebApi.Models;
using HandleMultipleFilesWebApi.Service.Minio;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Minio.DataModel.Args;
using System.IO.Compression;

namespace HandleMultipleFilesWebApi.Service.Process
{
    public class ProcessService : IProcessService
    {
        private readonly IMinioService _minioService;
        private readonly ILogger<FileDownloadController> _logger;
        private readonly IMemoryCache _memoryCache;
        private readonly IHubContext<JobStatusHub> _hubContext;

        public ProcessService(IMinioService minioService, ILogger<FileDownloadController> logger, IMemoryCache memoryCache, IHubContext<JobStatusHub> hubContext)
        {
            _minioService = minioService;
            _logger = logger;
            _memoryCache = memoryCache;
            _hubContext = hubContext;
        }

        public async Task ProcessFilesAsync(List<string> fileNames, string jobId)
        {
            var datetime = DateTime.Now;
            var zipFileName = $"output-{datetime:HH-mm-ss}.zip";
            var bucketName = string.Empty;

            try
            {
                SaveJob(jobId);
                using var zipMemoryStream = new MemoryStream();
                using (var zip = new ZipArchive(zipMemoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var item in fileNames)
                    {
                        var urlSplit = item.Split("/");
                        if (urlSplit.Length < 3)
                        {
                            _logger.LogWarning($"Invalid file path: {item}");
                            continue;
                        }

                        bucketName = urlSplit[0];
                        var objectKey = string.Join("/", urlSplit.Skip(1));
                        var zipEntry = zip.CreateEntry(Path.GetFileName(objectKey));

                        using var entryStream = zipEntry.Open();
                        var getObjectArgs = new GetObjectArgs()
                            .WithBucket(bucketName)
                            .WithObject(objectKey)
                            .WithCallbackStream(stream => stream.CopyTo(entryStream));
                        await _minioService.GetObjectAsync(getObjectArgs);
                    }
                }

                if (zipMemoryStream.Length == 0)
                {
                    _logger.LogWarning("No files found to zip for job " + jobId);
                    await _hubContext.Clients.All.SendAsync("ReceiveJobStatus", jobId, new { Status = "NoFilesFound" });
                    return; // Exit if no files were added to the zip
                }

                await UploadZipToMinio(zipMemoryStream, bucketName, zipFileName);
                var presignedUrl = await GeneratePresignedUrl(bucketName, zipFileName);

                await _hubContext.Clients.All.SendAsync("ReceiveJobStatus", jobId, new { Status = "Completed", Url = presignedUrl });
                //// Save or send the presigned URL along with the job ID for later retrieval
                //// This depends on how you track job statuses and results
                SaveJobResult(jobId, presignedUrl);
                _logger.LogInformation($"Sent 'Completed' status with URL for job {jobId}");

                //var test = _memoryCache.TryGetValue<JobResult>(jobId, out var jobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ProcessFilesAsync for job " + jobId);
                // Handle error, update job status accordingly
            }
        }

        public void SaveJobResult(string jobId, string presignedUrl)
        {
            var jobResult = new JobResult
            {
                JobId = jobId,
                PresignedUrl = presignedUrl,
                Status = "Completed" // or "Failed" depending on the context
            };

            // Save the result in the cache with some expiration time
            _memoryCache.Set(jobId, jobResult, TimeSpan.FromHours(1)); // Example: 1-hour expiration
        }

        public void SaveJob(string jobId)
        {
            var jobResult = new JobResult
            {
                JobId = jobId,
                PresignedUrl = string.Empty,
                Status = "Processing" // or "Failed" depending on the context
            };

            // Save the result in the cache with some expiration time
            _memoryCache.Set(jobId, jobResult, TimeSpan.FromHours(1)); // Example: 1-hour expiration
        }

        public async Task UploadZipToMinio(MemoryStream zipMemoryStream, string bucketName, string zipFileName)
        {
            zipMemoryStream.Position = 0;
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(zipFileName)
                .WithStreamData(zipMemoryStream)
                .WithObjectSize(zipMemoryStream.Length);
            await _minioService.PutObjectAsync(putObjectArgs);
        }

        public async Task<string> GeneratePresignedUrl(string bucketName, string zipFileName)
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(zipFileName)
                .WithExpiry(60 * 15); // 15 minutes expiry
            return await _minioService.PresignedGetObjectAsync(presignedGetObjectArgs);
        }

        public async Task CleanupFiles(string urlPath, string bucketName)
        {
            await _minioService.DeleteFileFromMinio(urlPath, bucketName);
        }
    }
}
