namespace VPNDashboard.Website.Models;

public class ClientProfile
{
    public string Name { get; set; } = string.Empty;
    public ClientStatus Status { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? RevocationDate { get; set; }
    public string Serial { get; set; } = string.Empty;
}

public enum ClientStatus
{
    Valid,
    Revoked,
    Expired
}
