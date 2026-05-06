using Microsoft.EntityFrameworkCore;
using VPNDashboard.AdminWeb.Data;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Services;

public interface IServerStore
{
    Task<List<TargetServer>> GetAllAsync();
    Task<TargetServer?> GetByIdAsync(int id);
    Task<TargetServer> CreateAsync(TargetServer server, string plainPassword);
    Task UpdateAsync(TargetServer server, string? newPassword = null);
    Task DeleteAsync(int id);
}

public class ServerStore : IServerStore
{
    private readonly AdminDbContext _db;
    private readonly CredentialProtector _protector;

    public ServerStore(AdminDbContext db, CredentialProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<List<TargetServer>> GetAllAsync()
    {
        return await _db.TargetServers
            .OrderBy(s => s.Tier)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<TargetServer?> GetByIdAsync(int id)
    {
        return await _db.TargetServers.FindAsync(id);
    }

    public async Task<TargetServer> CreateAsync(TargetServer server, string plainPassword)
    {
        server.PasswordEncrypted = _protector.Protect(plainPassword);
        server.CreatedAt = DateTime.UtcNow;
        _db.TargetServers.Add(server);
        await _db.SaveChangesAsync();
        return server;
    }

    public async Task UpdateAsync(TargetServer server, string? newPassword = null)
    {
        if (!string.IsNullOrEmpty(newPassword))
            server.PasswordEncrypted = _protector.Protect(newPassword);

        _db.TargetServers.Update(server);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var server = await _db.TargetServers.FindAsync(id);
        if (server != null)
        {
            _db.TargetServers.Remove(server);
            await _db.SaveChangesAsync();
        }
    }

    public string DecryptPassword(TargetServer server)
    {
        return _protector.Unprotect(server.PasswordEncrypted);
    }
}
