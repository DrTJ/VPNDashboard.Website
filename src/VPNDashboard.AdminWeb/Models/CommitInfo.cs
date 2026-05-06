namespace VPNDashboard.AdminWeb.Models;

public class CommitInfo
{
    public string Sha { get; set; } = "";
    public string ShortSha => Sha.Length >= 7 ? Sha[..7] : Sha;
    public string Author { get; set; } = "";
    public DateTime Date { get; set; }
    public string Message { get; set; } = "";
}
