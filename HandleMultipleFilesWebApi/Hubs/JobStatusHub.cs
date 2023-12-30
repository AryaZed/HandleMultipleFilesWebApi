using System.Threading.Tasks;
using HandleMultipleFilesWebApi.Models;
using Hangfire.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace HandleMultipleFilesWebApi.Hubs;

public class JobStatusHub : Hub
{
    private readonly IMemoryCache _memoryCache;

    public JobStatusHub(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    // This method is used by the server to update job status

    public async Task UpdateJobStatus(string jobId, string status)
    {
        var state = Hangfire.JobStorage.Current.GetConnection().GetStateData(jobId);
        var url = string.Empty;
        if (status == state.Name)
        {
            var newJob = Hangfire.JobStorage.Current.GetConnection().GetJobParameter(jobId, "newjob");
            if (newJob != null)
            {
                _memoryCache.TryGetValue<JobResult>(newJob, out var jobResult);
                if (jobResult != null && !string.IsNullOrWhiteSpace(jobResult.PresignedUrl))
                {
                    url = jobResult.PresignedUrl;
                }
            }
            await Clients.All.SendAsync("ReceiveJobStatus", jobId, new { Status = status, Url = url });
        }
    }
}
