namespace VPNDashboard.AdminWeb.Models;

public enum ServerTier { Free, Paid }

public class TargetServer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ServerTier Tier { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public byte[] PasswordEncrypted { get; set; } = Array.Empty<byte>();
    public string InstallDir { get; set; } = "/opt/vpn-dashboard";
    public string ServiceName { get; set; } = "vpn-dashboard";
    public string ServiceUser { get; set; } = "vpndash";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastDeployedAt { get; set; }
    public string? LastDeployedCommitSha { get; set; }
    public string? LastDeployedArtifactName { get; set; }
}
