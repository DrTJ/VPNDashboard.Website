using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VPNDashboard.Website.Hubs;

[Authorize]
public class ConnectedClientsHub : Hub
{
}
