using HandleMultipleFilesWebApi.Models;
using HandleMultipleFilesWebApi.Service.Files;
using HandleMultipleFilesWebApi.Service.Minio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;
using System.IO.Compression;

namespace HandleMultipleFilesWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileDownloadController : ControllerBase
    {
        private readonly IMinioService _minioService;
        private readonly IMinioClient _minioClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileDownloadController> _logger;

        public FileDownloadController(IMinioService minioService, IConfiguration configuration, ILogger<FileDownloadController> logger, IMinioClient minioClient)
        {
            _minioService = minioService;
            _configuration = configuration;
            _logger = logger;
            _minioClient = minioClient;
        }

        [HttpPost]
        [Route("download")]
        public async Task<IActionResult> DownloadFiles([FromBody] FileDownloadRequest request)
        {
            if (request?.FileNames == null || !request.FileNames.Any())
            {
                return BadRequest("No file names provided.");
            }

            var datetime = DateTime.Now;
            var zipFileName = $"output-{datetime:HH-mm-ss}.zip";
            var bucketName = string.Empty;

            try
            {
                using var zipMemoryStream = new MemoryStream();
                using (var zip = new ZipArchive(zipMemoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var item in request.FileNames)
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
                        await _minioClient.GetObjectAsync(getObjectArgs);
                    }
                }

                if (zipMemoryStream.Length == 0)
                {
                    return NotFound("No files found to zip.");
                }

                await UploadZipToMinio(zipMemoryStream, bucketName, zipFileName);
                var presignedUrl = await GeneratePresignedUrl(bucketName, zipFileName);
                return Ok(new { Url = presignedUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DownloadFiles");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        private async Task UploadZipToMinio(MemoryStream zipMemoryStream, string bucketName, string zipFileName)
        {
            zipMemoryStream.Position = 0;
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(zipFileName)
                .WithStreamData(zipMemoryStream)
                .WithObjectSize(zipMemoryStream.Length);
            await _minioClient.PutObjectAsync(putObjectArgs);
        }

        private async Task<string> GeneratePresignedUrl(string bucketName, string zipFileName)
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(zipFileName)
                .WithExpiry(60 * 15); // 15 minutes expiry
            return await _minioClient.PresignedGetObjectAsync(presignedGetObjectArgs);
        }

        private async Task<string> ProcessFilesAsync(List<string> fileNames)
        {
            var bucketName = string.Empty;
            var filePath = new List<string>();
            foreach (var item in fileNames)
            {
                var urlSplit = item.Split("/");
                bucketName = urlSplit[0];
                var objectKey = urlSplit[1] + "/" + urlSplit[2];
                filePath.Add(objectKey);
            }    
            var localFilePaths = GetLocalFilePathsFromMinIO(filePath, bucketName);
            //UnzipFiles(localFilePaths, zipPassword); 
            var newZipPath = RepackageFilesIntoZip(localFilePaths);
            var res = newZipPath.Substring(1);
            var downloadLink = await GenerateDownloadLinkWithExpiry(res, bucketName);

            //await ScheduleFileCleanup(newZipPath, bucketName, downloadLink.ExpiryTime);

            return downloadLink.Url;
        }

        private async Task ScheduleFileCleanup(string urlPath, string bucketName, int expiryTime)
        {
            var delay = TimeSpan.FromSeconds(expiryTime);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay((int)delay.TotalMilliseconds).ContinueWith(async _ =>
                {
                    await CleanupFiles(urlPath, bucketName);
                });
            }
            else
            {
                // If the expiry time has already passed, clean up immediately
                await CleanupFiles(urlPath, bucketName);
            }
        }

        private List<string> GetLocalFilePathsFromMinIO(List<string> filePath,string bucketName)
        {
            var configValue = _configuration.GetSection("MinioConfig").Get<MinioConfigViewModel>();
            var minIOFileService = new MinIOFileService(configValue.RootDirectory);

            List<string> localFilePaths = new List<string>();
            foreach (var item in filePath)  
            {
                // Determine the local path for each file in MinIO
                // For example: "/minio_data/bucket_name/" + fileName
                string localPath = minIOFileService.GetLocalPathForMinIOFile(bucketName, item,configValue.Endpoint);
                localFilePaths.Add(localPath);
            }
            return localFilePaths;
        }

        private void UnzipFiles(List<string> filePaths, string zipPassword)
        {
            var fileProcessingService = new FileProcessingService();
            fileProcessingService.UnzipFiles(filePaths, zipPassword, "/path/to/output/directory");
        }

        private string RepackageFilesIntoZip(List<string> unzippedFilePaths)
        {
            if (unzippedFilePaths == null || !unzippedFilePaths.Any())
            {
                throw new ArgumentException("unzippedFilePaths is null or empty.");
            }

            var firstFilePath = unzippedFilePaths.First();
            var pathSegments = firstFilePath.Split('/');
            // Assuming the base path always has a fixed number of segments
            // In this case, '/home/smart/minio/data/user-newaryadb-bucket' has 6 segments
            const int basePathSegmentCount = 5;
            var basePathSegments = pathSegments.Take(basePathSegmentCount);
            var basePath = string.Join("/", basePathSegments);
            var date = DateTime.UtcNow;
            // Append the desired directory or file name to the base path
            string outputZipPath = Path.Combine(basePath, $"mergeFile-{date.Hour}-{date.Minute}-{date.Second}.zip");

            var outputDirectory = Path.GetDirectoryName(outputZipPath);
            try
            {
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }
                var fileProcessingService = new FileProcessingService();
                string zipPath = fileProcessingService.RepackageFilesIntoZip(unzippedFilePaths, outputZipPath);
                return zipPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating directory for zipped files.");
                throw;
            }            
        }


        private async Task<string> GenerateDownloadLink(string newZipPath, string bucketName)
        {
            var url = await _minioService.GenerateDownloadLink(newZipPath,bucketName);
            return url;
        }

        private async Task<(string Url, int ExpiryTime)> GenerateDownloadLinkWithExpiry(string newZipPath, string bucketName)
        {
            // Define an expiry time, for example, 24 hours from now
            var expiryTime = TimeSpan.FromMinutes(1);

            var exp = (int)expiryTime.TotalSeconds;

            var splitFile = newZipPath.Split('/');

            var objectKey = splitFile[3] + "/" + splitFile[4];

            var url = await _minioService.GenerateDownloadLink(objectKey, bucketName, exp);
            return (url, exp);
        }

        private async Task CleanupFiles(string urlPath,string bucketName)
        {
           await _minioService.DeleteFileFromMinio(urlPath,bucketName);            
        }

    }

}
