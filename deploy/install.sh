#!/bin/bash
#
# install.sh — one-shot installer for VPN Dashboard on Fedora
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

echo "=== VPN Dashboard Installer ==="
echo ""

# 1. Install dependencies
echo "[1/9] Installing dependencies..."
dnf install -y nginx policycoreutils-python-utils openssl

# Install .NET 8 ASP.NET Core runtime
if command -v dotnet &>/dev/null && dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.AspNetCore.App 8"; then
    echo "  ASP.NET Core 8 runtime already installed."
elif dnf list --available aspnetcore-runtime-8.0 &>/dev/null; then
    dnf install -y aspnetcore-runtime-8.0
else
    echo "  aspnetcore-runtime-8.0 not available via dnf — trying Microsoft repository..."
    rpm --import https://packages.microsoft.com/keys/microsoft.asc 2>/dev/null || true
    dnf install -y "https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm" >/dev/null 2>&1 || true
    if dnf install -y aspnetcore-runtime-8.0 2>/dev/null; then
        echo "  Installed from Microsoft repository."
    else
        echo "  dnf install failed — falling back to dotnet-install.sh..."
        curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin \
            --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet
        ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    fi
fi

# Final check
if ! command -v dotnet &>/dev/null; then
    echo "ERROR: .NET runtime could not be installed. Install it manually and re-run."
    echo "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet"
    echo "  ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet"
    exit 1
fi

# 2. Create service user
echo "[2/9] Creating service user '$SERVICE_USER'..."
if ! id "$SERVICE_USER" &>/dev/null; then
    useradd --system --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
fi

# 3. Create directories
echo "[3/9] Setting up directories..."
mkdir -p "$INSTALL_DIR" "$DATA_DIR"
chown "$SERVICE_USER:$SERVICE_USER" "$DATA_DIR"

# 4. Deploy the application
echo "[4/9] Deploying the application..."
if [[ -d "$PROJECT_ROOT/publish" ]]; then
    cp -r "$PROJECT_ROOT/publish/"* "$INSTALL_DIR/"
    echo "  Copied pre-built binaries from publish/."
elif [[ -f "$PROJECT_ROOT/src/VPNDashboard.Website/VPNDashboard.Website.csproj" ]] && command -v dotnet &>/dev/null && dotnet --list-sdks 2>/dev/null | grep -q "^8\\."; then
    dotnet publish "$PROJECT_ROOT/src/VPNDashboard.Website/VPNDashboard.Website.csproj" \
        -c Release -o "$INSTALL_DIR" --nologo -v quiet
    echo "  Built and published from source."
else
    echo "ERROR: No pre-built binaries found in publish/ and no .NET SDK available to build."
    echo "  Build on your development machine first:"
    echo "    dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj -c Release -o publish"
    echo "  Then re-run this installer."
    exit 1
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
if [[ -d /etc/openvpn/server ]]; then
    # Apply ACL to existing files...
    setfacl -R -m u:${SERVICE_USER}:rX /etc/openvpn/server/ 2>/dev/null || true
    # ...and a default ACL so files created later by easy-rsa (e.g. when the
    # PKI is regenerated or new clients are issued) inherit read access.
    setfacl -R -d -m u:${SERVICE_USER}:rX /etc/openvpn/server/ 2>/dev/null || true
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
    openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
        -keyout /etc/ssl/private/vpn-dashboard.key \
        -out /etc/ssl/certs/vpn-dashboard.crt \
        -subj "/CN=$(hostname)" >/dev/null 2>&1
fi

# Remove default nginx config if it exists
rm -f /etc/nginx/conf.d/default.conf

# Install our vhost
cp "$SCRIPT_DIR/nginx/vpn-dashboard.conf" /etc/nginx/conf.d/vpn-dashboard.conf
nginx -t 2>/dev/null || {
    echo "ERROR: nginx config test failed!"
    exit 1
}
systemctl enable --now nginx.service
systemctl reload nginx.service

# 9. Firewall and SELinux
echo "[9/9] Configuring firewall and SELinux..."
if systemctl is-active --quiet firewalld.service; then
    firewall-cmd --permanent --add-service=http 2>/dev/null || true
    firewall-cmd --permanent --add-service=https 2>/dev/null || true
    firewall-cmd --reload 2>/dev/null || true
fi

setsebool -P httpd_can_network_connect on 2>/dev/null || true

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
