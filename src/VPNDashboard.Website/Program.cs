using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VPNDashboard.Website.Components;
using VPNDashboard.Website.Data;
using VPNDashboard.Website.Hubs;
using VPNDashboard.Website.Models;
using VPNDashboard.Website.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<OpenVpnSettings>(builder.Configuration.GetSection("OpenVpn"));

// Database
var dbPath = builder.Configuration.GetValue<string>("ConnectionStrings:DefaultConnection")
    ?? "Data Source=/var/lib/vpn-dashboard/identity.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(dbPath));

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
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// Services
builder.Services.AddScoped<OpenVpnReader>();
builder.Services.AddScoped<OpenVpnAdmin>();
builder.Services.AddScoped<OpenVpnInstaller>();
builder.Services.AddHostedService<ConnectedClientsBackgroundService>();

// SignalR
builder.Services.AddSignalR();

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Ensure database is created and seed admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

    var adminEmail = Environment.GetEnvironmentVariable("VPNDASH_ADMIN_EMAIL");
    var adminPassword = Environment.GetEnvironmentVariable("VPNDASH_ADMIN_PASSWORD");

    if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
    {
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

// Redirect to login if not authenticated, or to setup if OpenVPN is not installed
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

    // Always allow these paths
    if (path.StartsWith("/account/") ||
        path.StartsWith("/docs") ||
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

    // If not authenticated, let the auth middleware handle it
    if (context.User.Identity?.IsAuthenticated != true)
    {
        await next();
        return;
    }

    // Redirect to setup if OpenVPN is not installed
    if (path != "/setup" && path != "/error")
    {
        using var scope = context.RequestServices.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<OpenVpnReader>();
        if (!reader.IsInstalled)
        {
            context.Response.Redirect("/setup");
            return;
        }
    }

    await next();
});

// Logout endpoint
app.MapPost("/account/logout", async (HttpContext context, SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    context.Response.Redirect("/account/login");
});

// Download .ovpn file endpoint
app.MapGet("/api/download/{clientName}.ovpn", (string clientName, OpenVpnReader reader, HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var content = reader.BuildOvpnFile(clientName);
    if (content == null)
        return Results.NotFound();

    return Results.File(
        System.Text.Encoding.UTF8.GetBytes(content),
        "application/x-openvpn-profile",
        $"{clientName}.ovpn");
}).RequireAuthorization();

// SignalR hubs
app.MapHub<ConnectedClientsHub>("/hubs/connected-clients");
app.MapHub<InstallerHub>("/hubs/installer");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
