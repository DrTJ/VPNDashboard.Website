#!/bin/bash
set -euo pipefail

SERVER="root@5.161.115.89"
PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_ROOT"

echo "[1/6] Cleaning..."
find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
rm -rf publish-website publish-admin

echo "[2/6] Publishing Website..."
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj \
    -c Release -r linux-x64 --self-contained false -o publish-website --nologo -v quiet
find publish-website -name '*.pdb' -delete
rm -f publish-website/appsettings.Development.json

echo "[3/6] Publishing AdminWeb..."
dotnet publish src/VPNDashboard.AdminWeb/VPNDashboard.AdminWeb.csproj \
    -c Release -r linux-x64 --self-contained false -o publish-admin --nologo -v quiet
find publish-admin -name '*.pdb' -delete
rm -f publish-admin/appsettings.Development.json

echo "[4/6] Creating tarballs..."
tar -czf vpndashboard-website.tar.gz -C publish-website .
tar -czf vpndashboard-admin.tar.gz -C publish-admin .

echo "[5/6] Uploading to $SERVER..."
scp -q vpndashboard-website.tar.gz vpndashboard-admin.tar.gz "$SERVER":/tmp/

echo "[6/6] Deploying on server..."
ssh "$SERVER" 'bash -s' << 'REMOTE'
set -e
systemctl stop vpn-dashboard
tar -xzf /tmp/vpndashboard-website.tar.gz -C /opt/vpn-dashboard
chown -R vpndash:vpndash /opt/vpn-dashboard
systemctl start vpn-dashboard

systemctl stop vpndashboard-admin
tar -xzf /tmp/vpndashboard-admin.tar.gz -C /opt/vpndashboard-admin
chown -R vpndashadmin:vpndashadmin /opt/vpndashboard-admin
systemctl start vpndashboard-admin

rm -f /tmp/vpndashboard-website.tar.gz /tmp/vpndashboard-admin.tar.gz
echo "vpn-dashboard: $(systemctl is-active vpn-dashboard)"
echo "vpndashboard-admin: $(systemctl is-active vpndashboard-admin)"
REMOTE

echo "=== Deploy complete ==="