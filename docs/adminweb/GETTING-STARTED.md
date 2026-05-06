# Getting Started

A five-minute walkthrough: install AdminWeb, log in, add a server, build the app, and deploy it.

## Prerequisites

- A Linux host (Fedora/RHEL/Debian) with .NET 8 SDK installed.
- SSH access to at least one target server where VPN Dashboard will run.

## 1. Install AdminWeb

Run the install script on the machine that will host AdminWeb:

```bash
export VPNDASH_ADMIN_EMAIL="admin@example.com"
export VPNDASH_ADMIN_PASSWORD="YourStr0ngPass"
sudo -E bash deploy/install-adminweb.sh
```

This creates the systemd service, database, and Data Protection keyring. See [INSTALL-LINUX.md](INSTALL-LINUX.md) for the full production setup.

## 2. Log in

Open `https://admin.example.com` (or `http://<host>:5050` in development). Sign in with the admin email and password you set in step 1.

## 3. Add a target server

1. Click **Servers** in the sidebar.
2. Click **Add Server**.
3. Fill in the connection details:
   - **Name** — a human-readable label (e.g. `us-east-1`).
   - **Tier** — Free or Paid.
   - **Host / Port** — the SSH host and port (default 22).
   - **Username / Password** — SSH credentials.
   - **Install Dir** — where the app will be extracted (default `/opt/vpn-dashboard`).
   - **Service Name** — the systemd unit name (default `vpn-dashboard`).
   - **Service User** — the OS user that owns the files (default `vpndash`).
4. Click **Save**, then **Test Connection** to verify SSH access.

## 4. Configure build settings

1. Click **Settings → Build** in the sidebar (or navigate to `/settings/build`).
2. Enter the repository URL. For a private repo, also enter a GitHub username and personal access token.
3. Set the project path (default: `src/VPNDashboard.Website/VPNDashboard.Website.csproj`).
4. Click **Save**.

## 5. Build the main branch

1. Click **Build** in the sidebar.
2. Leave the branch field as `main` (or type another branch name).
3. Click **Fetch & Preview** to see the latest commit on that branch.
4. Click **Build & Package**. A live log panel streams `dotnet publish` output.
5. When the build completes, an artifact (e.g. `vpndashboard-main-a1b2c3d.tar.gz`) appears in the artifacts list.

## 6. Deploy to a server

1. Click **Artifacts** in the sidebar (or stay on the build page).
2. Find the artifact you just built.
3. Click **Deploy**, then pick the target server.
4. The deploy log streams in real time: upload via SCP, stop service, extract, chown, start service.
5. The server's status on the dashboard updates to **active** once the deploy finishes.

You're done. Repeat steps 5–6 whenever you need to ship an update.
