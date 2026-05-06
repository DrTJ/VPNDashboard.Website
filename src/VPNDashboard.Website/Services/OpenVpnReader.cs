using System.Globalization;
using Microsoft.Extensions.Options;
using VPNDashboard.Website.Models;

namespace VPNDashboard.Website.Services;

public class OpenVpnReader
{
    private readonly OpenVpnSettings _settings;
    private readonly ILogger<OpenVpnReader> _logger;

    public OpenVpnReader(IOptions<OpenVpnSettings> settings, ILogger<OpenVpnReader> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public bool IsInstalled => File.Exists(_settings.ServerConfPath);

    public List<ClientProfile> GetClientProfiles()
    {
        var profiles = new List<ClientProfile>();
        var indexPath = Path.Combine(_settings.PkiPath, "index.txt");

        if (!File.Exists(indexPath))
            return profiles;

        foreach (var line in File.ReadAllLines(indexPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('\t');
            if (parts.Length < 4)
                continue;

            var cn = parts.Length >= 6 ? ExtractCN(parts[5]) : parts[^1];
            if (cn == "server")
                continue;

            var profile = new ClientProfile
            {
                Name = cn,
                Serial = parts[3],
                Status = parts[0] switch
                {
                    "V" => ClientStatus.Valid,
                    "R" => ClientStatus.Revoked,
                    "E" => ClientStatus.Expired,
                    _ => ClientStatus.Valid
                }
            };

            if (parts[1].Length > 0 && parts[1] != "unknown")
            {
                if (DateTime.TryParseExact(parts[1].TrimEnd('Z'), "yyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry))
                    profile.ExpiryDate = expiry;
            }

            if (profile.Status == ClientStatus.Revoked && parts[2].Length > 0)
            {
                if (DateTime.TryParseExact(parts[2].TrimEnd('Z'), "yyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var revoked))
                    profile.RevocationDate = revoked;
            }

            profiles.Add(profile);
        }

        return profiles;
    }

    public List<ConnectedClient> GetConnectedClients()
    {
        var clients = new List<ConnectedClient>();

        if (!File.Exists(_settings.StatusLogPath))
            return clients;

        try
        {
            var lines = File.ReadAllLines(_settings.StatusLogPath);
            var inClientList = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("Common Name,"))
                {
                    inClientList = true;
                    continue;
                }

                if (line.StartsWith("ROUTING TABLE"))
                {
                    inClientList = false;
                    continue;
                }

                if (!inClientList || string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 5)
                    continue;

                clients.Add(new ConnectedClient
                {
                    CommonName = parts[0],
                    RealAddress = parts[1],
                    BytesReceived = long.TryParse(parts[2], out var br) ? br : 0,
                    BytesSent = long.TryParse(parts[3], out var bs) ? bs : 0,
                    ConnectedSince = DateTime.TryParse(parts[4], CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var cs) ? cs : DateTime.UtcNow
                });
            }

            // Parse routing table for virtual addresses
            var inRoutingTable = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("ROUTING TABLE"))
                {
                    inRoutingTable = true;
                    continue;
                }

                if (line.StartsWith("Virtual Address,"))
                    continue;

                if (line.StartsWith("GLOBAL STATS"))
                    break;

                if (!inRoutingTable || string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 3)
                    continue;

                var client = clients.FirstOrDefault(c => c.CommonName == parts[1]);
                if (client != null && !parts[0].Contains(':'))
                    client.VirtualAddress = parts[0];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse OpenVPN status log");
        }

        return clients;
    }

    public ServerStatus GetServerStatus()
    {
        var status = new ServerStatus { IsInstalled = IsInstalled };

        if (!status.IsInstalled)
            return status;

        try
        {
            var confLines = File.ReadAllLines(_settings.ServerConfPath);
            foreach (var line in confLines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("port ") && int.TryParse(trimmed[5..], out var p))
                    status.Port = p;
                else if (trimmed.StartsWith("proto "))
                    status.Protocol = trimmed[6..].Trim();
                else if (trimmed.StartsWith("local "))
                    status.ListenAddress = trimmed[6..].Trim();
                else if (trimmed.StartsWith("server "))
                    status.Subnet = trimmed[7..].Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse server.conf");
        }

        var profiles = GetClientProfiles();
        status.TotalProfiles = profiles.Count;
        status.ActiveProfiles = profiles.Count(p => p.Status == ClientStatus.Valid);
        status.RevokedProfiles = profiles.Count(p => p.Status == ClientStatus.Revoked);
        status.ConnectedClients = GetConnectedClients().Count;

        return status;
    }

    public async Task<bool> IsServiceRunningAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"is-active {_settings.ServiceName}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return false;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output.Trim() == "active";
        }
        catch
        {
            return false;
        }
    }

    public string? BuildOvpnFile(string clientName)
    {
        var clientCommonPath = _settings.ClientCommonPath;
        var inlinePath = Path.Combine(_settings.PkiPath, "inline", "private", $"{clientName}.inline");

        if (!File.Exists(clientCommonPath) || !File.Exists(inlinePath))
            return null;

        var commonLines = File.ReadAllLines(clientCommonPath)
            .Where(l => !l.TrimStart().StartsWith('#'));
        var inlineLines = File.ReadAllLines(inlinePath)
            .Where(l => !l.TrimStart().StartsWith('#'));

        return string.Join('\n', commonLines.Concat(inlineLines)) + '\n';
    }

    public async Task<string> GetJournalLogsAsync(int lines = 50)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "journalctl",
                Arguments = $"-u {_settings.ServiceName} -n {lines} --no-pager",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return "Failed to read journal";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        catch (Exception ex)
        {
            return $"Error reading journal: {ex.Message}";
        }
    }

    private static string ExtractCN(string subject)
    {
        // Subject format: /CN=clientname
        var cnPrefix = "/CN=";
        var idx = subject.IndexOf(cnPrefix, StringComparison.Ordinal);
        if (idx >= 0)
            return subject[(idx + cnPrefix.Length)..];

        // Fallback: might just be CN=clientname
        if (subject.StartsWith("CN=", StringComparison.Ordinal))
            return subject[3..];

        return subject;
    }
}
