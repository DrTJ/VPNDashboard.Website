namespace VPNDashboard.Website.Models;

public class ConnectedClient
{
    public string CommonName { get; set; } = string.Empty;
    public string RealAddress { get; set; } = string.Empty;
    public string VirtualAddress { get; set; } = string.Empty;
    public long BytesReceived { get; set; }
    public long BytesSent { get; set; }
    public DateTime ConnectedSince { get; set; }
}
