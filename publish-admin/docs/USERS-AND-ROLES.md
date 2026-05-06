# Users and Roles

AdminWeb uses ASP.NET Core Identity with cookie authentication and three built-in roles.

## Roles

### Admin

Full access to every feature.

### Operator

Can view the dashboard, servers, and artifacts. Can trigger builds and deploys. Cannot manage users or change build settings.

### Viewer

Dashboard read-only access. Cannot build, deploy, or manage anything.

## Permission matrix

| Action | Admin | Operator | Viewer |
|---|:---:|:---:|:---:|
| View dashboard | Yes | Yes | Yes |
| View server list & status | Yes | Yes | Yes |
| View artifacts | Yes | Yes | Yes |
| Add / edit / delete servers | Yes | No | No |
| Build & package | Yes | Yes | No |
| Deploy to servers | Yes | Yes | No |
| View build settings | Yes | Yes | No |
| Edit build settings | Yes | No | No |
| Manage users | Yes | No | No |
| View docs | Yes | Yes | Yes |

## Seeding the first admin

The first admin account is created from environment variables on startup:

```bash
export VPNDASH_ADMIN_EMAIL="admin@example.com"
export VPNDASH_ADMIN_PASSWORD="YourStr0ngPass"
```

AdminWeb checks these on every start. If the email doesn't already exist in the database, a new user is created with the Admin role. If the user already exists, nothing happens.

### Password requirements

- Minimum 8 characters
- At least one digit
- At least one lowercase letter
- Uppercase and special characters are not required

## Managing users

Admins can manage users at **Account → Users** (`/account/users`).

### Adding a user

1. Click **Add User**.
2. Enter email, password, and select a role.
3. Click **Create**.

### Changing a role

1. Find the user in the list.
2. Select a new role from the dropdown.
3. Click **Save**.

You cannot demote the last remaining Admin. This prevents lockout.

### Resetting a password

1. Click **Reset Password** next to the user.
2. Enter the new password.
3. Click **Save**.

### Deleting a user

1. Click **Delete** next to the user.
2. Confirm.

You cannot delete your own account or the last Admin.

## Authorization policies

The application defines two authorization policies used by Blazor pages:

| Policy | Required role(s) |
|---|---|
| `AdminOnly` | Admin |
| `AdminOrOperator` | Admin or Operator |

Pages use `@attribute [Authorize(Policy = "AdminOnly")]` or the `AdminOrOperator` policy to restrict access.

## Session lifetime

Authentication cookies expire after 7 days with sliding expiration enabled. Any request within the 7-day window resets the timer.
