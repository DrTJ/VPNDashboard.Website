using Microsoft.EntityFrameworkCore;
using VPNDashboard.Website.Data;
using VPNDashboard.Website.Models;

namespace VPNDashboard.Website.Services;

/// <summary>
/// Periodically activates Pending subscriptions whose StartDate has arrived and revokes Active
/// Periodic subscriptions whose EndDate has passed.
/// </summary>
public class SubscriptionScheduler : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<SubscriptionScheduler> _logger;

    public SubscriptionScheduler(IServiceProvider services, ILogger<SubscriptionScheduler> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so the host is fully up before we touch the DB / helper.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Subscription scheduler tick failed");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var vpnAdmin = scope.ServiceProvider.GetRequiredService<OpenVpnAdmin>();

        var nowUtc = DateTime.UtcNow;

        var pendingDue = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Pending
                && (s.StartDate == null || s.StartDate <= nowUtc))
            .ToListAsync(ct);

        foreach (var sub in pendingDue)
        {
            ct.ThrowIfCancellationRequested();
            var (ok, output) = await vpnAdmin.AddClientAsync(sub.ProfileName);
            if (ok)
            {
                sub.Status = SubscriptionStatus.Active;
                sub.ActivatedAt = DateTime.UtcNow;
                sub.UpdatedAt = sub.ActivatedAt.Value;
                sub.LastError = null;
                _logger.LogInformation("Activated subscription {Id} ({Name})", sub.Id, sub.ProfileName);
            }
            else
            {
                sub.LastError = Truncate(output, 1000);
                sub.UpdatedAt = DateTime.UtcNow;
                _logger.LogWarning("Failed to activate subscription {Id} ({Name}): {Output}",
                    sub.Id, sub.ProfileName, output);
            }
        }

        var expiredDue = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active
                && s.ScheduleType == ScheduleType.Periodic
                && s.EndDate != null
                && s.EndDate <= nowUtc)
            .ToListAsync(ct);

        foreach (var sub in expiredDue)
        {
            ct.ThrowIfCancellationRequested();
            var (ok, output) = await vpnAdmin.RevokeClientAsync(sub.ProfileName);
            if (ok)
            {
                sub.Status = SubscriptionStatus.Expired;
                sub.RevokedAt = DateTime.UtcNow;
                sub.UpdatedAt = sub.RevokedAt.Value;
                sub.LastError = null;
                _logger.LogInformation("Expired subscription {Id} ({Name})", sub.Id, sub.ProfileName);
            }
            else
            {
                sub.LastError = Truncate(output, 1000);
                sub.UpdatedAt = DateTime.UtcNow;
                _logger.LogWarning("Failed to expire subscription {Id} ({Name}): {Output}",
                    sub.Id, sub.ProfileName, output);
            }
        }

        if (pendingDue.Count > 0 || expiredDue.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : (value.Length <= max ? value : value[..max]);
}
