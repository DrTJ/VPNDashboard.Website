using Microsoft.AspNetCore.SignalR;

namespace VPNDashboard.AdminWeb.Hubs;

public class LiveLogHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }
}

public class HubProgress : IProgress<string>
{
    private readonly IHubContext<LiveLogHub> _hub;
    private readonly string _groupName;

    public HubProgress(IHubContext<LiveLogHub> hub, string groupName)
    {
        _hub = hub;
        _groupName = groupName;
    }

    public void Report(string value)
    {
        _hub.Clients.Group(_groupName).SendAsync("ReceiveLog", value).ConfigureAwait(false);
    }
}
