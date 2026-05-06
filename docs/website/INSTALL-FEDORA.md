# Installing VPN Dashboard on Fedora

This guide walks through installing the VPN Dashboard on a Fedora server.

## Prerequisites

- **Fedora 36 or later** with root access
- **TUN device** available (`/dev/net/tun`)
- **OpenVPN does NOT need to be pre-installed** — the dashboard's Setup Wizard will install it for you. If you've already installed it via Nyr's script, the dashboard will detect it automatically.

## Step 1: Install Git and required tools

```bash
sudo dnf install -y git curl
```

## Step 2: Install .NET 8 Runtime and nginx

On **Fedora 39+**, the ASP.NET Core runtime is available directly from the default repos:

```bash
sudo dnf install -y aspnetcore-runtime-8.0 nginx policycoreutils-python-utils openssl
```

On **Fedora 37–38**, add the Microsoft repository first:

```bash
sudo rpm --import https://packages.microsoft.com/keys/microsoft.asc
sudo dnf install -y "https://packages.microsoft.com/config/fedora/$(rpm -E %fedora)/packages-microsoft-prod.rpm"
sudo dnf install -y aspnetcore-runtime-8.0 nginx policycoreutils-python-utils openssl
```

On **Fedora 36 or older** (EOL releases where the Microsoft repo has no packages), use the standalone install script:

```bash
# Install .NET 8 ASP.NET Core runtime manually
curl -sSL https://dot.net/v1/dotnet-install.sh | sudo bash /dev/stdin \
    --channel 8.0 --runtime aspnetcore --install-dir /usr/share/dotnet
sudo ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet

# Verify
dotnet --info

# Install the remaining dependencies
sudo dnf install -y nginx policycoreutils-python-utils openssl
```

> **Note:** If `dnf` reports a conflict between the Microsoft and Fedora .NET packages, set a
> repo priority. Run `sudo dnf config-manager --set-disabled packages-microsoft-com-prod` and
> retry from the Fedora repos, or vice-versa. Only one source should provide .NET packages.
>
> The `deploy/install.sh` script handles all of this automatically — it tries the dnf package
> first, falls back to the Microsoft repo, and finally to `dotnet-install.sh` if needed.

## Step 3: Build the Application (on your development machine)

The server only needs the .NET **runtime** — building requires the .NET **SDK**, which should be on your development machine (not the server).

On your **development machine** (Mac/Linux/Windows with .NET 8 SDK):

```bash
git clone <repo-url> vpn-dashboard
cd vpn-dashboard
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj -c Release -o publish
```

This creates a `publish/` folder with the compiled application.

## Step 4: Copy to Server

Create a tarball and copy it to the server:

```bash
# On your development machine:
tar czf /tmp/vpn-dashboard-release.tar.gz publish/ deploy/ docs/

# Copy to server:
scp /tmp/vpn-dashboard-release.tar.gz root@<server-ip>:/tmp/
```

Then on the **server**, extract it:

```bash
mkdir -p /tmp/vpn-dashboard
cd /tmp/vpn-dashboard
tar xzf /tmp/vpn-dashboard-release.tar.gz
```

> **Alternative:** If the repo is public, you can clone directly on the server instead:
> `git clone <repo-url> /tmp/vpn-dashboard && cd /tmp/vpn-dashboard`
> But you will still need to build on a machine with the .NET SDK and copy the `publish/` folder,
> or install the SDK on the server (`sudo dnf install -y dotnet-sdk-8.0`).

## Step 5: Run the Installer

```bash
cd /tmp/vpn-dashboard
sudo ./deploy/install.sh
```

The installer will:

1. Create a system user `vpndash` (no shell, no home directory)
2. Copy the pre-built application to `/opt/vpn-dashboard/`
3. Create `/var/lib/vpn-dashboard/` for the SQLite identity database
4. Install the privileged helper script at `/usr/local/sbin/vpn-dashboard-helper.sh`
5. Configure sudoers for the `vpndash` user
6. Install and enable the systemd service
7. Configure nginx as a reverse proxy (default server on ports 80 and 443)
8. Generate a self-signed TLS certificate
9. Open firewall ports for HTTP and HTTPS
10. Set SELinux boolean `httpd_can_network_connect`
11. Enable the OpenVPN status log (if OpenVPN is already installed)
12. Prompt for initial admin email and password

## Step 6: First Login

Open `http://<server-ip>/` in your browser. You'll see the AdminLTE login page.

Log in with the admin email and password you provided during installation.

## User Roles

The dashboard has two roles:


| Role       | Permissions                                                                                                                                      |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Admin**  | Full access: manage VPN clients (add/revoke), server operations (reload/uninstall), install OpenVPN via Setup Wizard, and manage dashboard users |
| **Viewer** | Read-only: view client profiles, connected clients, server status, and download `.ovpn` files                                                    |


The initial user created during installation is an **Admin**. To add more users or change roles, go to **Administration > Users** (`/account/users`) in the sidebar (visible to Admins only).

All users can change their own password at **Settings** (`/account/manage`) from the top-right profile menu.

## Step 7: Install OpenVPN (if not already installed)

If OpenVPN is not yet installed, the dashboard will automatically redirect you to the **Setup Wizard** (`/setup`).

The wizard will ask for:


| Setting      | Default          | Description                        |
| ------------ | ---------------- | ---------------------------------- |
| Protocol     | UDP              | UDP is recommended for performance |
| Port         | 1194             | Standard OpenVPN port              |
| DNS          | System resolvers | DNS servers pushed to VPN clients  |
| First client | client           | Name for the first VPN profile     |


Click **Install OpenVPN** and watch the live installation log. This takes 1-2 minutes.

When it finishes, you'll be redirected to the dashboard. The first `.ovpn` profile is available on the Clients page.

## Step 8: Optional — Real TLS Certificate

Replace the self-signed certificate with Let's Encrypt:

```bash
sudo dnf install -y certbot python3-certbot-nginx
sudo certbot --nginx -d vpn.example.com
```

## Step 9: Verification Checklist

- `systemctl is-active vpn-dashboard` returns `active`
- `systemctl is-active nginx` returns `active`
- `http://<server-ip>/` loads the login page
- Can log in with admin credentials
- Clients page shows profiles
- Can add a new client and download the `.ovpn` file
- Can revoke a client
- Connected page updates live when a VPN client connects

## Step 10: Clean Up Seed Credentials

After your first login, remove the one-shot admin credentials:

```bash
sudo rm /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf
sudo systemctl daemon-reload
sudo systemctl restart vpn-dashboard.service
```

## Deploying Updates

When you make changes to the dashboard code, rebuild and redeploy:

**On your development machine:**

```bash
cd vpn-dashboard

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

## Manual Post-Install Setup

If the installer script did not fully configure everything (e.g. you deployed manually), you may need to perform these steps by hand.

### Copy published files to the install directory

If `/opt/vpn-dashboard/` is empty or missing `VPNDashboard.Website.dll`:

```bash
cp -r /tmp/vpn-dashboard/publish/* /opt/vpn-dashboard/
chown -R vpndash:vpndash /opt/vpn-dashboard/
systemctl restart vpn-dashboard
```

### Install the nginx reverse-proxy config

```bash
cp /tmp/vpn-dashboard/deploy/nginx/vpn-dashboard.conf /etc/nginx/conf.d/vpn-dashboard.conf
```

### Disable the default Fedora test page

Fedora's default nginx config includes a built-in server block that shows a "Fedora Webserver Test Page". You must remove or disable it so the dashboard's `default_server` takes effect:

```bash
# Remove the default config if it exists
mv /etc/nginx/conf.d/default.conf /etc/nginx/conf.d/default.conf.bak 2>/dev/null

# Comment out the default server block in nginx.conf
# (Fedora ships a server{} block inside /etc/nginx/nginx.conf itself)
sudo sed -i '/^    server {/,/^    }/s/^/#/' /etc/nginx/nginx.conf
```

### Generate the self-signed TLS certificate

If nginx fails with `cannot load certificate "/etc/ssl/certs/vpn-dashboard.crt"`:

```bash
mkdir -p /etc/ssl/private /etc/ssl/certs

openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
  -keyout /etc/ssl/private/vpn-dashboard.key \
  -out /etc/ssl/certs/vpn-dashboard.crt \
  -subj "/CN=vpn-dashboard"
```

### Set SELinux boolean

```bash
sudo setsebool -P httpd_can_network_connect on
```

### Open firewall ports

```bash
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload
```

### Test and restart nginx

```bash
nginx -t && systemctl restart nginx
systemctl enable nginx
```

### Verify everything is running

```bash
systemctl status vpn-dashboard   # should be active
systemctl status nginx            # should be active
curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1/  # should return 200 or 302
```

## Troubleshooting

### Page won't load / ERR_CONNECTION_REFUSED

1. Check if the dashboard service is running: `systemctl status vpn-dashboard`
2. Check nginx: `systemctl status nginx` and `nginx -t`
3. Check firewall: `firewall-cmd --list-all`
4. If the service is `inactive (dead)`, start it: `sudo systemctl start vpn-dashboard`
5. Check service logs: `sudo journalctl -u vpn-dashboard -n 50 --no-pager`

### Service fails with "application does not exist"

The published DLL is missing from `/opt/vpn-dashboard/`. This happens when the app was not built or copied correctly:

```bash
# Verify the DLL exists
ls -la /opt/vpn-dashboard/VPNDashboard.Website.dll

# If missing, copy from the publish directory
cp -r /tmp/vpn-dashboard/publish/* /opt/vpn-dashboard/
chown -R vpndash:vpndash /opt/vpn-dashboard/
systemctl restart vpn-dashboard
```

### Fedora test page shows instead of the dashboard

The default nginx server block is taking priority over the dashboard config:

```bash
# Remove the default config
mv /etc/nginx/conf.d/default.conf /etc/nginx/conf.d/default.conf.bak 2>/dev/null

# Comment out the server{} block in nginx.conf
sudo sed -i '/^    server {/,/^    }/s/^/#/' /etc/nginx/nginx.conf

# Verify our config is installed
ls /etc/nginx/conf.d/vpn-dashboard.conf

# Restart
nginx -t && systemctl restart nginx
```

### nginx fails with "cannot load certificate"

The self-signed TLS certificate was not generated:

```bash
mkdir -p /etc/ssl/private /etc/ssl/certs

openssl req -x509 -nodes -days 3650 -newkey rsa:2048 \
  -keyout /etc/ssl/private/vpn-dashboard.key \
  -out /etc/ssl/certs/vpn-dashboard.crt \
  -subj "/CN=vpn-dashboard"

nginx -t && systemctl restart nginx
```

### SELinux denials

```bash
sudo ausearch -m avc -ts recent
sudo setsebool -P httpd_can_network_connect on
```

### Connected clients not showing / OpenVPN status log not present

The Connected page relies on the OpenVPN status log. If no connected clients appear, the status log is likely missing or unreadable.

**Quick fix** — use the helper script:

```bash
sudo /usr/local/sbin/vpn-dashboard-helper.sh enable-status
```

**Manual fix** — if the helper script isn't available:

```bash
# Create the log directory
mkdir -p /var/log/openvpn

# Add the status directive to OpenVPN's config
echo "status /var/log/openvpn/openvpn-status.log 10" >> /etc/openvpn/server/server.conf

# Restart OpenVPN to start writing the log
systemctl restart openvpn-server@server

# Verify the log was created
ls -la /var/log/openvpn/openvpn-status.log

# Grant the vpndash user read access
setfacl -m u:vpndash:r /var/log/openvpn/openvpn-status.log
setfacl -m u:vpndash:rx /var/log/openvpn
```

The `10` in the status directive means OpenVPN updates the log every 10 seconds. The Connected page polls this file via SignalR every 5 seconds.

### Sudoers syntax error

```bash
sudo visudo -c -f /etc/sudoers.d/vpn-dashboard
```

### Install wizard stuck

Check the dashboard logs:

```bash
journalctl -u vpn-dashboard -f
```

