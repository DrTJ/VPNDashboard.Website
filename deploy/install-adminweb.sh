#!/bin/bash
#
# install-adminweb.sh — one-shot installer for VPN Dashboard AdminWeb on Fedora/RHEL
#
set -euo pipefail

INSTALL_DIR="/opt/vpndashboard-admin"
DATA_DIR="/var/lib/vpndashboard-admin"
SERVICE_USER="vpndashadmin"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

if [[ "$EUID" -ne 0 ]]; then
    echo "This installer must be run as root."
    exit 1
fi

echo "=== VPN Dashboard AdminWeb Installer ==="
echo ""

# 1. Install dependencies
echo "[1/10] Installing dependencies..."
dnf install -y nginx openssl policycoreutils-python-utils git tar

# Install .NET 8 SDK (full SDK — AdminWeb runs dotnet publish at runtime)
if command -v dotnet &>/dev/null && dotnet --list-sdks 2>/dev/null | grep -q "^8\."; then
    echo "  .NET 8 SDK already installed."
elif dnf list --available dotnet-sdk-8.0 &>/dev/null; then
    dnf install -y dotnet-sdk-8.0
else
    echo "  dotnet-sdk-8.0 not available via dnf — trying Microsoft repository..."
    rpm --import https://packages.microsoft.com/keys/microsoft.asc 2>/dev/null || true
    dnf install -y "https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm" >/dev/null 2>&1 || true
    if dnf install -y dotnet-sdk-8.0 2>/dev/null; then
        echo "  Installed from Microsoft repository."
    else
        echo "  dnf install failed — falling back to dotnet-install.sh..."
        curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin \
            --channel 8.0 --install-dir /usr/share/dotnet
        ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
    fi
fi

if ! command -v dotnet &>/dev/null; then
    echo "ERROR: .NET SDK could not be installed. Install it manually and re-run."
    echo "  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 8.0 --install-dir /usr/share/dotnet"
    echo "  ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet"
    exit 1
fi

# 2. Create service user
echo "[2/10] Creating service user '$SERVICE_USER'..."
if ! id "$SERVICE_USER" &>/dev/null; then
    useradd --system --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
fi

# 3. Create directories
echo "[3/10] Setting up directories..."
mkdir -p "$INSTALL_DIR" \
         "$DATA_DIR" \
         "$DATA_DIR/keys" \
         "$DATA_DIR/build" \
         "$DATA_DIR/artifacts" \
         "$DATA_DIR/.dotnet" \
         "$DATA_DIR/.nuget/packages" \
         "$DATA_DIR/.local/share"

chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"
chown -R "$SERVICE_USER:$SERVICE_USER" "$DATA_DIR"
chmod 0700 "$DATA_DIR/keys"

# 4. Deploy the application
echo "[4/10] Deploying the application..."
if [[ -d "$PROJECT_ROOT/publish" ]]; then
    cp -r "$PROJECT_ROOT/publish/"* "$INSTALL_DIR/"
    echo "  Copied pre-built binaries from publish/."
elif [[ -f "$PROJECT_ROOT/src/VPNDashboard.AdminWeb/VPNDashboard.AdminWeb.csproj" ]] && command -v dotnet &>/dev/null && dotnet --list-sdks 2>/dev/null | grep -q "^8\."; then
    dotnet publish "$PROJECT_ROOT/src/VPNDashboard.AdminWeb/VPNDashboard.AdminWeb.csproj" \
        -c Release -o "$INSTALL_DIR" --nologo -v quiet
    echo "  Built and published from source."
else
    echo "ERROR: No pre-built binaries found in publish/ and no .NET 8 SDK available to build."
    echo "  Build on your development machine first:"
    echo "    dotnet publish src/VPNDashboard.AdminWeb/VPNDashboard.AdminWeb.csproj -c Release -o publish"
    echo "  Then re-run this installer."
    exit 1
fi

chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR"

# 5. Install systemd unit
echo "[5/10] Installing systemd service..."
cp "$SCRIPT_DIR/vpndashboard-admin.service" /etc/systemd/system/vpndashboard-admin.service
systemctl daemon-reload
systemctl enable vpndashboard-admin.service

# 6. Set up nginx reverse proxy (port 5050 → Kestrel on 5051)
echo "[6/10] Configuring nginx..."

# Generate self-signed cert if none exists
if [[ ! -f /etc/ssl/certs/vpndashboard-admin.crt ]]; then
    openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
        -keyout /etc/ssl/private/vpndashboard-admin.key \
        -out /etc/ssl/certs/vpndashboard-admin.crt \
        -subj "/CN=$(hostname)" >/dev/null 2>&1
fi

cp "$SCRIPT_DIR/nginx/vpndashboard-admin.conf" /etc/nginx/conf.d/vpndashboard-admin.conf

nginx -t 2>/dev/null || {
    echo "ERROR: nginx config test failed!"
    exit 1
}
systemctl enable --now nginx.service
systemctl reload nginx.service

# 7. Firewall: open http/https
echo "[7/10] Configuring firewall..."
if systemctl is-active --quiet firewalld.service; then
    firewall-cmd --permanent --add-service=http 2>/dev/null || true
    firewall-cmd --permanent --add-service=https 2>/dev/null || true
    firewall-cmd --permanent --add-port=5050/tcp 2>/dev/null || true
    firewall-cmd --reload 2>/dev/null || true
fi

# 8. SELinux
echo "[8/10] Configuring SELinux..."
setsebool -P httpd_can_network_connect on 2>/dev/null || true

# Allow nginx to listen on port 5050
semanage port -a -t http_port_t -p tcp 5050 2>/dev/null || \
    semanage port -m -t http_port_t -p tcp 5050 2>/dev/null || true

# 9. Seed admin credentials
echo ""
echo "[9/10] Initial Admin Setup"
read -p "Admin email: " ADMIN_EMAIL
read -s -p "Admin password: " ADMIN_PASSWORD
echo ""

mkdir -p /etc/systemd/system/vpndashboard-admin.service.d
cat > /etc/systemd/system/vpndashboard-admin.service.d/seed-admin.conf <<EOF
[Service]
Environment=VPNDASH_ADMIN_EMAIL=$ADMIN_EMAIL
Environment=VPNDASH_ADMIN_PASSWORD=$ADMIN_PASSWORD
EOF
chmod 0600 /etc/systemd/system/vpndashboard-admin.service.d/seed-admin.conf
systemctl daemon-reload

# 10. Start the service
echo "[10/10] Starting vpndashboard-admin..."
systemctl start vpndashboard-admin.service

IP_ADDR=$(hostname -I | awk '{print $1}')
echo ""
echo "=== Installation Complete ==="
echo "AdminWeb is running at http://${IP_ADDR}:5050/"
echo "Log in with the admin credentials you just provided."
echo ""
echo "After first login, remove the seed credentials:"
echo "  sudo rm /etc/systemd/system/vpndashboard-admin.service.d/seed-admin.conf"
echo "  sudo systemctl daemon-reload"
echo "  sudo systemctl restart vpndashboard-admin.service"
