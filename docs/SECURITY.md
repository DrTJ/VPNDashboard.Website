# Security

## Architecture

The VPN Dashboard follows the principle of least privilege:

- **Service user**: Runs as `vpndash`, a system user with no shell and no home directory
- **Read-only access**: Can read the PKI directory and status log via filesystem ACLs
- **No direct root access**: All privileged operations go through a whitelisted helper script
- **Localhost only**: Kestrel binds to `127.0.0.1:5000` — never directly exposed to the internet
- **nginx terminates TLS**: The only public-facing process

## Sudoers Allowlist

The file `/etc/sudoers.d/vpn-dashboard` contains:

```
vpndash ALL=(root) NOPASSWD: /usr/local/sbin/vpn-dashboard-helper.sh
```

This allows the `vpndash` user to run ONLY the helper script as root, without a password.

## Helper Script Subcommands

The helper script (`vpn-dashboard-helper.sh`) accepts these subcommands:

| Subcommand | Arguments | What It Does |
|------------|-----------|-------------|
| `install` | `<proto> <port> <dns> <client>` | Runs Nyr's installer with the given parameters |
| `uninstall` | (none) | Runs Nyr's uninstaller (option 3) |
| `add` | `<client_name>` | Generates a new client certificate |
| `revoke` | `<client_name>` | Revokes a client certificate and regenerates the CRL |
| `reload` | (none) | Reloads the OpenVPN service |
| `enable-status` | (none) | Enables the status log in server.conf |

All client names are sanitized using the same regex as Nyr's script: only `[0-9a-zA-Z_-]` characters are allowed. Sanitization happens in both the C# layer and the bash helper.

## Input Validation

- Client names are sanitized at two layers (C# `OpenVpnAdmin.SanitizeClientName()` and bash `sanitize_name()`)
- The helper script validates argument counts and rejects empty inputs
- All process execution uses parameterized arguments (no shell interpolation)

## Vendored Script

The `openvpn-install.sh` script is vendored (pinned copy) at `/opt/vpn-dashboard/openvpn-install.sh`. It is not downloaded from the internet at runtime. This ensures:

- Reproducible installs
- Works on air-gapped hosts
- No supply chain risk from runtime downloads

## Identity & Authentication

- ASP.NET Core Identity with SQLite storage
- Passwords are hashed with PBKDF2 (ASP.NET Identity default)
- Session cookies with 7-day sliding expiration
- The initial admin password should be changed immediately after first login

## Recommendations

1. Replace the self-signed TLS certificate with Let's Encrypt
2. Remove the seed admin credentials after first login
3. Use a strong admin password (minimum 12 characters recommended)
4. Keep the dashboard and OpenVPN packages updated
5. Monitor `/var/log/vpn-dashboard/` and `journalctl -u vpn-dashboard` for anomalies
6. Consider restricting nginx access to specific IP ranges if the dashboard is not needed publicly
