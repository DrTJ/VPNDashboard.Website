using Renci.SshNet;
using VPNDashboard.AdminWeb.Data;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Services;

public interface IDeployService
{
    Task DeployAsync(TargetServer server, BuildArtifact artifact, IProgress<string> progress, CancellationToken ct = default);
}

public class DeployService : IDeployService
{
    private readonly ISshSessionFactory _ssh;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;

    public DeployService(ISshSessionFactory ssh, IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _ssh = ssh;
        _config = config;
        _scopeFactory = scopeFactory;
    }

    public async Task DeployAsync(TargetServer server, BuildArtifact artifact, IProgress<string> progress, CancellationToken ct = default)
    {
        var artifactDir = _config.GetValue<string>("Build:ArtifactDir") ?? "/var/lib/vpndashboard-admin/artifacts";
        var artifactPath = Path.Combine(artifactDir, artifact.FileName);

        if (!File.Exists(artifactPath))
            throw new FileNotFoundException($"Artifact not found: {artifact.FileName}", artifactPath);

        var connInfo = _ssh.CreateConnectionInfo(server);

        progress.Report($"=== Uploading {artifact.FileName} to {server.Host}:/tmp/ ===");
        await Task.Run(() =>
        {
            using var scp = new ScpClient(connInfo);
            scp.Connect();
            using var fs = File.OpenRead(artifactPath);
            scp.Upload(fs, "/tmp/vpndashboard-deploy.tar.gz");
            scp.Disconnect();
        }, ct);
        progress.Report("Upload complete.");

        progress.Report("=== Running remote deploy commands ===");
        await Task.Run(() =>
        {
            using var ssh = new SshClient(connInfo);
            ssh.Connect();

            var commands = new[]
            {
                $"sudo systemctl stop {server.ServiceName}",
                $"sudo mkdir -p {server.InstallDir}",
                $"sudo tar -xzf /tmp/vpndashboard-deploy.tar.gz -C {server.InstallDir}",
                $"sudo chown -R {server.ServiceUser}:{server.ServiceUser} {server.InstallDir}",
                $"sudo systemctl start {server.ServiceName}",
                "rm -f /tmp/vpndashboard-deploy.tar.gz",
                $"sudo systemctl --no-pager --lines=10 status {server.ServiceName}"
            };

            foreach (var cmdText in commands)
            {
                progress.Report($"$ {cmdText}");
                var cmd = ssh.RunCommand(cmdText);
                if (!string.IsNullOrWhiteSpace(cmd.Result))
                    progress.Report(cmd.Result.TrimEnd());
                if (!string.IsNullOrWhiteSpace(cmd.Error))
                    progress.Report(cmd.Error.TrimEnd());
            }

            ssh.Disconnect();
        }, ct);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        var tracked = await db.TargetServers.FindAsync(new object[] { server.Id }, ct);
        if (tracked != null)
        {
            tracked.LastDeployedAt = DateTime.UtcNow;
            tracked.LastDeployedCommitSha = artifact.CommitSha;
            tracked.LastDeployedArtifactName = artifact.FileName;
            await db.SaveChangesAsync(ct);
        }

        progress.Report("=== Deploy complete ===");
        progress.Report("EXIT 0");
    }
}
