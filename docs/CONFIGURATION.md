# Configuration

The VPN Dashboard is configured via `appsettings.json` and environment variables.

## Configuration File

The main configuration file is at `/opt/vpn-dashboard/appsettings.json`.

### OpenVPN Settings

```json
{
  "OpenVpn": {
    "ServerConfPath": "/etc/openvpn/server/server.conf",
    "PkiPath": "/etc/openvpn/server/easy-rsa/pki",
    "ClientCommonPath": "/etc/openvpn/server/client-common.txt",
    "StatusLogPath": "/var/log/openvpn/openvpn-status.log",
    "HelperScriptPath": "/usr/local/sbin/vpn-dashboard-helper.sh",
    "InstallScriptPath": "/opt/vpn-dashboard/openvpn-install.sh",
    "ServiceName": "openvpn-server@server",
    "DocsPath": "/opt/vpn-dashboard/docs"
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `ServerConfPath` | `/etc/openvpn/server/server.conf` | Path to OpenVPN server config. Used to detect if OpenVPN is installed. |
| `PkiPath` | `/etc/openvpn/server/easy-rsa/pki` | Path to the easy-rsa PKI directory. Client profiles are read from `index.txt` here. |
| `ClientCommonPath` | `/etc/openvpn/server/client-common.txt` | Template used to build `.ovpn` files. |
| `StatusLogPath` | `/var/log/openvpn/openvpn-status.log` | OpenVPN status log for connected clients. Polled every 5 seconds. |
| `HelperScriptPath` | `/usr/local/sbin/vpn-dashboard-helper.sh` | Privileged helper script path. |
| `InstallScriptPath` | `/opt/vpn-dashboard/openvpn-install.sh` | Vendored copy of Nyr's installer script. |
| `ServiceName` | `openvpn-server@server` | systemd service name for OpenVPN. |
| `DocsPath` | `/opt/vpn-dashboard/docs` | Path to documentation markdown files. |

### Database

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/lib/vpn-dashboard/identity.db"
  }
}
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `VPNDASH_ADMIN_EMAIL` | Email for the initial admin account (used on first run only) |
| `VPNDASH_ADMIN_PASSWORD` | Password for the initial admin account (used on first run only) |
| `ASPNETCORE_URLS` | Kestrel binding URL (default: `http://127.0.0.1:5000`) |
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Development` |

## Changing the Listening Port

By default, Kestrel listens on `127.0.0.1:5000` and nginx reverse-proxies to it.

To change the Kestrel port, edit the systemd unit:

```bash
sudo systemctl edit vpn-dashboard.service
```

Add:

```ini
[Service]
Environment=ASPNETCORE_URLS=http://127.0.0.1:5001
```

Then update the nginx config at `/etc/nginx/conf.d/vpn-dashboard.conf` to match, and reload both services.
