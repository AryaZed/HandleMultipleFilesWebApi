using HandleMultipleFilesWebApi.Hubs;
using HandleMultipleFilesWebApi.Models;
using HandleMultipleFilesWebApi.Service.Process;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace HandleMultipleFilesWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FileDownloadController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly IProcessService _processService;


        public FileDownloadController(IMemoryCache memoryCache, IBackgroundJobClient backgroundJobClient, IProcessService processService)
        {
            _memoryCache = memoryCache;
            _backgroundJobClient = backgroundJobClient;
            _processService = processService;
        }

        [HttpPost]
        [Route("download")]
        public async Task<IActionResult> DownloadFiles([FromBody] FileDownloadRequest request)
        {
            try
            {
                if (request?.FileNames == null || !request.FileNames.Any())
                {
                    return BadRequest("No file names provided.");
                }

                var jobId = Guid.NewGuid().ToString();
                // Start the asynchronous processing here (e.g., using a background worker or task queue)

                _backgroundJobClient.Enqueue(() => _processService.ProcessFilesAsync(request.FileNames, jobId));

                //await _processService.ProcessFilesAsync(request.FileNames, jobId);

                return Ok(new { JobId = jobId });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [Route("checkStatus")]
        public IActionResult CheckJobStatus(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return BadRequest("Invalid job ID.");
            }

            if (!_memoryCache.TryGetValue<JobResult>(jobId, out var jobResult))
            {
                return NotFound("Job not found.");
            }

            if (jobResult.Status == "Completed")
            {
                return Ok(new { Url = jobResult.PresignedUrl });
            }

            return Ok(new { Status = jobResult.Status }); // Or any other status message
        }

    }

}
