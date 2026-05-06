using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace VPNDashboard.AdminWeb.Services;

public sealed class CredentialProtector
{
    private readonly IDataProtector _protector;

    public CredentialProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("VPNDashboard.AdminWeb.ServerCredentials.v1");
    }

    public byte[] Protect(string plainText)
    {
        return _protector.Protect(Encoding.UTF8.GetBytes(plainText));
    }

    public string Unprotect(byte[] cipherBytes)
    {
        return Encoding.UTF8.GetString(_protector.Unprotect(cipherBytes));
    }
}
