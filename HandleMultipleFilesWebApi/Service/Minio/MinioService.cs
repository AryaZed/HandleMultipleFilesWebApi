using Minio;
using Minio.DataModel.Args;
using Minio.DataModel;

namespace HandleMultipleFilesWebApi.Service.Minio
{
    public class MinioService : IMinioService
    {
        private readonly IMinioClient minioClient;

        public MinioService(IMinioClient minioClient)
        {
            this.minioClient = minioClient;
        }

        public async Task<string> GenerateDownloadLink(string objectName, string bucketName, int expiryTimeInSeconds = 900) // Default expiry time is 15 minutes
        {
            try
            {
                var args = new PresignedGetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithExpiry(expiryTimeInSeconds);

                var presignedUrl = await minioClient.PresignedGetObjectAsync(args).ConfigureAwait(false);

                return presignedUrl;
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., object not found, access denied)
                Console.WriteLine($"Error generating download link for {objectName}: {ex.Message}");
                return null;
            }
        }

        public async Task DeleteFileFromMinio(string objectFile,string bucketName)
        {
            try
            {
                var args = new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectFile);

                await minioClient.RemoveObjectAsync(args);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<string> GetBucketURL(string bucketName)
        {
            try
            {
                return string.Empty;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
