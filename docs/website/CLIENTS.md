# Client Management

## Overview

The VPN Dashboard provides a web interface for managing OpenVPN client profiles — the certificates and configuration files that allow users to connect to the VPN. All client operations go through a privileged helper script (`vpn-dashboard-helper.sh`) executed via sudo.

## Clients Page

The **Clients** page (`/clients`) lists all configured OpenVPN client certificates read from the easy-rsa PKI directory (`pki/index.txt`). Each row shows:

- **Name** — the client's common name
- **Status** — Valid or Revoked
- **Expiry** — certificate expiration date
- **Actions** — Download and Revoke buttons

Both Admin and Viewer roles can view the client list. Admins additionally see the **Add Client** button.

## Creating Clients

Admins can create a new client profile by clicking **Add Client** on the Clients page. The dashboard:

1. Sanitizes the name (only `[0-9a-zA-Z_-]` characters allowed)
2. Calls `sudo vpn-dashboard-helper.sh add <clientName>` to generate the certificate via easy-rsa
3. Refreshes the client list

Clients can also be created automatically through the [Subscriptions](USERS-AND-SUBSCRIPTIONS.md#subscriptions) system.

## Downloading Configuration Files

Each valid client profile has a **Download** button that fetches the `.ovpn` configuration file. The download endpoint is:

```
GET /api/download/{clientName}.ovpn
```

This endpoint is protected by authentication (requires a logged-in user). It builds the `.ovpn` file on the fly by combining:

- The client template (`client-common.txt`)
- The CA certificate (`pki/ca.crt`)
- The client certificate (`pki/issued/{clientName}.crt`)
- The client private key (`pki/private/{clientName}.key`)
- The TLS auth key (`pki/ta.key`, if present)

The response is served with `Content-Type: application/x-openvpn-profile` and a `Content-Disposition` header for download.

## Revoking Clients

Admins can revoke a client by clicking **Revoke** on the Clients page. This:

1. Calls `sudo vpn-dashboard-helper.sh revoke <clientName>` to revoke the certificate and regenerate the CRL
2. If the client has an associated subscription, marks it as `Revoked` in the database
3. The client's status changes to Revoked and the profile can no longer connect to the VPN

Revocation takes effect immediately — any currently connected session for that client is terminated when OpenVPN reloads the CRL.

## Connected Clients

The **Connected** page (`/connected`) shows a real-time view of all clients currently connected to the VPN. The page displays:

| Column | Description |
|--------|-------------|
| Client | Common name of the connected client |
| Real IP | The client's actual IP address and port |
| Virtual IP | The VPN-assigned IP address |
| Received | Total bytes received from the client |
| Sent | Total bytes sent to the client |
| Connected Since | When the client established the connection |

### Real-Time Updates

The Connected page uses a `ConnectedClientsHub` (SignalR hub at `/hubs/connected-clients`) to push live updates to the browser. A `ConnectedClientsBackgroundService` polls the OpenVPN status log (`openvpn-status.log`) every 5 seconds and broadcasts the current client list to all connected dashboard users.

Both status log version 1 and version 2 formats are supported.

## Requirements

Client management requires OpenVPN to be installed on the server. If OpenVPN is not installed, the Clients page will show a warning and the dashboard redirects to the Setup Wizard (`/setup`).
