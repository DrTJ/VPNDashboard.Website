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

To upgrade the VPN Dashboard:

```bash
cd /tmp
git clone <repo-url> vpn-dashboard-new
cd vpn-dashboard-new
sudo ./deploy/install.sh
```

The installer will overwrite the application binaries but preserve:

- The identity database
- OpenVPN configuration and PKI
- nginx configuration

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
