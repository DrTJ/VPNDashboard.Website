using System.ComponentModel.DataAnnotations;

namespace VPNDashboard.Website.Models;

public class Subscription
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string ProfileName { get; set; } = string.Empty;

    public ScheduleType ScheduleType { get; set; } = ScheduleType.Unlimited;

    /// <summary>When the subscription should become active. Null means "immediately".</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>For Periodic schedules: when the subscription expires and the profile is revoked.</summary>
    public DateTime? EndDate { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the underlying OpenVPN profile is actually issued.</summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>Set when the underlying OpenVPN profile is revoked (manually or by expiry).</summary>
    public DateTime? RevokedAt { get; set; }

    [MaxLength(256)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Last error encountered by the scheduler when trying to activate / expire this subscription.</summary>
    [MaxLength(1000)]
    public string? LastError { get; set; }
}

public enum ScheduleType
{
    Unlimited = 0,
    Periodic = 1,
}

public enum SubscriptionStatus
{
    /// <summary>Scheduled to start in the future; OpenVPN profile not yet issued.</summary>
    Pending = 0,

    /// <summary>OpenVPN profile is issued and currently valid.</summary>
    Active = 1,

    /// <summary>Periodic subscription reached its EndDate and the profile was revoked.</summary>
    Expired = 2,

    /// <summary>Manually revoked by an administrator.</summary>
    Revoked = 3,
}
