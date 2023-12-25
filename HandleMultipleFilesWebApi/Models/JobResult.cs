namespace HandleMultipleFilesWebApi.Models
{
    public class JobResult
    {
        public string JobId { get; set; }
        public string PresignedUrl { get; set; }
        public string Status { get; set; }
    }
}
