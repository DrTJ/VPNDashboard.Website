namespace VPNDashboard.Website.Models;

public class OpenVpnSettings
{
    public string ServerConfPath { get; set; } = "/etc/openvpn/server/server.conf";
    public string PkiPath { get; set; } = "/etc/openvpn/server/easy-rsa/pki";
    public string ClientCommonPath { get; set; } = "/etc/openvpn/server/client-common.txt";
    public string StatusLogPath { get; set; } = "/var/log/openvpn/openvpn-status.log";
    public string HelperScriptPath { get; set; } = "/usr/local/sbin/vpn-dashboard-helper.sh";
    public string InstallScriptPath { get; set; } = "/opt/vpn-dashboard/openvpn-install.sh";
    public string ServiceName { get; set; } = "openvpn-server@server";
    public string DocsPath { get; set; } = "/opt/vpn-dashboard/docs";
}
