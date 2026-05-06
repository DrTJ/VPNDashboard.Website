# Troubleshooting

## Service not running

**Symptom:** AdminWeb is unreachable or returns a connection error.

```bash
sudo systemctl status vpndashboard-admin
```

If the status is `inactive` or `failed`, check the logs:

```bash
sudo journalctl -u vpndashboard-admin --no-pager -n 50
```

Common causes:
- Missing .NET 8 runtime. Install with `sudo dnf install dotnet-runtime-8.0` (Fedora) or `sudo apt install dotnet-runtime-8.0` (Debian/Ubuntu).
- Port 5050 already in use. Change the Kestrel endpoint in `appsettings.json` or via environment variable.
- Database migration failure. Check logs for EF Core migration errors.

## Sudo failures on target servers

**Symptom:** Deploy log shows `sudo: a password is required` or a permission error.

The SSH user on the target server needs passwordless sudo for specific commands. Create `/etc/sudoers.d/vpndash`:

```
vpndash ALL=(ALL) NOPASSWD: /usr/bin/systemctl stop vpn-dashboard, /usr/bin/systemctl start vpn-dashboard, /usr/bin/systemctl status vpn-dashboard, /usr/bin/mkdir, /usr/bin/tar, /usr/bin/chown
```

Apply and verify:

```bash
sudo visudo -f /etc/sudoers.d/vpndash
sudo chmod 0440 /etc/sudoers.d/vpndash
sudo -u vpndash sudo -n systemctl status vpn-dashboard  # should not prompt
```

## Git authentication errors

**Symptom:** Build fails with `fatal: Authentication failed` or `remote: Repository not found`.

- **Public repo:** verify the repository URL is correct and accessible. Try `git ls-remote {url}` from the AdminWeb host.
- **Private repo:** go to **Settings → Build** and check the GitHub username and token. The token needs `repo` scope. Regenerate if expired.
- **URL changed:** if you changed the repository URL, AdminWeb will detect the mismatch and re-clone on the next build.

## Build failures

**Symptom:** Build log shows `dotnet publish` errors.

| Error | Fix |
|---|---|
| `dotnet: command not found` | Install the .NET 8 SDK on the AdminWeb host |
| `error MSB1009: Project file does not exist` | Check `ProjectPath` in build settings — it must be relative to the repo root |
| `error CS...` compilation errors | The source code has build errors on that branch |
| `tar: command not found` | Install tar (`sudo dnf install tar` or `sudo apt install tar`) |

To reproduce the issue manually:

```bash
cd /var/lib/vpndashboard-admin/build/repo
dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj -c Release -o /tmp/test-build
```

## Deploy connection refused

**Symptom:** Deploy fails with `A connection could not be established` or `Connection refused`.

Checklist:
1. **Host/port** — verify the server's Host and Port fields are correct.
2. **SSH service** — ensure `sshd` is running on the target: `sudo systemctl status sshd`.
3. **Firewall** — ensure port 22 (or custom SSH port) is open on the target: `sudo firewall-cmd --list-ports` (Fedora) or `sudo ufw status` (Ubuntu).
4. **Credentials** — verify the username and password by attempting a manual SSH login from the AdminWeb host: `ssh {username}@{host} -p {port}`.
5. **Test connection** — use the **Test Connection** button on the server page for a quick check.

## SignalR / live log issues

**Symptom:** Build or deploy starts but the log panel stays blank or disconnects.

- **nginx config** — WebSocket upgrade headers are required. Verify your nginx config includes:

  ```nginx
  proxy_http_version 1.1;
  proxy_set_header Upgrade $http_upgrade;
  proxy_set_header Connection "upgrade";
  ```

- **Timeout** — long builds may exceed nginx's default proxy timeout. Add to the `location /` block:

  ```nginx
  proxy_read_timeout 300s;
  proxy_send_timeout 300s;
  ```

- **Browser** — check the browser console for WebSocket errors. A `401` means the session cookie expired; log in again.

## Data Protection key errors

**Symptom:** `CryptographicException` in logs, or all server passwords show as corrupt.

The Data Protection keys at `/var/lib/vpndashboard-admin/keys/` may be missing or belong to a different application instance. Restore from backup:

```bash
sudo tar -xzf dp-keys-backup.tar.gz -C /
sudo systemctl restart vpndashboard-admin
```

If no backup exists, you must re-enter all server passwords and Git tokens.

## Database locked

**Symptom:** `Microsoft.Data.Sqlite.SqliteException: database is locked`.

SQLite does not support high concurrency. This can happen if multiple AdminWeb instances point at the same database file. Ensure only one instance is running:

```bash
sudo systemctl list-units 'vpndashboard-admin*'
```
