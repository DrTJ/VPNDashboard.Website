# Configuration

All configuration lives in `appsettings.json`. Every key can be overridden with environment variables using the `__` (double-underscore) separator.

## Full reference

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/lib/vpndashboard-admin/admin.db"
  },
  "DataProtection": {
    "KeyDirectory": "/var/lib/vpndashboard-admin/keys"
  },
  "Build": {
    "WorkDir": "/var/lib/vpndashboard-admin/build",
    "ArtifactDir": "/var/lib/vpndashboard-admin/artifacts",
    "Seed": {
      "RepositoryUrl": "https://github.com/DrTJ/VPNDashboard.Website.git",
      "DefaultBranch": "main",
      "ProjectPath": "src/VPNDashboard.Website/VPNDashboard.Website.csproj"
    }
  },
  "Docs": {
    "Path": "docs"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5050"
      }
    }
  }
}
```

## Key descriptions

### ConnectionStrings:DefaultConnection

SQLite connection string. The database file is created automatically on first run. Default path: `/var/lib/vpndashboard-admin/admin.db`.

```bash
# Environment override
export ConnectionStrings__DefaultConnection="Data Source=/custom/path/admin.db"
```

### DataProtection:KeyDirectory

Directory where ASP.NET Data Protection stores its XML key files. These keys encrypt server passwords and Git tokens. Default: `/var/lib/vpndashboard-admin/keys`.

Back up this directory. If the keys are lost, all encrypted credentials become unrecoverable.

### Build:WorkDir

Working directory for the Git clone. The repository is cloned to `{WorkDir}/repo`. Default: `/var/lib/vpndashboard-admin/build`.

### Build:ArtifactDir

Directory where build artifacts (`.tar.gz` files) are stored. Default: `/var/lib/vpndashboard-admin/artifacts`.

### Build:Seed:RepositoryUrl

Git repository URL used to seed the build settings on first run. Only applied when the database row has an empty `RepositoryUrl`. Default: `https://github.com/DrTJ/VPNDashboard.Website.git`.

### Build:Seed:DefaultBranch

Default branch name used to seed build settings. Default: `main`.

### Build:Seed:ProjectPath

Relative path (from repo root) to the `.csproj` file used by `dotnet publish`. Default: `src/VPNDashboard.Website/VPNDashboard.Website.csproj`.

### Docs:Path

Relative path to the documentation directory served by the in-app docs viewer. Default: `docs`.

### Kestrel:Endpoints:Http:Url

The URL Kestrel binds to. Default: `http://0.0.0.0:5050`. In production, keep this as an internal binding and put nginx in front of it.

```bash
# Listen on a different port
export Kestrel__Endpoints__Http__Url="http://0.0.0.0:8080"
```

## Environment variable overrides

ASP.NET configuration supports environment variable overrides using the `__` separator to represent nesting:

| JSON path | Environment variable |
|---|---|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` |
| `DataProtection:KeyDirectory` | `DataProtection__KeyDirectory` |
| `Build:WorkDir` | `Build__WorkDir` |
| `Build:ArtifactDir` | `Build__ArtifactDir` |
| `Build:Seed:RepositoryUrl` | `Build__Seed__RepositoryUrl` |
| `Kestrel:Endpoints:Http:Url` | `Kestrel__Endpoints__Http__Url` |

Environment variables can be set in the systemd unit file:

```ini
[Service]
Environment=Build__ArtifactDir=/mnt/data/artifacts
Environment=ConnectionStrings__DefaultConnection=Data Source=/mnt/data/admin.db
```
