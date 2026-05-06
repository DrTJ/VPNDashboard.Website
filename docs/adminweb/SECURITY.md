# Security

## Password encryption model

Server SSH passwords and GitHub tokens are encrypted at rest using ASP.NET Data Protection.

### How it works

1. A `CredentialProtector` service wraps `IDataProtector` with the purpose string `VPNDashboard.AdminWeb.ServerCredentials.v1`.
2. When a server password is saved, `Protect()` converts the plain-text password to an encrypted `byte[]` stored in SQLite.
3. When AdminWeb needs to open an SSH connection, `Unprotect()` decrypts the bytes back to plain text in memory.
4. The same mechanism is used for GitHub personal access tokens.

### Why reversible encryption?

SSH.NET requires a plain-text password to authenticate. Unlike user login passwords (which are one-way hashed by ASP.NET Identity), SSH credentials must be recoverable. Data Protection provides authenticated encryption (AES-256-CBC + HMACSHA256) which is the recommended approach in the ASP.NET ecosystem for this use case.

## Key management

Data Protection keys are stored as XML files in the directory configured by `DataProtection:KeyDirectory` (default: `/var/lib/vpndashboard-admin/keys`).

**Critical:** if these key files are lost, all encrypted credentials (server passwords and Git tokens) become permanently unrecoverable. You would need to re-enter every password.

### Backup

```bash
sudo tar -czf dp-keys-backup-$(date +%Y%m%d).tar.gz /var/lib/vpndashboard-admin/keys/
```

### Restore

```bash
sudo mkdir -p /var/lib/vpndashboard-admin/keys
sudo tar -xzf dp-keys-backup.tar.gz -C /
sudo chown -R vpndash-admin:vpndash-admin /var/lib/vpndashboard-admin/keys
```

Key rotation is handled automatically by Data Protection. Old keys are retained for decryption; new data is encrypted with the latest key.

## HTTPS

AdminWeb transmits encrypted credentials over the wire when establishing SSH connections, but all browser traffic — including login cookies, SignalR streams, and any form data — should be protected with TLS.

**Always run AdminWeb behind an nginx reverse proxy with a valid TLS certificate.** See [INSTALL-LINUX.md](INSTALL-LINUX.md) for the nginx configuration.

Without HTTPS:
- Login cookies can be intercepted.
- Server passwords submitted in add/edit forms travel in plain text.
- SignalR WebSocket traffic (build/deploy logs) is unencrypted.

## User authentication

User login passwords are handled by ASP.NET Core Identity, which stores them as one-way salted hashes (PBKDF2 by default). These are not reversible and are unrelated to the Data Protection system used for SSH credentials.

## Network exposure

- AdminWeb listens on port 5050 by default. Do not expose this port to the internet. Use nginx as a reverse proxy.
- SSH connections to target servers are outbound-only from the AdminWeb host. Target servers do not need to reach back to AdminWeb.

## Database

The SQLite database at `/var/lib/vpndashboard-admin/admin.db` contains Identity tables (users, roles, tokens), encrypted server credentials, build settings, and artifact records. Protect it with filesystem permissions:

```bash
sudo chown vpndash-admin:vpndash-admin /var/lib/vpndashboard-admin/admin.db
sudo chmod 600 /var/lib/vpndashboard-admin/admin.db
```
