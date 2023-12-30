using HandleMultipleFilesWebApi.Hubs;
using HandleMultipleFilesWebApi.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System;

public class JobMonitorService
{
    private readonly IHubContext<JobStatusHub> _hubContext;
    private readonly IMemoryCache _memoryCache;

    public JobMonitorService(IHubContext<JobStatusHub> hubContext, IMemoryCache memoryCache)
    {
        _hubContext = hubContext;
        _memoryCache = memoryCache;
    }

    public void MonitorJob(string jobId)
    {
        // Start a new task to monitor the job
        _ = MonitorJobAsync(jobId);
    }

    private async Task MonitorJobAsync(string jobId)
    {
        bool isJobCompleted = false;
        while (!isJobCompleted)
        {
            // Implement logic to check the job status
            var state = Hangfire.JobStorage.Current.GetConnection().GetStateData(jobId);
            if (state?.Name == "Succeeded")
            {
                isJobCompleted = true;
                string url = await GetJobUrl(jobId);
                await _hubContext.Clients.All.SendAsync("ReceiveJobStatus", jobId, new { Status = "Succeeded", Url = url });
            }
            else
            {
                await Task.Delay(5000); // Wait before checking again
            }
        }
    }

    private async Task<string> GetJobUrl(string jobId)
    {
        var newJob = Hangfire.JobStorage.Current.GetConnection().GetJobParameter(jobId, "newjob");
        var url = string.Empty;
        if (newJob != null)
        {
            _memoryCache.TryGetValue<JobResult>(newJob, out var jobResult);
            if (jobResult != null && !string.IsNullOrWhiteSpace(jobResult.PresignedUrl))
            {
                url = jobResult.PresignedUrl;                
            }
        }
        return url;
    }
}
