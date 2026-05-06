using Renci.SshNet;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Services;

public interface ISshSessionFactory
{
    Renci.SshNet.ConnectionInfo CreateConnectionInfo(TargetServer server);
}

public class SshSessionFactory : ISshSessionFactory
{
    private readonly CredentialProtector _protector;

    public SshSessionFactory(CredentialProtector protector)
    {
        _protector = protector;
    }

    public Renci.SshNet.ConnectionInfo CreateConnectionInfo(TargetServer server)
    {
        var password = _protector.Unprotect(server.PasswordEncrypted);
        return new Renci.SshNet.ConnectionInfo(
            server.Host,
            server.Port,
            server.Username,
            new PasswordAuthenticationMethod(server.Username, password))
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }
}
