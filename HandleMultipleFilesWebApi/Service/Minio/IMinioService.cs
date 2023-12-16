namespace HandleMultipleFilesWebApi.Service.Minio
{
    public interface IMinioService
    {
        Task<string> GenerateDownloadLink(string objectName,string bucketName, int expiryTimeInSeconds = 900);
        Task DeleteFileFromMinio(string objectName, string bucketName);
        Task<string> GetBucketURL(string bucketName);
    }
}
