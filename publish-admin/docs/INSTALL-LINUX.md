# Installation (Linux)

Production installation of VPNDashboard AdminWeb on a Linux server.

## Quick install

```bash
export VPNDASH_ADMIN_EMAIL="admin@example.com"
export VPNDASH_ADMIN_PASSWORD="YourStr0ngPass"
sudo -E bash deploy/install-adminweb.sh
```

The script performs the following:

1. Publishes the AdminWeb project to `/opt/vpndashboard-admin`.
2. Creates the data directory at `/var/lib/vpndashboard-admin` (database, keys, build workspace, artifacts).
3. Creates a `vpndash-admin` system user.
4. Installs and enables a `vpndashboard-admin.service` systemd unit.
5. Seeds the first admin account from the environment variables.

## Required environment variables

| Variable | Purpose |
|---|---|
| `VPNDASH_ADMIN_EMAIL` | Email address for the initial admin user |
| `VPNDASH_ADMIN_PASSWORD` | Password for the initial admin user (min 8 chars, must include a digit and lowercase letter) |

These are only needed on first run. The admin account persists in the SQLite database.

## nginx reverse proxy

AdminWeb listens on `http://0.0.0.0:5050` by default. Put it behind nginx with TLS:

```nginx
server {
    listen 443 ssl http2;
    server_name admin.example.com;

    ssl_certificate     /etc/letsencrypt/live/admin.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/admin.example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5050;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

server {
    listen 80;
    server_name admin.example.com;
    return 301 https://$host$request_uri;
}
```

The `Upgrade` / `Connection` headers are required for SignalR WebSocket transport.

## TLS with Let's Encrypt

```bash
sudo dnf install certbot python3-certbot-nginx   # Fedora/RHEL
# or
sudo apt install certbot python3-certbot-nginx    # Debian/Ubuntu

sudo certbot --nginx -d admin.example.com
```

## Firewall

Open only the ports you need:

```bash
# Fedora/RHEL (firewalld)
sudo firewall-cmd --permanent --add-service=http
sudo firewall-cmd --permanent --add-service=https
sudo firewall-cmd --reload

# Debian/Ubuntu (ufw)
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
```

Do **not** expose port 5050 directly. Let nginx handle external traffic.

## Data Protection keyring

ASP.NET Data Protection keys are stored at:

```
/var/lib/vpndashboard-admin/keys/
```

These keys encrypt server passwords and Git tokens. **If you lose the keys, all stored credentials become unrecoverable.** Back them up:

```bash
sudo tar -czf dp-keys-backup.tar.gz /var/lib/vpndashboard-admin/keys/
```

Restore on a new host before starting the service:

```bash
sudo mkdir -p /var/lib/vpndashboard-admin/keys
sudo tar -xzf dp-keys-backup.tar.gz -C /
sudo chown -R vpndash-admin:vpndash-admin /var/lib/vpndashboard-admin/keys
```

## Verifying the installation

```bash
sudo systemctl status vpndashboard-admin
# should show: active (running)

curl -s http://127.0.0.1:5050/account/login | head -5
# should return HTML
```

## Updating AdminWeb

To update to a newer version:

```bash
cd /path/to/VPNDashboard.Website
git pull
sudo -E bash deploy/install-adminweb.sh
```

The install script re-publishes and restarts the service. Database migrations run automatically on startup.
