# Building

AdminWeb builds VPN Dashboard from source and packages it into a deployable tarball. The build page provides branch selection, commit preview, and live log streaming.

## Workflow

### 1. Enter a branch name

Type a branch name in the input field (defaults to the branch configured in build settings). Any remote branch works.

### 2. Fetch & Preview

Click **Fetch & Preview** to pull the latest changes and see the head commit on that branch:

- **SHA** — short (7-char) and full commit hash.
- **Author** — commit author name.
- **Date** — author date (ISO 8601).
- **Message** — commit subject line.

This runs `git fetch --all --prune` followed by `git log -1 ... origin/{branch}`.

### 3. Build & Package

Click **Build & Package** to start the build. The following steps run automatically:

1. **Clone or update** — if the repo doesn't exist locally, it is cloned to `{Build:WorkDir}/repo`. If it exists, the remote URL is verified and updated if needed.
2. **Fetch** — `git fetch --all --prune`.
3. **Checkout** — `git checkout {branch}` then `git reset --hard origin/{branch}`.
4. **Publish** — `dotnet publish {ProjectPath} -c Release -o {tempDir} --nologo`.
5. **Strip debug files** — all `.pdb` files and `appsettings.Development.json` are deleted from the output.
6. **Package** — `tar -czf {ArtifactDir}/{tarName} -C {tempDir} .`

### Artifact naming

Artifacts follow this pattern:

```
vpndashboard-{branch}-{shortsha}.tar.gz
```

Examples:
- `vpndashboard-main-a1b2c3d.tar.gz`
- `vpndashboard-feature/auth-f9e8d7c.tar.gz`

If a build for the same branch and commit SHA already exists, the artifact record is updated rather than duplicated.

## Live log streaming

Build output (stdout and stderr from `dotnet publish`, `git`, and `tar`) is streamed in real time to the browser via the `LiveLogHub` SignalR hub. The log panel scrolls automatically and shows the full output including any warnings or errors.

The log ends with `EXIT 0` on success or `EXIT {code}` on failure.

## Build settings

Build settings are managed at **Settings → Build** (`/settings/build`):

| Setting | Description |
|---|---|
| Repository URL | Git clone URL (HTTPS) |
| Default Branch | Pre-filled in the branch input on the build page |
| Project Path | Relative path to the `.csproj` file from the repo root |
| GitHub Username | Username for authenticated Git access (optional) |
| GitHub Token | Personal access token for private repos (encrypted at rest) |

### Private repository access

For private repositories:

1. Go to **Settings → Build**.
2. Enter your GitHub username.
3. Enter a personal access token (PAT) with `repo` scope.
4. Click **Save**.

The token is encrypted with ASP.NET Data Protection before storage. During clone and fetch, AdminWeb constructs an authenticated URL:

```
https://{username}:{token}@github.com/org/repo.git
```

The token is never logged or displayed in the UI after saving.

## Artifacts directory

Built tarballs are stored in the directory specified by `Build:ArtifactDir` (default: `/var/lib/vpndashboard-admin/artifacts`). Each artifact is recorded in the database with its filename, branch, commit SHA, author, date, build timestamp, and file size.

See [Deploying](DEPLOYING.md) for how to deploy an artifact to a server.
