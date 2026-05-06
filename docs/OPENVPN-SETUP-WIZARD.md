# OpenVPN Setup Wizard

The Setup Wizard installs and configures OpenVPN directly from the VPN Dashboard web UI.

## When Does It Appear?

The wizard appears automatically when:

- You log into the dashboard and OpenVPN is not installed
- The file `/etc/openvpn/server/server.conf` does not exist

All other dashboard pages redirect to `/setup` until OpenVPN is installed.

## What Does It Ask?

### Step 1: Protocol & Port

- **Protocol**: UDP (recommended) or TCP
- **Port**: Default 1194. Can be any port 1-65535.

### Step 2: DNS

Select which DNS servers are pushed to VPN clients:

| Option | DNS Servers |
|--------|-------------|
| 1 | Default system resolvers |
| 2 | Google (8.8.8.8, 8.8.4.4) |
| 3 | Cloudflare (1.1.1.1, 1.0.0.1) |
| 4 | OpenDNS (208.67.222.222, 208.67.220.220) |
| 5 | Quad9 (9.9.9.9, 149.112.112.112) |
| 6 | Gcore (95.85.95.85, 2.56.220.2) |
| 7 | AdGuard (94.140.14.14, 94.140.15.15) |

### Step 3: First Client

Name for the first VPN client profile. Only letters, numbers, hyphens and underscores are allowed.

### Step 4: Review & Install

Review your choices, then click **Install OpenVPN**. Installation output streams live to the page.

## What Does It Do?

Behind the scenes, the wizard:

1. Calls `sudo /usr/local/sbin/vpn-dashboard-helper.sh install <proto> <port> <dns> <client>`
2. The helper runs the vendored copy of Nyr's `openvpn-install.sh` with your answers piped via stdin
3. The script installs OpenVPN, easy-rsa, generates certificates, configures firewall rules, and starts the service
4. After installation, the helper enables the OpenVPN status log for live client monitoring

## What Gets Created?

- `/etc/openvpn/server/server.conf` — OpenVPN server configuration
- `/etc/openvpn/server/easy-rsa/pki/` — Public Key Infrastructure (certificates, keys)
- `/etc/openvpn/server/client-common.txt` — Template for client configs
- The first client's `.ovpn` file (downloadable from the Clients page)
- Firewall rules for the VPN port
- `openvpn-server@server` systemd service

## Re-running the Wizard

To re-run the wizard (e.g., after uninstalling OpenVPN), simply navigate to `/setup` or uninstall OpenVPN from the Server page.
