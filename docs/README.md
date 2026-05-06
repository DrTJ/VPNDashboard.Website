# VPN Dashboard

A two-project solution for deploying and centrally managing OpenVPN servers, built with .NET 8, Blazor Server, and AdminLTE 4.

## Projects

### VPNDashboard.Website

`src/VPNDashboard.Website/`

Blazor Server app that runs on each VPN server. Manages OpenVPN directly: client profiles, real-time connected-client monitoring, subscriptions, and a server setup wizard.

- **Docs:** [`docs/website/`](website/)

### VPNDashboard.AdminWeb

`src/VPNDashboard.AdminWeb/`

Blazor Server admin panel for centrally managing multiple VPN Dashboard deployments. Server inventory with Free/Paid tiers, build from GitHub, one-click deploy over SSH, and role-based access (Admin / Operator / Viewer).

- **Docs:** [`docs/adminweb/`](adminweb/)

## Solution Structure

```
VPNDashboard.Website.sln
├── src/
│   ├── VPNDashboard.Website/        # Per-server dashboard
│   │   ├── Components/Pages/        # Blazor pages
│   │   ├── Data/                    # EF Core + SQLite
│   │   ├── Models/                  # Domain models
│   │   ├── Services/                # OpenVPN integration
│   │   └── wwwroot/                 # AdminLTE 4 assets
│   └── VPNDashboard.AdminWeb/       # Central admin panel
│       ├── Components/Pages/        # Blazor pages
│       ├── Data/                    # EF Core + SQLite
│       ├── Hubs/                    # SignalR (live build logs)
│       ├── Models/                  # Domain models
│       ├── Services/                # Build, deploy, SSH
│       └── wwwroot/                 # AdminLTE 4 assets
├── deploy/                          # Install scripts
└── docs/                            # Documentation (this folder)
    ├── website/                     # Website-specific docs
    └── adminweb/                    # AdminWeb-specific docs
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Framework | .NET 8, ASP.NET Core |
| UI | Blazor Server, AdminLTE 4 (Bootstrap 5) |
| Database | EF Core with SQLite |
| Auth | ASP.NET Core Identity |
| Real-time | SignalR |
| Remote ops | SSH.NET (AdminWeb only) |

## Quick Start

Both projects require the .NET 8 SDK to build.

**VPNDashboard.Website:**

```bash
dotnet run --project src/VPNDashboard.Website
```

**VPNDashboard.AdminWeb:**

```bash
dotnet run --project src/VPNDashboard.AdminWeb
```

**Publish for deployment:**

```bash
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj -c Release -o publish/website
dotnet publish src/VPNDashboard.AdminWeb/VPNDashboard.AdminWeb.csproj -c Release -o publish/adminweb
```

## Documentation

### General

- [Installation — Fedora](INSTALL-FEDORA.md)
- [Installation — Ubuntu / Debian](INSTALL-UBUNTU.md)
- [Configuration](CONFIGURATION.md)
- [Security](SECURITY.md)
- [Operations](OPERATIONS.md)
- [Uninstall](UNINSTALL.md)
- [Style Guide](STYLE.md)

### OpenVPN

- [OpenVPN Setup Wizard](OPENVPN-SETUP-WIZARD.md)
- [OpenVPN Uninstall](OPENVPN-UNINSTALL.md)

## License

MIT
