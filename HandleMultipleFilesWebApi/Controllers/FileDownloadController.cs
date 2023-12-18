using HandleMultipleFilesWebApi.Models;
using HandleMultipleFilesWebApi.Service.Files;
using HandleMultipleFilesWebApi.Service.Minio;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace HandleMultipleFilesWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileDownloadController : ControllerBase
    {
        private readonly IMinioService _minioService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FileDownloadController> _logger;

        public FileDownloadController(IMinioService minioService, IConfiguration configuration, ILogger<FileDownloadController> logger)
        {
            _minioService = minioService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost]
        [Route("download")]
        public async Task<IActionResult> DownloadFiles([FromBody] FileDownloadRequest request)
        {
            try
            {
                // Validate the request
                if (request.FileNames == null || !request.FileNames.Any())
                {
                    _logger.LogWarning("DownloadFiles request is invalid: FileNames are null or empty.");
                    return BadRequest("Invalid request.");
                }

                // Logic to handle file download and zipping
                string downloadUrl = await ProcessFilesAsync(request.FileNames);

                // Return the download URL
                return Ok(new { Url = downloadUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing DownloadFiles request.");
                return StatusCode(500, "An error occurred while processing your request.");
            }
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
            var downloadLink = await GenerateDownloadLinkWithExpiry(newZipPath, bucketName);

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
            const int basePathSegmentCount = 6;
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

            var url = await _minioService.GenerateDownloadLink(newZipPath, bucketName, exp);
            return (url, exp);
        }

        private async Task CleanupFiles(string urlPath,string bucketName)
        {
           await _minioService.DeleteFileFromMinio(urlPath,bucketName);            
        }

    }

}
