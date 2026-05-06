namespace VPNDashboard.Website.Models;

public class ServerStatus
{
    public bool IsInstalled { get; set; }
    public bool IsRunning { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public int Port { get; set; }
    public string ListenAddress { get; set; } = string.Empty;
    public string Subnet { get; set; } = string.Empty;
    public int TotalProfiles { get; set; }
    public int ActiveProfiles { get; set; }
    public int RevokedProfiles { get; set; }
    public int ConnectedClients { get; set; }
}
