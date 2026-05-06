# VPNDashboard AdminWeb

VPNDashboard AdminWeb is a Blazor Server admin panel for managing VPN Dashboard deployments across multiple servers.  It provides a single place to build, package, and deploy the VPN Dashboard application to any number of target servers over SSH.

## Tech stack

| Layer | Technology |
|---|---|
| UI | Blazor Server (.NET 8) |
| Auth | ASP.NET Core Identity (cookie-based) |
| Database | EF Core + SQLite |
| SSH | SSH.NET (password auth) |
| Real-time logs | SignalR (`LiveLogHub`) |
| Credential encryption | ASP.NET Data Protection |

## Features

- **Multi-server management** — register Free and Paid tier servers, monitor service status in real time.
- **One-click builds** — fetch any branch, preview the commit, then build and package a release tarball.
- **One-click deploys** — upload an artifact and run the full stop/extract/chown/start cycle remotely.
- **Live log streaming** — build and deploy output is streamed to the browser via SignalR.
- **Role-based access** — Admin, Operator, and Viewer roles restrict who can do what.
- **Encrypted credentials** — server passwords and Git tokens are encrypted at rest with ASP.NET Data Protection.

## Documentation

| Document | Description |
|---|---|
| [Getting Started](GETTING-STARTED.md) | Five-minute walkthrough from install to first deploy |
| [Installation (Linux)](INSTALL-LINUX.md) | Production install with nginx, TLS, and firewall |
| [Configuration](CONFIGURATION.md) | Every `appsettings.json` key explained |
| [Servers](SERVERS.md) | Adding, editing, and managing target servers |
| [Building](BUILDING.md) | Branch selection, build workflow, artifact naming |
| [Deploying](DEPLOYING.md) | One-click deploy flow and remote commands |
| [Users & Roles](USERS-AND-ROLES.md) | Role definitions, permissions, and user management |
| [Security](SECURITY.md) | Encryption model, key management, HTTPS |
| [Troubleshooting](TROUBLESHOOTING.md) | Common issues and fixes |
| [Architecture](ARCHITECTURE.md) | Service breakdown and high-level design |
