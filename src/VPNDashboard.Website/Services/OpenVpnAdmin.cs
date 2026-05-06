using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using VPNDashboard.Website.Models;

namespace VPNDashboard.Website.Services;

public partial class OpenVpnAdmin
{
    private readonly OpenVpnSettings _settings;
    private readonly ILogger<OpenVpnAdmin> _logger;

    public OpenVpnAdmin(IOptions<OpenVpnSettings> settings, ILogger<OpenVpnAdmin> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    [GeneratedRegex(@"[^0-9a-zA-Z_\-]")]
    private static partial Regex UnsafeCharsRegex();

    public static string SanitizeClientName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "client";
        return UnsafeCharsRegex().Replace(name, "_");
    }

    public async Task<(bool Success, string Output)> AddClientAsync(string clientName)
    {
        var sanitized = SanitizeClientName(clientName);
        if (string.IsNullOrWhiteSpace(sanitized))
            return (false, "Invalid client name");

        return await RunHelperAsync($"add {sanitized}");
    }

    public async Task<(bool Success, string Output)> RevokeClientAsync(string clientName)
    {
        var sanitized = SanitizeClientName(clientName);
        if (string.IsNullOrWhiteSpace(sanitized))
            return (false, "Invalid client name");

        return await RunHelperAsync($"revoke {sanitized}");
    }

    public async Task<(bool Success, string Output)> ReloadServerAsync()
    {
        return await RunHelperAsync("reload");
    }

    private async Task<(bool Success, string Output)> RunHelperAsync(string arguments)
    {
        try
        {
            _logger.LogInformation("Running helper: {Arguments}", arguments);

            var psi = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"{_settings.HelperScriptPath} {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return (false, "Failed to start process");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var output = stdout + (string.IsNullOrEmpty(stderr) ? "" : $"\n{stderr}");
            var success = process.ExitCode == 0;

            if (!success)
                _logger.LogWarning("Helper command failed: {Arguments} => {Output}", arguments, output);

            return (success, output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run helper: {Arguments}", arguments);
            return (false, ex.Message);
        }
    }
}
