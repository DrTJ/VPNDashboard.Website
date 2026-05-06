# Operations Guide

## Backup

### What to Back Up

| Item | Path | Purpose |
|------|------|---------|
| Identity database | `/var/lib/vpn-dashboard/identity.db` | Admin accounts and settings |
| PKI directory | `/etc/openvpn/server/easy-rsa/pki/` | All certificates and keys |
| Server config | `/etc/openvpn/server/server.conf` | OpenVPN configuration |
| Client template | `/etc/openvpn/server/client-common.txt` | Template for .ovpn files |

### Backup Script Example

```bash
#!/bin/bash
BACKUP_DIR="/root/vpn-backup-$(date +%Y%m%d)"
mkdir -p "$BACKUP_DIR"
cp /var/lib/vpn-dashboard/identity.db "$BACKUP_DIR/"
cp -r /etc/openvpn/server/easy-rsa/pki/ "$BACKUP_DIR/pki/"
cp /etc/openvpn/server/server.conf "$BACKUP_DIR/"
cp /etc/openvpn/server/client-common.txt "$BACKUP_DIR/"
echo "Backup saved to $BACKUP_DIR"
```

## Upgrade

To upgrade the VPN Dashboard, rebuild on your development machine and redeploy.

**On your development machine:**

```bash
cd vpn-dashboard
git pull

# Rebuild
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj -c Release -o publish

# Create tarball
tar czf /tmp/vpn-dashboard-release.tar.gz publish/ deploy/ docs/

# Copy to server
scp /tmp/vpn-dashboard-release.tar.gz root@<server-ip>:/tmp/
```

**On the server:**

```bash
cd /tmp/vpn-dashboard
tar xzf /tmp/vpn-dashboard-release.tar.gz
cp -rf publish/* /opt/vpn-dashboard/
chown -R vpndash:vpndash /opt/vpn-dashboard/
systemctl restart vpn-dashboard
```

This will overwrite the application binaries but preserve:

- The identity database (`/var/lib/vpn-dashboard/identity.db`)
- OpenVPN configuration and PKI
- nginx configuration
- systemd service and sudoers files

## Log Locations

| Log | How to View |
|-----|-------------|
| Dashboard service | `journalctl -u vpn-dashboard -f` |
| nginx access log | `/var/log/nginx/access.log` |
| nginx error log | `/var/log/nginx/error.log` |
| OpenVPN service | `journalctl -u openvpn-server@server -f` |
| OpenVPN status | `/var/log/openvpn/openvpn-status.log` |

## Restarting Services

```bash
# Restart the dashboard
sudo systemctl restart vpn-dashboard

# Restart nginx
sudo systemctl restart nginx

# Restart OpenVPN
sudo systemctl restart openvpn-server@server

# Reload OpenVPN (graceful, doesn't disconnect clients)
sudo systemctl reload openvpn-server@server
```

## Common Troubleshooting

### Dashboard not accessible

1. Check service: `systemctl status vpn-dashboard`
2. Check nginx: `systemctl status nginx`
3. Check firewall: `firewall-cmd --list-services`
4. Check SELinux: `getenforce` and `ausearch -m avc -ts recent`

### Connected clients not updating

1. Verify status log exists: `ls -la /var/log/openvpn/openvpn-status.log`
2. If missing, enable it: `sudo /usr/local/sbin/vpn-dashboard-helper.sh enable-status`
3. Verify status log is being updated: `cat /var/log/openvpn/openvpn-status.log`
4. Grant `vpndash` read access if needed:
   ```bash
   setfacl -m u:vpndash:r /var/log/openvpn/openvpn-status.log
   setfacl -m u:vpndash:rx /var/log/openvpn
   ```
5. If the log exists and has data but the page is empty, check the dashboard logs:
   ```bash
   journalctl -u vpn-dashboard -n 20 --no-pager | grep -i "status log"
   ```

> **Note:** The dashboard supports both OpenVPN status log version 1 and version 2 formats.
> Fedora's OpenVPN systemd service adds `--status-version 2` automatically.

### Service takes a long time to start/restart

If `systemctl restart vpn-dashboard` hangs for ~90 seconds, the service type may be set incorrectly:

```bash
# Check current setting
grep "Type=" /etc/systemd/system/vpn-dashboard.service

# Fix: change Type=notify to Type=simple
sed -i 's/Type=notify/Type=simple/' /etc/systemd/system/vpn-dashboard.service

# Add a stop timeout
grep -q 'TimeoutStopSec' /etc/systemd/system/vpn-dashboard.service || \
  sed -i '/RestartSec=10/a TimeoutStopSec=10' /etc/systemd/system/vpn-dashboard.service

systemctl daemon-reload
systemctl restart vpn-dashboard
```

### Cannot add or revoke clients

1. Check sudoers: `sudo visudo -c -f /etc/sudoers.d/vpn-dashboard`
2. Check helper exists: `ls -la /usr/local/sbin/vpn-dashboard-helper.sh`
3. Try running manually: `sudo /usr/local/sbin/vpn-dashboard-helper.sh add testclient`

### Database locked errors

If the SQLite database reports locking errors, ensure only one instance of the dashboard is running:

```bash
systemctl status vpn-dashboard
ps aux | grep VPNDashboard
```
