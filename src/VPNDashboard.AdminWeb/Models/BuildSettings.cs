namespace VPNDashboard.AdminWeb.Models;

public class BuildSettings
{
    public int Id { get; set; }
    public string RepositoryUrl { get; set; } = "";
    public string DefaultBranch { get; set; } = "main";
    public string ProjectPath { get; set; } = "src/VPNDashboard.Website/VPNDashboard.Website.csproj";
    public byte[]? GitHubTokenEncrypted { get; set; }
    public string? GitHubUsername { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
