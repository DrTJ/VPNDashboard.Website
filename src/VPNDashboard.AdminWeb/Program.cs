using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VPNDashboard.AdminWeb.Components;
using VPNDashboard.AdminWeb.Data;
using VPNDashboard.AdminWeb.Hubs;
using VPNDashboard.AdminWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=/var/lib/vpndashboard-admin/admin.db";
builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseSqlite(connStr));

// Data Protection (reversible encryption for stored passwords)
var keyDir = builder.Configuration.GetValue<string>("DataProtection:KeyDirectory")
    ?? "/var/lib/vpndashboard-admin/keys";
Directory.CreateDirectory(keyDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyDir))
    .SetApplicationName("VPNDashboard.AdminWeb");

// Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AdminDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("AdminOrOperator", p => p.RequireRole("Admin", "Operator"));
});

// Services
builder.Services.AddSingleton<CredentialProtector>();
builder.Services.AddScoped<IServerStore, ServerStore>();
builder.Services.AddScoped<IBuildSettingsStore, BuildSettingsStore>();
builder.Services.AddSingleton<ISshSessionFactory, SshSessionFactory>();
builder.Services.AddSingleton<IServerStatusService, ServerStatusService>();
builder.Services.AddScoped<IGitWorkspace, GitWorkspace>();
builder.Services.AddScoped<IBuildService, BuildService>();
builder.Services.AddScoped<IDeployService, DeployService>();
builder.Services.AddScoped<UserAdminService>();

// SignalR
builder.Services.AddSignalR();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Ensure database and seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    db.Database.Migrate();

    // Seed BuildSettings from appsettings if empty
    var settings = await db.BuildSettings.FindAsync(1);
    if (settings != null && string.IsNullOrEmpty(settings.RepositoryUrl))
    {
        settings.RepositoryUrl = builder.Configuration.GetValue<string>("Build:Seed:RepositoryUrl")
            ?? "https://github.com/DrTJ/VPNDashboard.Website.git";
        settings.DefaultBranch = builder.Configuration.GetValue<string>("Build:Seed:DefaultBranch") ?? "main";
        settings.ProjectPath = builder.Configuration.GetValue<string>("Build:Seed:ProjectPath")
            ?? "src/VPNDashboard.Website/VPNDashboard.Website.csproj";
        settings.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // Seed roles
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Operator", "Viewer" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Seed admin user from env vars
    var adminEmail = Environment.GetEnvironmentVariable("VPNDASH_ADMIN_EMAIL");
    var adminPassword = Environment.GetEnvironmentVariable("VPNDASH_ADMIN_PASSWORD");
    if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var existingUser = await userManager.FindByEmailAsync(adminEmail);
        if (existingUser == null)
        {
            var admin = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                app.Logger.LogInformation("Seeded admin user: {Email}", adminEmail);
            }
            else
            {
                app.Logger.LogWarning("Failed to seed admin: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Redirect to login if not authenticated
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

    if (path.StartsWith("/account/") ||
        path.StartsWith("/_framework") ||
        path.StartsWith("/_blazor") ||
        path.StartsWith("/hubs/") ||
        path.StartsWith("/css") ||
        path.StartsWith("/js") ||
        path.StartsWith("/lib") ||
        path == "/favicon.png")
    {
        await next();
        return;
    }

    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    await next();
});

// Logout endpoint
app.MapPost("/account/logout", async (HttpContext context, SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    context.Response.Redirect("/account/login");
});

// SignalR hubs
app.MapHub<LiveLogHub>("/hubs/live-log");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
