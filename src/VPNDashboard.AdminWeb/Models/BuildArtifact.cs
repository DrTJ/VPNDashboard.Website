namespace VPNDashboard.AdminWeb.Models;

public class BuildArtifact
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string Branch { get; set; } = "";
    public string CommitSha { get; set; } = "";
    public string CommitMessage { get; set; } = "";
    public string CommitAuthor { get; set; } = "";
    public DateTime CommitDate { get; set; }
    public DateTime BuiltAt { get; set; }
    public long SizeBytes { get; set; }
}
