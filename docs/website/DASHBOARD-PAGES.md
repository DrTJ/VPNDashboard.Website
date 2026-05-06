# Dashboard Pages

This document describes each page in the VPN Dashboard web application.

## Index — Dashboard (`/`)

The main landing page after login. It displays an overview of the VPN server with summary cards showing total client profiles, active (valid) profiles, revoked profiles, and the number of currently connected clients. Each card links to the relevant detail page. If OpenVPN is not installed, a callout directs the admin to the Setup Wizard.

## Clients (`/clients`)

Lists all OpenVPN client profiles read from the easy-rsa PKI directory. Each row shows the client name, certificate status (Valid or Revoked), and expiry date. Viewers can download `.ovpn` configuration files; Admins can also add new clients and revoke existing ones. A link to the Subscriptions page is provided for managing time-based access.

## Connected (`/connected`)

Provides real-time monitoring of currently connected VPN clients. A table shows each client's common name, real IP address, VPN-assigned virtual IP, bytes received and sent, and connection timestamp. The page auto-refreshes every 5 seconds via a SignalR hub (`ConnectedClientsHub`) backed by a background service that polls the OpenVPN status log.

## Server (`/server`)

Displays OpenVPN server configuration details and operational controls. Admins can reload the OpenVPN service and view journal logs. The page also includes a Danger Zone section where admins can uninstall OpenVPN entirely (with a confirmation prompt). Server configuration values are read directly from `server.conf`.

## Setup (`/setup`)

The OpenVPN installation wizard, accessible only to Admins. It appears automatically when OpenVPN is not installed and walks through choosing the protocol, port, DNS servers, and first client name. Clicking Install streams the installation output live to the page. Once complete, the dashboard redirects to the main overview.

## Subscriptions (`/subscriptions`)

An Admin-only page for managing time-based client subscriptions. Admins can create subscriptions that automatically issue and revoke OpenVPN profiles on a schedule. Each subscription shows its profile name, schedule type (Unlimited or Periodic), start/end dates, current status, and any errors. Subscriptions can be edited, cancelled, or left to expire automatically.

## Docs (`/docs`, `/docs/{slug}`)

An in-app documentation viewer that renders the project's markdown files. The page lists all available documentation topics and displays the selected document's content rendered as HTML. The markdown files are read from the configured `DocsPath` directory on the server.

## Users (`/account/users`)

An Admin-only user management page. Admins can create new dashboard users with email and password, assign roles (Admin or Viewer), reset passwords, and delete accounts. The page lists all registered users with their email, role, and available actions.

## Manage Account (`/account/manage`)

A personal settings page available to all authenticated users. Users can change their own password from this page. It is accessed via the profile menu in the top-right corner of the layout.
