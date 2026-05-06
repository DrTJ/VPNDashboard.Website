# Servers

AdminWeb manages a list of target servers where VPN Dashboard is deployed. Each server is an SSH-accessible Linux host running the VPN Dashboard as a systemd service.

## Server fields

| Field | Type | Default | Description |
|---|---|---|---|
| Name | string | — | Human-readable label (e.g. `us-east-1`) |
| Tier | enum | Free | `Free` or `Paid` — used to group servers on the dashboard |
| Host | string | — | SSH hostname or IP address |
| Port | int | 22 | SSH port |
| Username | string | — | SSH login user |
| Password | string | — | SSH password (encrypted at rest, see [Security](SECURITY.md)) |
| InstallDir | string | `/opt/vpn-dashboard` | Directory where the app is extracted on deploy |
| ServiceName | string | `vpn-dashboard` | Name of the systemd unit |
| ServiceUser | string | `vpndash` | OS user that owns the application files |
| Notes | string | — | Free-text notes (optional) |

## Adding a server

1. Navigate to **Servers** in the sidebar.
2. Click **Add Server**.
3. Fill in all required fields and click **Save**.

The password is encrypted using ASP.NET Data Protection before being stored in SQLite.

## Editing a server

Click a server row to open the edit form. Leave the password field blank to keep the existing password. Enter a new value to replace it.

## Deleting a server

Click **Delete** on the server row. This removes the server record from the database. It does not affect anything on the remote host.

## Test connection

After saving a server, click **Test Connection**. AdminWeb will open an SSH session to the host and run `systemctl is-active {ServiceName}`. The result appears as a status badge:

- **active** — the service is running.
- **inactive** — the service exists but is stopped.
- **unknown** — `systemctl` could not determine the state.
- **unreachable** — SSH connection failed.

Status is cached for 30 seconds to avoid flooding target servers with SSH connections.

## Tier grouping

The dashboard groups servers by tier. This lets you see Free and Paid servers at a glance and deploy to them selectively.

## Sudoers configuration on target servers

The SSH user on each target server needs passwordless `sudo` for the deploy commands. Add this to `/etc/sudoers.d/vpndash` on the target:

```
vpndash ALL=(ALL) NOPASSWD: /usr/bin/systemctl stop vpn-dashboard, /usr/bin/systemctl start vpn-dashboard, /usr/bin/systemctl status vpn-dashboard, /usr/bin/mkdir, /usr/bin/tar, /usr/bin/chown
```

Replace `vpndash` with your SSH username and `vpn-dashboard` with your service name if they differ. Apply with:

```bash
sudo visudo -f /etc/sudoers.d/vpndash
sudo chmod 0440 /etc/sudoers.d/vpndash
```

Without this, deploy commands will fail with a `sudo: a password is required` error.

## Tracked deploy metadata

After each deploy, AdminWeb updates the server record with:

| Field | Description |
|---|---|
| LastDeployedAt | UTC timestamp of the last deploy |
| LastDeployedCommitSha | Full commit SHA of the deployed artifact |
| LastDeployedArtifactName | Filename of the deployed tarball |

This data is shown on the dashboard and server detail pages so you can tell at a glance what version each server is running.
