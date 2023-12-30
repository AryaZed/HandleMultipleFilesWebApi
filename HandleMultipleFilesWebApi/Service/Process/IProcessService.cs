namespace HandleMultipleFilesWebApi.Service.Process
{
    public interface IProcessService
    {
        Task ProcessFilesAsync(List<string> fileNames,string jobId);
        void SaveJobResult(string jobId, string presignedUrl);
        Task UploadZipToMinio(MemoryStream zipMemoryStream, string bucketName, string zipFileName);
        Task<string> GeneratePresignedUrl(string bucketName, string zipFileName);
        Task CleanupFiles(string urlPath, string bucketName);
    }
}
