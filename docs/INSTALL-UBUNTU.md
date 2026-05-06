# Installing VPN Dashboard on Ubuntu

This guide walks through installing the VPN Dashboard on an Ubuntu server.

## Prerequisites

- **Ubuntu 22.04 or later** (22.04 LTS, 24.04 LTS, or newer) with root access
- **TUN device** available (`/dev/net/tun`)
- **OpenVPN does NOT need to be pre-installed** — the dashboard's Setup Wizard will install it for you. If you've already installed it via Nyr's script, the dashboard will detect it automatically.

## Step 1: Install Git and required tools

```bash
sudo apt-get update
sudo apt-get install -y git curl wget
```

## Step 2: Install .NET 8 Runtime

Add the Microsoft package repository and install the ASP.NET Core 8 runtime:

```bash
# Install prerequisites
sudo apt-get update
sudo apt-get install -y wget apt-transport-https

# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET 8 runtime
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-8.0
```

> **Note:** On Ubuntu 24.04+, .NET 8 may also be available directly via `sudo apt-get install -y dotnet-runtime-8.0 aspnetcore-runtime-8.0` without adding the Microsoft repo.

## Step 3: Install nginx and other dependencies

```bash
sudo apt-get install -y nginx openssl acl
```

## Step 4: Get the Dashboard

Clone the repository:

```bash
git clone <repo-url> /tmp/vpn-dashboard
cd /tmp/vpn-dashboard
```

## Step 5: Run the Installer

```bash
sudo ./deploy/install-ubuntu.sh
```

The installer will:

1. Create a system user `vpndash` (no shell, no home directory)
2. Publish the application to `/opt/vpn-dashboard/`
3. Create `/var/lib/vpn-dashboard/` for the SQLite identity database
4. Install the privileged helper script at `/usr/local/sbin/vpn-dashboard-helper.sh`
5. Configure sudoers for the `vpndash` user
6. Install and enable the systemd service
7. Configure nginx as a reverse proxy (default server on ports 80 and 443)
8. Generate a self-signed TLS certificate
9. Allow HTTP/HTTPS through `ufw` (if active)
10. Enable the OpenVPN status log (if OpenVPN is already installed)
11. Prompt for initial admin email and password

## Step 6: First Login

Open `http://<server-ip>/` in your browser. You'll see the AdminLTE login page.

Log in with the admin email and password you provided during installation.

## Step 7: Install OpenVPN (if not already installed)

If OpenVPN is not yet installed, the dashboard will automatically redirect you to the **Setup Wizard** (`/setup`).

The wizard will ask for:

| Setting | Default | Description |
|---------|---------|-------------|
| Protocol | UDP | UDP is recommended for performance |
| Port | 1194 | Standard OpenVPN port |
| DNS | System resolvers | DNS servers pushed to VPN clients |
| First client | client | Name for the first VPN profile |

Click **Install OpenVPN** and watch the live installation log. This takes 1-2 minutes.

When it finishes, you'll be redirected to the dashboard. The first `.ovpn` profile is available on the Clients page.

## Step 8: Optional — Real TLS Certificate

Replace the self-signed certificate with Let's Encrypt:

```bash
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d vpn.example.com
```

## Step 9: Verification Checklist

- [ ] `systemctl is-active vpn-dashboard` returns `active`
- [ ] `systemctl is-active nginx` returns `active`
- [ ] `http://<server-ip>/` loads the login page
- [ ] Can log in with admin credentials
- [ ] Clients page shows profiles
- [ ] Can add a new client and download the `.ovpn` file
- [ ] Can revoke a client
- [ ] Connected page updates live when a VPN client connects

## Step 10: Clean Up Seed Credentials

After your first login, remove the one-shot admin credentials:

```bash
sudo rm /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf
sudo systemctl daemon-reload
sudo systemctl restart vpn-dashboard.service
```

## Troubleshooting

### Page won't load

1. Check if the service is running: `systemctl status vpn-dashboard`
2. Check nginx: `systemctl status nginx` and `sudo nginx -t`
3. Check firewall: `sudo ufw status`

### Port 80 already in use

If Apache is installed and occupying port 80:

```bash
sudo systemctl stop apache2
sudo systemctl disable apache2
sudo systemctl restart nginx
```

### nginx returns 502 Bad Gateway

The dashboard service may not be running yet:

```bash
sudo systemctl status vpn-dashboard
sudo journalctl -u vpn-dashboard -n 50
```

Verify Kestrel is listening:

```bash
ss -tlnp | grep 5000
```

### OpenVPN status log not present

```bash
sudo /usr/local/sbin/vpn-dashboard-helper.sh enable-status
```

### Sudoers syntax error

```bash
sudo visudo -c -f /etc/sudoers.d/vpn-dashboard
```

### Install wizard stuck

Check the dashboard logs:

```bash
journalctl -u vpn-dashboard -f
```

### AppArmor issues (rare)

If AppArmor blocks the dashboard from reading the PKI:

```bash
sudo aa-complain /opt/vpn-dashboard/VPNDashboard.Website
```

## Differences from Fedora

| Feature | Ubuntu | Fedora |
|---------|--------|--------|
| Package manager | `apt` | `dnf` |
| .NET repo | Microsoft PPA required | Built-in (Fedora 39+) or Microsoft repo |
| Firewall | `ufw` | `firewalld` |
| SELinux | Not used (AppArmor instead) | `setsebool -P httpd_can_network_connect on` |
| Default server path | `/etc/nginx/sites-enabled/` | `/etc/nginx/conf.d/` |
| CRL owner group | `nogroup` | `nobody` |
