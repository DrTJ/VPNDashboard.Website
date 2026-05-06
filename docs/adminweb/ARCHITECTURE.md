# Architecture

## High-Level Overview

VPNDashboard.AdminWeb is a Blazor Server application that centrally manages deployments of VPNDashboard.Website across multiple Linux servers.

```
Browser (Admin)
    │
    ▼ HTTPS
┌──────────────────────────────────────────────┐
│  VPNDashboard.AdminWeb (Blazor Server)       │
│                                              │
│  ┌────────────┐  ┌──────────────────────┐    │
│  │ ASP.NET    │  │ SignalR (LiveLogHub)  │    │
│  │ Identity   │  │ - build logs         │    │
│  │            │  │ - deploy logs        │    │
│  └────────────┘  └──────────────────────┘    │
│                                              │
│  ┌────────────┐  ┌──────────────────────┐    │
│  │ EF Core    │  │ Data Protection      │    │
│  │ SQLite     │  │ (password encryption) │    │
│  │ admin.db   │  │ /keys/               │    │
│  └────────────┘  └──────────────────────┘    │
│                                              │
│  ┌────────────┐  ┌──────────────────────┐    │
│  │ Git CLI    │  │ SSH.NET              │    │
│  │ (subprocess│  │ (SCP upload +        │    │
│  │  for build)│  │  remote commands)    │    │
│  └────────────┘  └──────────────────────┘    │
└──────────────────────────────────────────────┘
       │                    │
       ▼                    ▼
┌─────────────┐   ┌─────────────────────┐
│ Git Repo    │   │ Target Server #1    │
│ (GitHub)    │   │ Target Server #2    │
└─────────────┘   │ Target Server #N    │
                  └─────────────────────┘
```

## Data Flow

### Build Flow

1. Admin enters a branch name on the Build page
2. `IGitWorkspace.EnsureClonedAsync()` checks if the repo is cloned; re-clones if the URL changed
3. `IGitWorkspace.FetchAsync()` runs `git fetch --all --prune`
4. `IGitWorkspace.GetHeadCommitAsync()` returns commit details for confirmation
5. `IBuildService.BuildAsync()` runs the full pipeline:
   - `git checkout {branch} && git reset --hard origin/{branch}`
   - `dotnet publish -c Release -o {buildOut}`
   - Strips `*.pdb` and `appsettings.Development.json`
   - `tar -czf {artifactDir}/vpndashboard-{branch}-{shortsha}.tar.gz`
6. A `BuildArtifact` row is persisted in SQLite
7. All output is streamed to the browser via `IProgress<string>`

### Deploy Flow

1. Admin selects an artifact and clicks Deploy on a server detail page
2. `IDeployService.DeployAsync()`:
   - Decrypts the server's SSH password using `CredentialProtector`
   - Uploads the tarball via SCP to `/tmp/vpndashboard-deploy.tar.gz`
   - Runs remote commands via SSH: stop service, extract, chown, start service
3. `TargetServer.LastDeployedAt` and related fields are updated
4. All output is streamed to the browser

## Service Interfaces

| Service | Lifetime | Responsibility |
|---------|----------|---------------|
| `IServerStore` | Scoped | CRUD for target servers, encrypts/decrypts passwords |
| `IBuildSettingsStore` | Scoped | Manages the singleton `BuildSettings` row (repo URL, token, etc.) |
| `ISshSessionFactory` | Singleton | Creates `Renci.SshNet.ConnectionInfo` from a `TargetServer` |
| `IServerStatusService` | Singleton | Probes `systemctl is-active` over SSH with 30s cache |
| `IGitWorkspace` | Scoped | Manages the persistent git clone (fetch, checkout, commit info) |
| `IBuildService` | Scoped | Orchestrates the full build pipeline |
| `IDeployService` | Scoped | Orchestrates upload + remote deploy |
| `CredentialProtector` | Singleton | Wraps ASP.NET Data Protection for reversible encryption |
| `UserAdminService` | Scoped | CRUD for Identity users with role management |
| `LiveLogHub` | SignalR Hub | Bridges `IProgress<string>` to connected browser clients |

## Database Schema

The `AdminDbContext` extends `IdentityDbContext` and adds:

- **TargetServers** — server connection details with encrypted passwords
- **BuildArtifacts** — metadata for each built tarball (branch, SHA, size, timestamps)
- **BuildSettings** — singleton row (Id=1) for repo URL, branch, project path, GitHub credentials

## Security Model

- Passwords are encrypted using ASP.NET Data Protection (AES-256-CBC with key derivation)
- The keyring at `/var/lib/vpndashboard-admin/keys/` is the master key — back it up
- GitHub tokens follow the same encryption model
- Tokens are never written to disk in plaintext
- SSH connections are established in-memory; passwords are decrypted only when needed
- Three roles control access: Admin > Operator > Viewer
