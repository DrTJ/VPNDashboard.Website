using System.Collections.Concurrent;
using Renci.SshNet;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Services;

public interface IServerStatusService
{
    Task<string> GetStatusAsync(TargetServer server);
    void InvalidateCache(int serverId);
}

public class ServerStatusService : IServerStatusService
{
    private readonly ISshSessionFactory _ssh;
    private readonly ConcurrentDictionary<int, (string Status, DateTime FetchedAt)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public ServerStatusService(ISshSessionFactory ssh)
    {
        _ssh = ssh;
    }

    public async Task<string> GetStatusAsync(TargetServer server)
    {
        if (_cache.TryGetValue(server.Id, out var cached) &&
            DateTime.UtcNow - cached.FetchedAt < CacheDuration)
        {
            return cached.Status;
        }

        var status = await Task.Run(() => ProbeStatus(server));
        _cache[server.Id] = (status, DateTime.UtcNow);
        return status;
    }

    public void InvalidateCache(int serverId)
    {
        _cache.TryRemove(serverId, out _);
    }

    private string ProbeStatus(TargetServer server)
    {
        try
        {
            var connInfo = _ssh.CreateConnectionInfo(server);
            using var client = new SshClient(connInfo);
            client.Connect();
            var cmd = client.RunCommand($"systemctl is-active {server.ServiceName} 2>/dev/null || echo unknown");
            client.Disconnect();
            return cmd.Result.Trim();
        }
        catch
        {
            return "unreachable";
        }
    }
}
