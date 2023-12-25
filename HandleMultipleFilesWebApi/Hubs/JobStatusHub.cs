using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace HandleMultipleFilesWebApi.Hubs;

public class JobStatusHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    // This method is used by the server to update job status
    public async Task UpdateJobStatus(string jobId, string status, string url = null)
    {
        // Send job status to all connected clients
        // You might want to send it to a specific client based on the jobId
        await Clients.All.SendAsync("ReceiveJobStatus", jobId, new { Status = status, Url = url });
    }
}
