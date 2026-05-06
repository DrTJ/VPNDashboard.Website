# Uninstalling OpenVPN

OpenVPN can be uninstalled from the web UI via the Server page's **Danger Zone** section.

## What Gets Removed

The uninstall process removes everything Nyr's script created:

- Stops and disables `openvpn-server@server.service`
- Removes firewall rules (firewalld port and trusted zone entries, or `openvpn-iptables.service`)
- Removes SELinux port mapping (if a custom port was used)
- Deletes `/etc/sysctl.d/99-openvpn-forward.conf`
- Deletes `/etc/openvpn/server/` (all certificates, keys, and configuration)
- Uninstalls the `openvpn` package via `dnf remove -y openvpn`

## What Is NOT Removed

- The VPN Dashboard itself (keeps running)
- The SQLite identity database (`/var/lib/vpn-dashboard/identity.db`)
- Any `.ovpn` files that were previously downloaded by users
- The vendored `openvpn-install.sh` script
- nginx configuration and certificates

## How to Uninstall from the UI

1. Log into the dashboard
2. Go to **Server** page
3. Scroll to the **Danger Zone** card
4. Click **Uninstall OpenVPN**
5. Type `UNINSTALL` in the confirmation field
6. Click **Confirm Uninstall**
7. Watch the live uninstall log
8. On success, you'll be redirected to the Setup Wizard

## How to Uninstall from the Shell

```bash
printf '3\ny\n' | sudo bash /opt/vpn-dashboard/openvpn-install.sh
```

Or interactively:

```bash
sudo bash /opt/vpn-dashboard/openvpn-install.sh
# Choose option 3, then confirm with y
```

## After Uninstalling

After uninstalling OpenVPN, the dashboard will redirect to the Setup Wizard where you can reinstall with different settings.
