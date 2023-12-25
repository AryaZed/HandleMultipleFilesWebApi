using Minio.DataModel.Args;

namespace HandleMultipleFilesWebApi.Service.Minio
{
    public interface IMinioService
    {
        Task<string> GenerateDownloadLink(string objectName,string bucketName, int expiryTimeInSeconds = 900);
        Task DeleteFileFromMinio(string objectName, string bucketName);
        Task<string> GetBucketURL(string bucketName);
        Task GetObjectAsync(GetObjectArgs args);
        Task<string> PresignedGetObjectAsync(PresignedGetObjectArgs presignedGetObjectArgs);
        Task PutObjectAsync(PutObjectArgs putObjectArgs);
    }
}
