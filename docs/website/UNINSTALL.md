# Uninstalling

## Remove OpenVPN

You can remove OpenVPN either from the web UI or from the shell.

### From the Web UI

1. Log into the dashboard
2. Navigate to **Server**
3. Scroll to **Danger Zone**
4. Click **Uninstall OpenVPN**, type `UNINSTALL`, and confirm

### From the Shell

Interactive:

```bash
sudo bash /opt/vpn-dashboard/openvpn-install.sh
# Choose option 3 (Remove OpenVPN)
# Confirm with y
```

One-liner:

```bash
printf '3\ny\n' | sudo bash /opt/vpn-dashboard/openvpn-install.sh
```

**Important**: If you plan to also remove the dashboard, remove OpenVPN first while the vendored script still exists.

## Remove the VPN Dashboard

Run the uninstall script:

```bash
sudo /opt/vpn-dashboard/uninstall.sh
```

This removes:

- The systemd service (`vpn-dashboard.service`)
- Application files (`/opt/vpn-dashboard/`)
- Data directory (`/var/lib/vpn-dashboard/`)
- Helper script (`/usr/local/sbin/vpn-dashboard-helper.sh`)
- Sudoers file (`/etc/sudoers.d/vpn-dashboard`)
- nginx vhost (`/etc/nginx/conf.d/vpn-dashboard.conf`)
- The `vpndash` system user

**Not removed:**

- OpenVPN itself (remove separately if desired)
- Self-signed TLS certificates at `/etc/ssl/certs/vpn-dashboard.crt` and `/etc/ssl/private/vpn-dashboard.key`
- nginx package (installed as a dependency)

## Complete Removal (Both)

To remove everything:

```bash
# Step 1: Remove OpenVPN
printf '3\ny\n' | sudo bash /opt/vpn-dashboard/openvpn-install.sh

# Step 2: Remove the dashboard
sudo /opt/vpn-dashboard/uninstall.sh

# Step 3 (optional): Remove leftover certificates
sudo rm -f /etc/ssl/certs/vpn-dashboard.crt /etc/ssl/private/vpn-dashboard.key

# Step 4 (optional): Remove nginx if no longer needed
sudo dnf remove -y nginx
```
