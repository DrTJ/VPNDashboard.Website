using Microsoft.EntityFrameworkCore;
using VPNDashboard.Website.Data;
using VPNDashboard.Website.Models;

namespace VPNDashboard.Website.Services;

public class SubscriptionService
{
    private readonly AppDbContext _db;
    private readonly OpenVpnAdmin _vpnAdmin;
    private readonly OpenVpnReader _vpnReader;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(
        AppDbContext db,
        OpenVpnAdmin vpnAdmin,
        OpenVpnReader vpnReader,
        ILogger<SubscriptionService> logger)
    {
        _db = db;
        _vpnAdmin = vpnAdmin;
        _vpnReader = vpnReader;
        _logger = logger;
    }

    public Task<List<Subscription>> GetAllAsync(CancellationToken ct = default)
        => _db.Subscriptions
            .AsNoTracking()
            .OrderByDescending(s => s.Status == SubscriptionStatus.Active)
            .ThenByDescending(s => s.Status == SubscriptionStatus.Pending)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public Task<Subscription?> GetAsync(int id, CancellationToken ct = default)
        => _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Subscription?> GetActiveByProfileAsync(string profileName, CancellationToken ct = default)
        => _db.Subscriptions
            .FirstOrDefaultAsync(s => s.ProfileName == profileName
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending), ct);

    public record CreateResult(bool Success, string? Error, Subscription? Subscription);

    /// <summary>
    /// Creates a subscription. If StartDate is null or already passed, the OpenVPN profile is issued
    /// immediately and the subscription is marked Active. Otherwise it is left Pending and will be
    /// activated by the scheduler when StartDate arrives.
    /// </summary>
    public async Task<CreateResult> CreateAsync(
        string profileName,
        ScheduleType scheduleType,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        string? notes,
        string? createdByUserId,
        CancellationToken ct = default)
    {
        var sanitized = OpenVpnAdmin.SanitizeClientName(profileName);
        if (string.IsNullOrWhiteSpace(sanitized))
            return new CreateResult(false, "Invalid profile name.", null);

        if (scheduleType == ScheduleType.Periodic)
        {
            if (endDateUtc is null)
                return new CreateResult(false, "Periodic subscriptions require an end date.", null);
            var effectiveStart = startDateUtc ?? DateTime.UtcNow;
            if (endDateUtc <= effectiveStart)
                return new CreateResult(false, "End date must be after the start date.", null);
        }
        else
        {
            // Unlimited never has an end date
            endDateUtc = null;
        }

        // Disallow duplicate live subscriptions for the same profile name
        var existing = await _db.Subscriptions.AnyAsync(s => s.ProfileName == sanitized
            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending), ct);
        if (existing)
            return new CreateResult(false, $"A subscription for '{sanitized}' is already active or pending.", null);

        // Disallow if there is already an issued OpenVPN profile with that name (and we'd issue immediately)
        var nowUtc = DateTime.UtcNow;
        var willIssueImmediately = startDateUtc is null || startDateUtc <= nowUtc;
        if (willIssueImmediately)
        {
            var existingProfile = _vpnReader.GetClientProfiles()
                .FirstOrDefault(p => string.Equals(p.Name, sanitized, StringComparison.Ordinal));
            if (existingProfile != null && existingProfile.Status == ClientStatus.Valid)
                return new CreateResult(false,
                    $"An OpenVPN profile named '{sanitized}' already exists. Use a different name or revoke the existing profile first.",
                    null);
        }

        var subscription = new Subscription
        {
            ProfileName = sanitized,
            ScheduleType = scheduleType,
            StartDate = startDateUtc,
            EndDate = endDateUtc,
            Status = SubscriptionStatus.Pending,
            Notes = notes,
            CreatedByUserId = createdByUserId,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        if (willIssueImmediately)
        {
            var (ok, output) = await _vpnAdmin.AddClientAsync(sanitized);
            if (ok)
            {
                subscription.Status = SubscriptionStatus.Active;
                subscription.ActivatedAt = nowUtc;
                subscription.UpdatedAt = nowUtc;
                subscription.LastError = null;
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                subscription.LastError = Truncate(output, 1000);
                subscription.UpdatedAt = nowUtc;
                await _db.SaveChangesAsync(ct);
                return new CreateResult(false, $"Failed to issue OpenVPN profile: {output}", subscription);
            }
        }

        return new CreateResult(true, null, subscription);
    }

    public record UpdateResult(bool Success, string? Error);

    /// <summary>
    /// Updates a Pending or Active subscription's schedule fields. Profile name is immutable.
    /// </summary>
    public async Task<UpdateResult> UpdateScheduleAsync(
        int id,
        ScheduleType scheduleType,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        string? notes,
        CancellationToken ct = default)
    {
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null) return new UpdateResult(false, "Subscription not found.");

        if (sub.Status != SubscriptionStatus.Pending && sub.Status != SubscriptionStatus.Active)
            return new UpdateResult(false, "Only pending or active subscriptions can be edited.");

        if (scheduleType == ScheduleType.Periodic)
        {
            if (endDateUtc is null)
                return new UpdateResult(false, "Periodic subscriptions require an end date.");
            var effectiveStart = sub.Status == SubscriptionStatus.Active
                ? (sub.ActivatedAt ?? sub.StartDate ?? sub.CreatedAt)
                : (startDateUtc ?? DateTime.UtcNow);
            if (endDateUtc <= effectiveStart)
                return new UpdateResult(false, "End date must be after the start date.");
        }
        else
        {
            endDateUtc = null;
        }

        // Cannot change the start date of an already-active subscription
        if (sub.Status == SubscriptionStatus.Pending)
        {
            sub.StartDate = startDateUtc;
        }

        sub.ScheduleType = scheduleType;
        sub.EndDate = endDateUtc;
        sub.Notes = notes;
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return new UpdateResult(true, null);
    }

    /// <summary>
    /// Cancels a subscription. If the OpenVPN profile is currently issued, revokes it via the helper.
    /// </summary>
    public async Task<UpdateResult> CancelAsync(int id, CancellationToken ct = default)
    {
        var sub = await _db.Subscriptions.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (sub is null) return new UpdateResult(false, "Subscription not found.");

        if (sub.Status is SubscriptionStatus.Revoked or SubscriptionStatus.Expired)
            return new UpdateResult(true, null);

        var now = DateTime.UtcNow;

        if (sub.Status == SubscriptionStatus.Active)
        {
            var (ok, output) = await _vpnAdmin.RevokeClientAsync(sub.ProfileName);
            if (!ok)
            {
                sub.LastError = Truncate(output, 1000);
                sub.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
                return new UpdateResult(false, $"Failed to revoke OpenVPN profile: {output}");
            }
            sub.RevokedAt = now;
        }

        sub.Status = SubscriptionStatus.Revoked;
        sub.UpdatedAt = now;
        sub.LastError = null;
        await _db.SaveChangesAsync(ct);
        return new UpdateResult(true, null);
    }

    /// <summary>
    /// Marks any active subscription for the given profile as revoked (used when revocation is performed
    /// directly from the Clients page, outside the normal Cancel flow).
    /// </summary>
    public async Task MarkRevokedByProfileAsync(string profileName, CancellationToken ct = default)
    {
        var sanitized = OpenVpnAdmin.SanitizeClientName(profileName);
        var now = DateTime.UtcNow;
        var subs = await _db.Subscriptions
            .Where(s => s.ProfileName == sanitized
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Pending))
            .ToListAsync(ct);
        foreach (var s in subs)
        {
            s.Status = SubscriptionStatus.Revoked;
            s.RevokedAt = s.Status == SubscriptionStatus.Active ? now : s.RevokedAt;
            s.UpdatedAt = now;
        }
        if (subs.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : (value.Length <= max ? value : value[..max]);
}
