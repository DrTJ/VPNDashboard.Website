#!/bin/bash
#
# install-ubuntu.sh — one-shot installer for VPN Dashboard on Ubuntu
#
set -euo pipefail

INSTALL_DIR="/opt/vpn-dashboard"
DATA_DIR="/var/lib/vpn-dashboard"
SERVICE_USER="vpndash"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

if [[ "$EUID" -ne 0 ]]; then
    echo "This installer must be run as root."
    exit 1
fi

# Verify we're on Ubuntu/Debian
if ! grep -qs "ubuntu\|debian" /etc/os-release; then
    echo "This installer is for Ubuntu/Debian. Use install.sh for Fedora."
    exit 1
fi

echo "=== VPN Dashboard Installer (Ubuntu) ==="
echo ""

# 1. Install dependencies
echo "[1/9] Installing dependencies..."
apt-get update -qq

if ! dpkg -s aspnetcore-runtime-8.0 &>/dev/null; then
    if ! apt-cache show aspnetcore-runtime-8.0 &>/dev/null; then
        echo "  Adding Microsoft package repository..."
        apt-get install -y wget apt-transport-https >/dev/null 2>&1
        UBUNTU_VERSION=$(lsb_release -rs 2>/dev/null || grep VERSION_ID /etc/os-release | cut -d'"' -f2)
        wget -q "https://packages.microsoft.com/config/ubuntu/${UBUNTU_VERSION}/packages-microsoft-prod.deb" \
            -O /tmp/packages-microsoft-prod.deb
        dpkg -i /tmp/packages-microsoft-prod.deb
        rm -f /tmp/packages-microsoft-prod.deb
        apt-get update -qq
    fi
fi

apt-get install -y aspnetcore-runtime-8.0 nginx openssl acl

# 2. Create service user
echo "[2/9] Creating service user '$SERVICE_USER'..."
if ! id "$SERVICE_USER" &>/dev/null; then
    useradd --system --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
fi

# 3. Create directories
echo "[3/9] Setting up directories..."
mkdir -p "$INSTALL_DIR" "$DATA_DIR"
chown "$SERVICE_USER:$SERVICE_USER" "$DATA_DIR"

# 4. Build and publish the app
echo "[4/9] Publishing the application..."
if [[ -f "$PROJECT_ROOT/src/VPNDashboard.Website/VPNDashboard.Website.csproj" ]]; then
    if command -v dotnet &>/dev/null; then
        dotnet publish "$PROJECT_ROOT/src/VPNDashboard.Website/VPNDashboard.Website.csproj" \
            -c Release -o "$INSTALL_DIR" --nologo -v quiet
    else
        echo "WARNING: dotnet SDK not found. Install the SDK to build, or copy published output to $INSTALL_DIR manually."
    fi
else
    echo "WARNING: Project not found. Copy published output to $INSTALL_DIR manually."
fi

# Copy docs
if [[ -d "$PROJECT_ROOT/docs" ]]; then
    cp -r "$PROJECT_ROOT/docs" "$INSTALL_DIR/docs"
fi

# Copy vendored openvpn-install.sh
if [[ -f "$SCRIPT_DIR/openvpn-install.sh" ]]; then
    cp "$SCRIPT_DIR/openvpn-install.sh" "$INSTALL_DIR/openvpn-install.sh"
    chmod 0755 "$INSTALL_DIR/openvpn-install.sh"
    chown root:root "$INSTALL_DIR/openvpn-install.sh"
fi

# Copy uninstall script
cp "$SCRIPT_DIR/uninstall.sh" "$INSTALL_DIR/uninstall.sh"
chmod 0755 "$INSTALL_DIR/uninstall.sh"

# 5. Install helper script and sudoers
echo "[5/9] Installing helper script and sudoers..."
cp "$SCRIPT_DIR/vpn-dashboard-helper.sh" /usr/local/sbin/vpn-dashboard-helper.sh
chmod 0755 /usr/local/sbin/vpn-dashboard-helper.sh
chown root:root /usr/local/sbin/vpn-dashboard-helper.sh
cp "$SCRIPT_DIR/vpn-dashboard.sudoers" /etc/sudoers.d/vpn-dashboard
chmod 0440 /etc/sudoers.d/vpn-dashboard

# Validate sudoers
visudo -c -f /etc/sudoers.d/vpn-dashboard || {
    echo "ERROR: sudoers file is invalid!"
    rm -f /etc/sudoers.d/vpn-dashboard
    exit 1
}

# 6. Set up PKI read access for vpndash
echo "[6/9] Setting up PKI read permissions..."
if [[ -d /etc/openvpn/server/easy-rsa/pki ]]; then
    setfacl -R -m u:${SERVICE_USER}:rX /etc/openvpn/server/easy-rsa/pki/ 2>/dev/null || true
    setfacl -R -m u:${SERVICE_USER}:rX /etc/openvpn/server/ 2>/dev/null || true
fi

# 7. Install systemd unit
echo "[7/9] Installing systemd service..."
cp "$SCRIPT_DIR/vpn-dashboard.service" /etc/systemd/system/vpn-dashboard.service
systemctl daemon-reload
systemctl enable vpn-dashboard.service

# 8. Set up nginx
echo "[8/9] Configuring nginx..."
# Generate self-signed cert
if [[ ! -f /etc/ssl/certs/vpn-dashboard.crt ]]; then
    mkdir -p /etc/ssl/private
    openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
        -keyout /etc/ssl/private/vpn-dashboard.key \
        -out /etc/ssl/certs/vpn-dashboard.crt \
        -subj "/CN=$(hostname)" >/dev/null 2>&1
fi

# On Ubuntu, nginx uses sites-enabled. Remove the default site and put our config in conf.d.
rm -f /etc/nginx/sites-enabled/default
mkdir -p /etc/nginx/conf.d
cp "$SCRIPT_DIR/nginx/vpn-dashboard.conf" /etc/nginx/conf.d/vpn-dashboard.conf
nginx -t 2>/dev/null || {
    echo "ERROR: nginx config test failed!"
    exit 1
}
systemctl enable --now nginx.service
systemctl reload nginx.service

# 9. Firewall
echo "[9/9] Configuring firewall..."
if command -v ufw &>/dev/null && ufw status | grep -q "active"; then
    ufw allow 'Nginx Full' 2>/dev/null || {
        ufw allow 80/tcp 2>/dev/null || true
        ufw allow 443/tcp 2>/dev/null || true
    }
fi

# Enable status log if OpenVPN is already installed
if [[ -f /etc/openvpn/server/server.conf ]]; then
    /usr/local/sbin/vpn-dashboard-helper.sh enable-status || true
fi

# Seed admin credentials
echo ""
echo "=== Initial Admin Setup ==="
read -p "Admin email: " ADMIN_EMAIL
read -s -p "Admin password: " ADMIN_PASSWORD
echo ""

# Write one-shot env vars for first start
mkdir -p /etc/systemd/system/vpn-dashboard.service.d
cat > /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf <<EOF
[Service]
Environment=VPNDASH_ADMIN_EMAIL=$ADMIN_EMAIL
Environment=VPNDASH_ADMIN_PASSWORD=$ADMIN_PASSWORD
EOF
chmod 0600 /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf
systemctl daemon-reload

# Start the dashboard
systemctl start vpn-dashboard.service

echo ""
echo "=== Installation Complete ==="
echo "Dashboard is running at http://$(hostname -I | awk '{print $1}')/"
echo "Log in with the admin credentials you just provided."
echo ""
echo "After first login, remove the seed credentials:"
echo "  sudo rm /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf"
echo "  sudo systemctl daemon-reload"
echo "  sudo systemctl restart vpn-dashboard.service"
