# Users and Subscriptions

## Authentication

The VPN Dashboard uses **ASP.NET Core Identity** with a SQLite database for user authentication. Users sign in with an email address and password.

### Password Policy

| Requirement | Value |
|-------------|-------|
| Minimum length | 8 characters |
| Require digit | Yes (`0`–`9`) |
| Require lowercase | Yes (`a`–`z`) |
| Require uppercase | No |
| Require special character | No |

## Roles

There are two roles:

| Role | Access |
|------|--------|
| **Admin** | Full access — manage VPN clients, server operations, install/uninstall OpenVPN, manage users, and manage subscriptions |
| **Viewer** | Read-only — view client profiles, connected clients, server status, and download `.ovpn` files |

Role checks are enforced at the page level using `[Authorize(Roles = "Admin")]` on admin-only pages and components.

## Seeding the First Admin

On first run, the application creates an initial admin account using two environment variables:

| Variable | Description |
|----------|-------------|
| `VPNDASH_ADMIN_EMAIL` | Email for the initial admin account |
| `VPNDASH_ADMIN_PASSWORD` | Password for the initial admin account |

These are typically set via a systemd drop-in file:

```bash
mkdir -p /etc/systemd/system/vpn-dashboard.service.d
cat > /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf << 'EOF'
[Service]
Environment=VPNDASH_ADMIN_EMAIL=admin@example.com
Environment=VPNDASH_ADMIN_PASSWORD=Admin12345
EOF

systemctl daemon-reload
systemctl restart vpn-dashboard
```

The admin is only seeded when no user with that email exists yet. If the password does not meet the policy requirements, the seed fails silently — check `journalctl -u vpn-dashboard` for details.

**Remove the seed file after your first login** so the password is not stored in plaintext:

```bash
sudo rm /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf
sudo systemctl daemon-reload
```

## User Management

Admins can manage dashboard users at **Administration > Users** (`/account/users`). Available actions:

- **Create** a new user with email, password, and role assignment
- **Edit role** — promote a Viewer to Admin or demote an Admin to Viewer
- **Reset password** — set a new password for any user
- **Delete** — remove a user account entirely

All users can change their own password at **Settings** (`/account/manage`).

## Subscriptions

Subscriptions provide time-based management of OpenVPN client profiles. Instead of manually adding and revoking clients, an admin creates a subscription that automates the lifecycle.

### Subscription Model

Each subscription record tracks:

| Field | Description |
|-------|-------------|
| `ProfileName` | The OpenVPN client profile name (immutable after creation) |
| `ScheduleType` | `Unlimited` (no expiry) or `Periodic` (has a start and end date) |
| `StartDate` | When the profile should be issued (null means immediately) |
| `EndDate` | For Periodic subscriptions: when the profile is automatically revoked |
| `Status` | `Pending`, `Active`, `Expired`, or `Revoked` |

### Schedule Types

- **Unlimited**: The OpenVPN profile is issued and remains valid indefinitely until manually revoked.
- **Periodic**: The profile is issued at the start date and automatically revoked when the end date passes.

### SubscriptionService

`SubscriptionService` manages the CRUD operations for subscriptions:

- **Create** — validates the profile name, checks for duplicates, and either issues the OpenVPN profile immediately (if the start date has passed or is null) or leaves it in `Pending` status for the scheduler.
- **Update** — modifies the schedule type, dates, or notes on a pending or active subscription.
- **Cancel** — revokes the underlying OpenVPN profile (if active) and marks the subscription as `Revoked`.

When a client is revoked directly from the Clients page, the service also marks any associated subscription as revoked via `MarkRevokedByProfileAsync`.

### SubscriptionScheduler

`SubscriptionScheduler` is a `BackgroundService` that polls every 30 seconds:

1. **Activate pending subscriptions** — finds subscriptions with `Status = Pending` whose `StartDate` has arrived (or is null) and issues the OpenVPN profile via the helper script.
2. **Expire periodic subscriptions** — finds subscriptions with `Status = Active`, `ScheduleType = Periodic`, and `EndDate` in the past, then revokes the OpenVPN profile and marks the subscription as `Expired`.

Errors during activation or expiration are recorded in the subscription's `LastError` field and logged.

### How Subscriptions Link to OpenVPN Profiles

Each subscription's `ProfileName` maps directly to an OpenVPN client certificate common name. When a subscription is activated, `SubscriptionService` calls `OpenVpnAdmin.AddClientAsync(profileName)` to generate the certificate and `.ovpn` file. When it expires or is cancelled, `OpenVpnAdmin.RevokeClientAsync(profileName)` revokes the certificate and regenerates the CRL.
