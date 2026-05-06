# Deploying Both Projects to the Same Server

Both VPNDashboard.Website and VPNDashboard.AdminWeb can run on the same Linux server. They use separate directories, service users, and ports.

## Architecture

```
nginx (ports 80/443 + 5050)
  ├── /       → 127.0.0.1:5000   (VPNDashboard.Website)
  └── :5050   → 127.0.0.1:5051   (VPNDashboard.AdminWeb)

vpn-dashboard.service           vpndashboard-admin.service
  user: vpndash                   user: vpndashadmin
  dir:  /opt/vpn-dashboard        dir:  /opt/vpndashboard-admin
  data: /var/lib/vpn-dashboard    data: /var/lib/vpndashboard-admin
```

## Installation

### 1. Install VPNDashboard.Website

```bash
sudo bash deploy/install.sh
```

### 2. Install AdminWeb on the same box

```bash
sudo bash deploy/install-adminweb.sh
```

### 3. Access

| App | URL |
|-----|-----|
| VPN Dashboard | `https://your-server/` (ports 80/443) |
| Admin Panel | `http://your-server:5050/` |

## Subdomain Setup (optional)

To serve both on port 443 using different subdomains, replace the default nginx configs with:

```nginx
server {
    listen 443 ssl;
    server_name vpn.example.com;
    ssl_certificate /etc/ssl/certs/vpn-dashboard.crt;
    ssl_certificate_key /etc/ssl/private/vpn-dashboard.key;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 443 ssl;
    server_name admin.example.com;
    ssl_certificate /etc/ssl/certs/vpn-dashboard.crt;
    ssl_certificate_key /etc/ssl/private/vpn-dashboard.key;

    location / {
        proxy_pass http://127.0.0.1:5051;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Then reload nginx:

```bash
sudo nginx -t && sudo systemctl reload nginx
```

## Publishing from macOS to the Linux Server

The full workflow from your Mac: clean, build for Linux, package, upload, and deploy.

### Prerequisites (one-time setup on your Mac)

```bash
# Ensure .NET 8 SDK is installed
dotnet --version

# Verify SSH access to the server
ssh root@your-server "echo OK"
```

### Step 1: Clean old build artifacts

```bash
cd /path/to/VPNDashboard.Website

# Remove bin/obj from all projects
find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
rm -rf publish-website publish-admin
```

### Step 2: Build for Linux (cross-publish from macOS)

Since the target is Linux, publish with the `linux-x64` runtime:

```bash
# VPNDashboard.Website
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o publish-website

# VPNDashboard.AdminWeb
dotnet publish src/VPNDashboard.AdminWeb/VPNDashboard.AdminWeb.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o publish-admin
```

> `--self-contained false` keeps the tarball small (requires .NET runtime on server).
> Use `--self-contained true` if you don't want to install the .NET runtime on the server (larger ~80MB archive).

### Step 3: Strip unnecessary files

```bash
# Remove debug symbols and dev config
find publish-website -name '*.pdb' -delete
rm -f publish-website/appsettings.Development.json

find publish-admin -name '*.pdb' -delete
rm -f publish-admin/appsettings.Development.json
```

### Step 4: Create tarballs

```bash
tar -czf vpndashboard-website.tar.gz -C publish-website .
tar -czf vpndashboard-admin.tar.gz -C publish-admin .
```

### Step 5: Upload to the server

```bash
scp vpndashboard-website.tar.gz vpndashboard-admin.tar.gz root@your-server:/tmp/
```

### Step 6: Deploy on the server (via SSH)

You can run this in one shot from your Mac:

```bash
ssh root@your-server 'bash -s' << 'EOF'
set -e

# Deploy Website
echo "=== Deploying VPNDashboard.Website ==="
systemctl stop vpn-dashboard
mkdir -p /opt/vpn-dashboard
tar -xzf /tmp/vpndashboard-website.tar.gz -C /opt/vpn-dashboard
chown -R vpndash:vpndash /opt/vpn-dashboard
systemctl start vpn-dashboard
echo "Website: $(systemctl is-active vpn-dashboard)"

# Deploy AdminWeb
echo "=== Deploying VPNDashboard.AdminWeb ==="
systemctl stop vpndashboard-admin
mkdir -p /opt/vpndashboard-admin
tar -xzf /tmp/vpndashboard-admin.tar.gz -C /opt/vpndashboard-admin
chown -R vpndashadmin:vpndashadmin /opt/vpndashboard-admin
systemctl start vpndashboard-admin
echo "AdminWeb: $(systemctl is-active vpndashboard-admin)"

# Cleanup
rm -f /tmp/vpndashboard-website.tar.gz /tmp/vpndashboard-admin.tar.gz
echo "=== Done ==="
EOF
```

### All-in-one script (copy-paste from your Mac terminal)

```bash
#!/bin/bash
set -euo pipefail

SERVER="root@your-server"
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
```

Save this as `deploy.sh` in the repo root and run:

```bash
chmod +x deploy.sh
./deploy.sh
```

> Replace `root@your-server` with your actual SSH user and server IP/hostname.

## Deploying only one project

To deploy just the Website:

```bash
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj \
    -c Release -r linux-x64 --self-contained false -o publish-website
find publish-website -name '*.pdb' -delete
rm -f publish-website/appsettings.Development.json
tar -czf vpndashboard-website.tar.gz -C publish-website .
scp vpndashboard-website.tar.gz root@your-server:/tmp/
ssh root@your-server 'systemctl stop vpn-dashboard && tar -xzf /tmp/vpndashboard-website.tar.gz -C /opt/vpn-dashboard && chown -R vpndash:vpndash /opt/vpn-dashboard && systemctl start vpn-dashboard && rm /tmp/vpndashboard-website.tar.gz'
```

To deploy just AdminWeb, substitute the project/paths accordingly.

## Using AdminWeb to Deploy Website

Once AdminWeb is running, you can add `localhost` (or `127.0.0.1`) as a target server in the Admin Panel. Then use the Build & Deploy UI to update VPNDashboard.Website without SSH-ing in manually.
