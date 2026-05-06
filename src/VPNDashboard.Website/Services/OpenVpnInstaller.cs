using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using VPNDashboard.Website.Hubs;
using VPNDashboard.Website.Models;

namespace VPNDashboard.Website.Services;

public class OpenVpnInstaller
{
    private readonly OpenVpnSettings _settings;
    private readonly IHubContext<InstallerHub> _hubContext;
    private readonly ILogger<OpenVpnInstaller> _logger;

    public OpenVpnInstaller(
        IOptions<OpenVpnSettings> settings,
        IHubContext<InstallerHub> hubContext,
        ILogger<OpenVpnInstaller> logger)
    {
        _settings = settings.Value;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<bool> InstallAsync(string protocol, string port, string dns, string clientName)
    {
        var sanitizedClient = OpenVpnAdmin.SanitizeClientName(clientName);
        var args = $"install {protocol} {port} {dns} {sanitizedClient}";

        return await RunWithLiveOutputAsync(args);
    }

    public async Task<bool> UninstallAsync()
    {
        return await RunWithLiveOutputAsync("uninstall");
    }

    private async Task<bool> RunWithLiveOutputAsync(string arguments)
    {
        try
        {
            _logger.LogInformation("Running installer helper: {Arguments}", arguments);

            await _hubContext.Clients.All.SendAsync("InstallerLog", $"[Starting] sudo {_settings.HelperScriptPath} {arguments}");

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
            {
                await _hubContext.Clients.All.SendAsync("InstallerLog", "[ERROR] Failed to start process");
                return false;
            }

            var stdoutTask = ReadStreamAsync(process.StandardOutput, "stdout");
            var stderrTask = ReadStreamAsync(process.StandardError, "stderr");

            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;
            var statusMsg = success ? "[DONE] Operation completed successfully" : $"[ERROR] Process exited with code {process.ExitCode}";
            await _hubContext.Clients.All.SendAsync("InstallerLog", statusMsg);
            await _hubContext.Clients.All.SendAsync("InstallerComplete", success);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installer failed: {Arguments}", arguments);
            await _hubContext.Clients.All.SendAsync("InstallerLog", $"[ERROR] {ex.Message}");
            await _hubContext.Clients.All.SendAsync("InstallerComplete", false);
            return false;
        }
    }

    private async Task ReadStreamAsync(System.IO.StreamReader reader, string streamName)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            await _hubContext.Clients.All.SendAsync("InstallerLog", line);
        }
    }
}
