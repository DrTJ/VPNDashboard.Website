using Microsoft.EntityFrameworkCore;
using VPNDashboard.AdminWeb.Data;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Services;

public interface IBuildSettingsStore
{
    Task<BuildSettings> GetAsync();
    Task SaveAsync(BuildSettings settings, string? newToken = null);
    string? DecryptToken(BuildSettings settings);
}

public class BuildSettingsStore : IBuildSettingsStore
{
    private readonly AdminDbContext _db;
    private readonly CredentialProtector _protector;

    public BuildSettingsStore(AdminDbContext db, CredentialProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<BuildSettings> GetAsync()
    {
        var settings = await _db.BuildSettings.FindAsync(1);
        return settings ?? throw new InvalidOperationException("BuildSettings row missing; database may not have been seeded.");
    }

    public async Task SaveAsync(BuildSettings settings, string? newToken = null)
    {
        settings.Id = 1;
        if (newToken != null)
        {
            settings.GitHubTokenEncrypted = string.IsNullOrEmpty(newToken)
                ? null
                : _protector.Protect(newToken);
        }

        settings.UpdatedAt = DateTime.UtcNow;
        _db.BuildSettings.Update(settings);
        await _db.SaveChangesAsync();
    }

    public string? DecryptToken(BuildSettings settings)
    {
        if (settings.GitHubTokenEncrypted == null || settings.GitHubTokenEncrypted.Length == 0)
            return null;
        return _protector.Unprotect(settings.GitHubTokenEncrypted);
    }
}
