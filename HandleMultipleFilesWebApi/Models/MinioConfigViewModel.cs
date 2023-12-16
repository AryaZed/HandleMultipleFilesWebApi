namespace HandleMultipleFilesWebApi.Models
{
    public class MinioConfigViewModel
    {
        public string Endpoint
        {
            get; set;
        }
        public string AccessKey
        {
            get; set;
        }
        public string SecretKey
        {
            get; set;
        }
        public string RootDirectory { get; set; }
    }
}
