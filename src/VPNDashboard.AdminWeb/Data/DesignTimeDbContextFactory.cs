using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VPNDashboard.AdminWeb.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AdminDbContext>
{
    public AdminDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new AdminDbContext(options);
    }
}
