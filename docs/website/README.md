# VPN Dashboard

A web-based management dashboard for OpenVPN servers, built with ASP.NET Core 8 Blazor Server and AdminLTE 4.

## Features

- **Install/Uninstall OpenVPN** directly from the web UI (uses Nyr's openvpn-install script)
- **Manage client profiles** — add, revoke, and download `.ovpn` configuration files
- **Live connected clients** — real-time view of who's connected (auto-refreshes every 5 seconds)
- **Server management** — view configuration, reload service, read journal logs
- **Secure** — ASP.NET Core Identity with SQLite, unprivileged service user, sudoers-whitelisted helper script
- **AdminLTE 4 UI** — polished admin interface with Bootstrap 5

## Quick Start

**On your development machine** (requires .NET 8 SDK):

```bash
git clone <repo-url> vpn-dashboard
cd vpn-dashboard
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj -c Release -o publish
tar czf /tmp/vpn-dashboard-release.tar.gz publish/ deploy/ docs/
scp /tmp/vpn-dashboard-release.tar.gz root@<server-ip>:/tmp/
```

**On the server:**

```bash
mkdir -p /tmp/vpn-dashboard && cd /tmp/vpn-dashboard
tar xzf /tmp/vpn-dashboard-release.tar.gz

# Fedora:
sudo ./deploy/install.sh

# Ubuntu / Debian:
sudo ./deploy/install-ubuntu.sh
```

Then open `http://<server-ip>/` in your browser.

> **Note:** The server only needs the .NET 8 **runtime**, not the SDK. Building must be done on a development machine. See the full installation guides for details.

## Documentation

- [Install on Fedora](INSTALL-FEDORA.md) — full step-by-step Fedora installation guide
- [Install on Ubuntu](INSTALL-UBUNTU.md) — full step-by-step Ubuntu/Debian installation guide
- [OpenVPN Setup Wizard](OPENVPN-SETUP-WIZARD.md) — how the in-app installer works
- [OpenVPN Uninstall](OPENVPN-UNINSTALL.md) — what the uninstall action removes
- [Configuration](CONFIGURATION.md) — all settings and environment variables
- [Security](SECURITY.md) — security model, threat model, hardening
- [Operations](OPERATIONS.md) — backup, upgrade, troubleshooting
- [Uninstall](UNINSTALL.md) — removing the dashboard and/or OpenVPN

## Architecture

The dashboard runs as a systemd service (`vpn-dashboard.service`) behind nginx. It communicates with OpenVPN via:

- **Reading** the easy-rsa PKI directly (`/etc/openvpn/server/easy-rsa/pki/`)
- **Reading** the OpenVPN status log (`/var/log/openvpn/openvpn-status.log`)
- **Executing** privileged operations through a whitelisted bash helper (`vpn-dashboard-helper.sh`) via sudo

## License

MIT
