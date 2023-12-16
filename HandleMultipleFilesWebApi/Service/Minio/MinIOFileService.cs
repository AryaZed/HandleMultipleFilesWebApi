namespace HandleMultipleFilesWebApi.Service.Minio
{
    public class MinIOFileService
    {
        private readonly string minIORootDirectory; // Root directory where MinIO stores its data

        public MinIOFileService(string minIORootDirectory)
        {
            this.minIORootDirectory = minIORootDirectory;
        }

        public string GetLocalPathForMinIOFile(string bucketName, string fileName,string endPoint)
        {
            // Construct the local file path based on the bucket and file name
            // Example: "/minio_data/{bucketName}/{fileName}"
            string localFilePath = Path.Combine(minIORootDirectory, bucketName, fileName);
            return localFilePath;
        }
    }
}