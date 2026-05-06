#!/bin/bash
#
# uninstall.sh — remove VPN Dashboard (does NOT touch OpenVPN)
#
set -euo pipefail

if [[ "$EUID" -ne 0 ]]; then
    echo "This script must be run as root."
    exit 1
fi

echo "=== VPN Dashboard Uninstaller ==="
echo ""
echo "This will remove the VPN Dashboard application."
echo "OpenVPN will NOT be removed."
echo ""
read -p "Continue? [y/N]: " confirm
if [[ ! "$confirm" =~ ^[yY]$ ]]; then
    echo "Aborted."
    exit 0
fi

echo ""

# If you want to also remove OpenVPN, do this FIRST:
echo "NOTE: If you want to also remove OpenVPN, run this BEFORE proceeding:"
echo "  printf '3\ny\n' | sudo bash /opt/vpn-dashboard/openvpn-install.sh"
echo ""

# Stop and disable the service
echo "[1/6] Stopping vpn-dashboard service..."
systemctl stop vpn-dashboard.service 2>/dev/null || true
systemctl disable vpn-dashboard.service 2>/dev/null || true
rm -f /etc/systemd/system/vpn-dashboard.service
rm -rf /etc/systemd/system/vpn-dashboard.service.d
systemctl daemon-reload

# Remove nginx config
echo "[2/6] Removing nginx configuration..."
rm -f /etc/nginx/conf.d/vpn-dashboard.conf
systemctl reload nginx.service 2>/dev/null || true

# Remove helper and sudoers
echo "[3/6] Removing helper script and sudoers..."
rm -f /usr/local/sbin/vpn-dashboard-helper.sh
rm -f /etc/sudoers.d/vpn-dashboard

# Remove application files
echo "[4/6] Removing application files..."
rm -rf /opt/vpn-dashboard

# Remove data directory
echo "[5/6] Removing data directory..."
rm -rf /var/lib/vpn-dashboard

# Remove service user
echo "[6/6] Removing service user..."
userdel vpndash 2>/dev/null || true

echo ""
echo "=== VPN Dashboard removed ==="
echo ""
echo "Self-signed certificates were left at:"
echo "  /etc/ssl/certs/vpn-dashboard.crt"
echo "  /etc/ssl/private/vpn-dashboard.key"
echo "Remove them manually if desired."
