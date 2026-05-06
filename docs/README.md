# VPN Dashboard

A web-based management dashboard for OpenVPN servers, built with ASP.NET Core 8 Blazor Server and AdminLTE 4.

## Features

- **Install/Uninstall OpenVPN** directly from the web UI (uses Nyr's openvpn-install script)
- **Manage client profiles** — add, revoke, and download `.ovpn` configuration files
- **Live connected clients** — real-time view of who's connected via SignalR
- **Server management** — view configuration, reload service, read journal logs
- **Secure** — ASP.NET Core Identity with SQLite, unprivileged service user, sudoers-whitelisted helper script
- **AdminLTE 4 UI** — polished admin interface with Bootstrap 5

## Quick Start

```bash
# Get the code
git clone <repo-url> /tmp/vpn-dashboard
cd /tmp/vpn-dashboard

# Fedora:
sudo ./deploy/install.sh

# Ubuntu / Debian:
sudo ./deploy/install-ubuntu.sh
```

Then open `http://<server-ip>/` in your browser.

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
