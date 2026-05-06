# Installing VPN Dashboard on Fedora

This guide walks through installing the VPN Dashboard on a Fedora server.

## Prerequisites

- **Fedora 36 or later** with root access
- **TUN device** available (`/dev/net/tun`)
- **OpenVPN does NOT need to be pre-installed** — the dashboard's Setup Wizard will install it for you. If you've already installed it via Nyr's script, the dashboard will detect it automatically.

## Step 1: Install .NET 8 Runtime and nginx

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

## Step 2: Get the Dashboard

Clone the repository or download a release tarball:

```bash
git clone <repo-url> /tmp/vpn-dashboard
cd /tmp/vpn-dashboard
```

## Step 3: Run the Installer

```bash
sudo ./deploy/install.sh
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
9. Open firewall ports for HTTP and HTTPS
10. Set SELinux boolean `httpd_can_network_connect`
11. Enable the OpenVPN status log (if OpenVPN is already installed)
12. Prompt for initial admin email and password

## Step 4: First Login

Open `http://<server-ip>/` in your browser. You'll see the AdminLTE login page.

Log in with the admin email and password you provided during installation.

## Step 5: Install OpenVPN (if not already installed)

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

## Step 6: Optional — Real TLS Certificate

Replace the self-signed certificate with Let's Encrypt:

```bash
sudo dnf install -y certbot python3-certbot-nginx
sudo certbot --nginx -d vpn.example.com
```

## Step 7: Verification Checklist

- [ ] `systemctl is-active vpn-dashboard` returns `active`
- [ ] `systemctl is-active nginx` returns `active`
- [ ] `http://<server-ip>/` loads the login page
- [ ] Can log in with admin credentials
- [ ] Clients page shows profiles
- [ ] Can add a new client and download the `.ovpn` file
- [ ] Can revoke a client
- [ ] Connected page updates live when a VPN client connects

## Step 8: Clean Up Seed Credentials

After your first login, remove the one-shot admin credentials:

```bash
sudo rm /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf
sudo systemctl daemon-reload
sudo systemctl restart vpn-dashboard.service
```

## Troubleshooting

### Page won't load

1. Check if the service is running: `systemctl status vpn-dashboard`
2. Check nginx: `systemctl status nginx` and `nginx -t`
3. Check firewall: `firewall-cmd --list-all`

### SELinux denials

```bash
sudo ausearch -m avc -ts recent
sudo setsebool -P httpd_can_network_connect on
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
