using Microsoft.AspNetCore.SignalR;
using VPNDashboard.Website.Hubs;

namespace VPNDashboard.Website.Services;

public class ConnectedClientsBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<ConnectedClientsHub> _hubContext;
    private readonly ILogger<ConnectedClientsBackgroundService> _logger;

    public ConnectedClientsBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<ConnectedClientsHub> hubContext,
        ILogger<ConnectedClientsBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reader = scope.ServiceProvider.GetRequiredService<OpenVpnReader>();
                var clients = reader.GetConnectedClients();

                await _hubContext.Clients.All.SendAsync("UpdateConnectedClients", clients, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error polling connected clients");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
