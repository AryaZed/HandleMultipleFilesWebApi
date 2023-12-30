using System.Threading.Tasks;
using HandleMultipleFilesWebApi.Models;
using Hangfire.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace HandleMultipleFilesWebApi.Hubs;

public class JobStatusHub : Hub
{
    private readonly JobMonitorService _jobMonitorService;

    public JobStatusHub(JobMonitorService jobMonitorService)
    {
        _jobMonitorService = jobMonitorService;
    }

    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    // This method is used by the server to update job status

    public async Task UpdateJobStatus(string jobId)
    {
        _jobMonitorService.MonitorJob(jobId);
    }
}
