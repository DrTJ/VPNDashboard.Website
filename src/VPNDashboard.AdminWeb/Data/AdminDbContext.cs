using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Data;

public class AdminDbContext : IdentityDbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options)
    {
    }

    public DbSet<TargetServer> TargetServers => Set<TargetServer>();
    public DbSet<BuildArtifact> BuildArtifacts => Set<BuildArtifact>();
    public DbSet<BuildSettings> BuildSettings => Set<BuildSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<TargetServer>(e =>
        {
            e.HasIndex(s => s.Host);
            e.HasIndex(s => s.Tier);
        });

        builder.Entity<BuildArtifact>(e =>
        {
            e.HasIndex(a => a.CommitSha);
            e.HasIndex(a => a.Branch);
        });

        builder.Entity<BuildSettings>(e =>
        {
            e.HasData(new BuildSettings { Id = 1 });
        });
    }
}
