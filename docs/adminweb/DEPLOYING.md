# Deploying

AdminWeb deploys a build artifact to a target server over SSH in one click.

## Deploy flow

1. Navigate to the **Artifacts** page or the **Server Detail** page.
2. Select the artifact you want to deploy.
3. Click **Deploy** and choose the target server.
4. AdminWeb connects to the server via SSH and runs the deploy sequence.
5. Live output streams to the browser via SignalR.

## Remote commands

The following commands are executed on the target server in order:

```bash
# 1. Upload artifact via SCP
scp artifact.tar.gz → /tmp/vpndashboard-deploy.tar.gz

# 2. Stop the service
sudo systemctl stop {ServiceName}

# 3. Ensure install directory exists
sudo mkdir -p {InstallDir}

# 4. Extract the tarball
sudo tar -xzf /tmp/vpndashboard-deploy.tar.gz -C {InstallDir}

# 5. Fix file ownership
sudo chown -R {ServiceUser}:{ServiceUser} {InstallDir}

# 6. Start the service
sudo systemctl start {ServiceName}

# 7. Clean up
rm -f /tmp/vpndashboard-deploy.tar.gz

# 8. Verify status
sudo systemctl --no-pager --lines=10 status {ServiceName}
```

All `sudo` commands require passwordless sudo on the target server. See [Servers — Sudoers](SERVERS.md#sudoers-configuration-on-target-servers) for the required configuration.

## What gets updated

After a successful deploy, AdminWeb writes back to the server record:

| Field | Value |
|---|---|
| `LastDeployedAt` | Current UTC timestamp |
| `LastDeployedCommitSha` | Full commit SHA from the artifact |
| `LastDeployedArtifactName` | Filename of the deployed tarball |

These values appear on the dashboard and server detail page.

## Retry / redeploy

There is no special retry mechanism. To retry a failed deploy or redeploy the same (or a different) artifact, just click **Deploy** again. The sequence is idempotent: it stops the service, overwrites the install directory, fixes ownership, and starts the service.

## Rollback

To roll back to a previous version, deploy an older artifact. All past build artifacts remain in `Build:ArtifactDir` until manually deleted.

## Error handling

- **Artifact not found** — if the `.tar.gz` file is missing from disk, the deploy fails before opening an SSH connection.
- **SSH connection failure** — check the host, port, username, and password. Use **Test Connection** on the server page.
- **sudo permission denied** — configure passwordless sudo as described in [Servers](SERVERS.md).
- **Service fails to start** — the deploy still completes (extraction is done), but the final `systemctl status` output will show the error. Check the application logs on the target server.
