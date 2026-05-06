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

### Admin Password Requirements

The seeded admin password must meet all of these:

- At least **8 characters** long
- At least one **lowercase letter** (`a`-`z`)
- At least one **digit** (`0`-`9`)

If the password doesn't meet these requirements, the seed will fail silently. Check the service logs to verify:

```bash
journalctl -u vpn-dashboard -n 20 --no-pager | grep -i seed
```

### Setting Admin Credentials

Admin credentials are set via a systemd drop-in file:

```bash
mkdir -p /etc/systemd/system/vpn-dashboard.service.d
cat > /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf << 'EOF'
[Service]
Environment=VPNDASH_ADMIN_EMAIL=admin@example.com
Environment=VPNDASH_ADMIN_PASSWORD=Admin12345
EOF

systemctl daemon-reload
systemctl restart vpn-dashboard
```

The admin is only created on first run if the email doesn't already exist. To reset credentials, delete the database and restart:

```bash
rm -f /var/lib/vpn-dashboard/identity.db
systemctl restart vpn-dashboard
```

After your first login, remove the seed file so the password isn't stored in plaintext:

```bash
rm /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf
systemctl daemon-reload
```

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

## OpenVPN Status Log Format

The dashboard supports both OpenVPN status log **version 1** (default) and **version 2** formats. Fedora's OpenVPN systemd service adds `--status-version 2` automatically, so the log format may differ from what you see in the `server.conf`.

If the Connected Clients page is empty despite clients being connected, check the status log format:

```bash
cat /var/log/openvpn/openvpn-status.log
```

- **Version 1**: Lines like `clientname,1.2.3.4:1234,1234,5678,2024-01-01 00:00:00`
- **Version 2**: Lines prefixed with `CLIENT_LIST,clientname,...`

Both formats are parsed correctly by the dashboard.
