#!/bin/bash
#
# vpn-dashboard-helper.sh — privileged helper for VPN Dashboard
# Called by the vpndash user via sudo NOPASSWD.
# Only specific subcommands are allowed; all inputs are validated.
#

set -euo pipefail

INSTALL_SCRIPT="/opt/vpn-dashboard/openvpn-install.sh"
EASYRSA_DIR="/etc/openvpn/server/easy-rsa"
SERVER_CONF="/etc/openvpn/server/server.conf"
STATUS_LOG="/var/log/openvpn/openvpn-status.log"
GROUP_NAME="nobody"
DASHBOARD_USER="vpndash"

sanitize_name() {
    echo "$1" | sed 's/[^0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_-]/_/g'
}

# Grant the dashboard service user read access to the PKI. easy-rsa often
# regenerates files (index.txt, crl.pem, issued/*) without preserving ACLs,
# so we re-apply both the access ACL and the default ACL after any change.
reapply_pki_acls() {
    if [[ -d /etc/openvpn/server ]] && command -v setfacl &>/dev/null; then
        setfacl -R -m "u:${DASHBOARD_USER}:rX" /etc/openvpn/server/ 2>/dev/null || true
        setfacl -R -d -m "u:${DASHBOARD_USER}:rX" /etc/openvpn/server/ 2>/dev/null || true
    fi
}

cmd_install() {
    local proto="$1"
    local port="$2"
    local dns="$3"
    local client
    client=$(sanitize_name "$4")

    if [[ -z "$client" ]]; then
        echo "ERROR: client name is required"
        exit 1
    fi

    if [[ ! -f "$INSTALL_SCRIPT" ]]; then
        echo "ERROR: install script not found at $INSTALL_SCRIPT"
        exit 1
    fi

    # Nyr's script does `read -N 999999 -t 0.001` on line 18 to discard pending stdin.
    # We sleep briefly so our piped answers arrive after that discard finishes.
    # Answers for a single-IP, non-NAT Fedora server:
    #   protocol choice (1=UDP, 2=TCP)
    #   port
    #   dns choice (1-7)
    #   client name
    #   "" (press any key to continue)
    { sleep 0.3; printf '%s\n' "$proto" "$port" "$dns" "$client" ""; } \
        | bash "$INSTALL_SCRIPT"

    # Enable status log after install
    cmd_enable_status
    reapply_pki_acls
}

cmd_uninstall() {
    if [[ ! -f "$INSTALL_SCRIPT" ]]; then
        echo "ERROR: install script not found at $INSTALL_SCRIPT"
        exit 1
    fi

    # Option 3 = Remove OpenVPN, then confirm with "y"
    { sleep 0.3; printf '%s\n' "3" "y"; } \
        | bash "$INSTALL_SCRIPT"
}

cmd_add() {
    local client
    client=$(sanitize_name "$1")

    if [[ -z "$client" ]]; then
        echo "ERROR: client name is required"
        exit 1
    fi

    if [[ -e "$EASYRSA_DIR/pki/issued/${client}.crt" ]]; then
        echo "ERROR: client '$client' already exists"
        exit 1
    fi

    cd "$EASYRSA_DIR"
    ./easyrsa --batch --days=3650 build-client-full "$client" nopass

    reapply_pki_acls
    echo "Client '$client' added successfully"
}

cmd_revoke() {
    local client
    client=$(sanitize_name "$1")

    if [[ -z "$client" ]]; then
        echo "ERROR: client name is required"
        exit 1
    fi

    cd "$EASYRSA_DIR"
    ./easyrsa --batch revoke "$client"
    ./easyrsa --batch --days=3650 gen-crl

    rm -f /etc/openvpn/server/crl.pem
    rm -f "$EASYRSA_DIR/pki/reqs/${client}.req"
    rm -f "$EASYRSA_DIR/pki/private/${client}.key"
    cp "$EASYRSA_DIR/pki/crl.pem" /etc/openvpn/server/crl.pem
    chown nobody:"$GROUP_NAME" /etc/openvpn/server/crl.pem

    reapply_pki_acls
    echo "Client '$client' revoked successfully"
}

cmd_reload() {
    systemctl reload openvpn-server@server
    echo "OpenVPN server reloaded"
}

cmd_enable_status() {
    if [[ ! -f "$SERVER_CONF" ]]; then
        echo "WARNING: server.conf not found, skipping status log setup"
        return
    fi

    if grep -q "^status " "$SERVER_CONF"; then
        echo "Status log already configured"
        return
    fi

    mkdir -p "$(dirname "$STATUS_LOG")"
    echo "status $STATUS_LOG 10" >> "$SERVER_CONF"
    systemctl reload openvpn-server@server 2>/dev/null || true
    echo "Status log enabled at $STATUS_LOG"
}

# Main dispatcher
case "${1:-}" in
    install)
        shift
        if [[ $# -lt 4 ]]; then
            echo "Usage: $0 install <proto> <port> <dns> <client_name>"
            exit 1
        fi
        cmd_install "$1" "$2" "$3" "$4"
        ;;
    uninstall)
        cmd_uninstall
        ;;
    add)
        shift
        if [[ $# -lt 1 ]]; then
            echo "Usage: $0 add <client_name>"
            exit 1
        fi
        cmd_add "$1"
        ;;
    revoke)
        shift
        if [[ $# -lt 1 ]]; then
            echo "Usage: $0 revoke <client_name>"
            exit 1
        fi
        cmd_revoke "$1"
        ;;
    reload)
        cmd_reload
        ;;
    enable-status)
        cmd_enable_status
        ;;
    *)
        echo "Usage: $0 {install|uninstall|add|revoke|reload|enable-status}"
        exit 1
        ;;
esac
