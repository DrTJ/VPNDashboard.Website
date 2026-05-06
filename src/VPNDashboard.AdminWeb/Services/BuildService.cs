using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VPNDashboard.AdminWeb.Data;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Services;

public interface IBuildService
{
    Task<BuildArtifact> BuildAsync(string branch, IProgress<string> progress, CancellationToken ct = default);
}

public class BuildService : IBuildService
{
    private readonly IGitWorkspace _git;
    private readonly IBuildSettingsStore _settingsStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    public BuildService(
        IGitWorkspace git,
        IBuildSettingsStore settingsStore,
        IServiceScopeFactory scopeFactory,
        IConfiguration config)
    {
        _git = git;
        _settingsStore = settingsStore;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    public async Task<BuildArtifact> BuildAsync(string branch, IProgress<string> progress, CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync();

        progress.Report("=== Ensuring repository clone ===");
        await _git.EnsureClonedAsync(settings, progress);

        progress.Report("=== Fetching latest changes ===");
        await _git.FetchAsync(progress);

        progress.Report($"=== Checking out {branch} ===");
        await _git.CheckoutAsync(branch, progress);

        var commit = await _git.GetHeadCommitAsync(branch);
        progress.Report($"Building commit {commit.ShortSha} by {commit.Author}: {commit.Message}");

        var buildOut = Path.Combine(Path.GetTempPath(), $"vpndash-build-{Guid.NewGuid():N}");
        var artifactDir = _config.GetValue<string>("Build:ArtifactDir") ?? "/var/lib/vpndashboard-admin/artifacts";
        Directory.CreateDirectory(artifactDir);
        Directory.CreateDirectory(buildOut);

        try
        {
            var projectPath = Path.Combine(_git.RepoDir, settings.ProjectPath);
            progress.Report("=== Running dotnet publish ===");
            await RunCommandAsync("dotnet", $"publish \"{projectPath}\" -c Release -o \"{buildOut}\" --nologo", progress, ct);

            progress.Report("=== Stripping debug files ===");
            foreach (var pdb in Directory.GetFiles(buildOut, "*.pdb", SearchOption.AllDirectories))
            {
                File.Delete(pdb);
                progress.Report($"  Removed {Path.GetFileName(pdb)}");
            }

            var devSettings = Path.Combine(buildOut, "appsettings.Development.json");
            if (File.Exists(devSettings))
            {
                File.Delete(devSettings);
                progress.Report("  Removed appsettings.Development.json");
            }

            var tarName = $"vpndashboard-{branch}-{commit.ShortSha}.tar.gz";
            var tarPath = Path.Combine(artifactDir, tarName);

            progress.Report($"=== Creating {tarName} ===");
            await RunCommandAsync("tar", $"-czf \"{tarPath}\" -C \"{buildOut}\" .", progress, ct);

            var fileInfo = new FileInfo(tarPath);
            var artifact = new BuildArtifact
            {
                FileName = tarName,
                Branch = branch,
                CommitSha = commit.Sha,
                CommitMessage = commit.Message,
                CommitAuthor = commit.Author,
                CommitDate = commit.Date,
                BuiltAt = DateTime.UtcNow,
                SizeBytes = fileInfo.Length
            };

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

            var existing = await db.BuildArtifacts
                .FirstOrDefaultAsync(a => a.CommitSha == commit.Sha && a.Branch == branch, ct);
            if (existing != null)
            {
                existing.FileName = artifact.FileName;
                existing.BuiltAt = artifact.BuiltAt;
                existing.SizeBytes = artifact.SizeBytes;
                existing.CommitMessage = artifact.CommitMessage;
                existing.CommitAuthor = artifact.CommitAuthor;
                existing.CommitDate = artifact.CommitDate;
                artifact = existing;
            }
            else
            {
                db.BuildArtifacts.Add(artifact);
            }

            await db.SaveChangesAsync(ct);

            progress.Report($"=== Build complete: {tarName} ({fileInfo.Length / 1024}KB) ===");
            progress.Report("EXIT 0");
            return artifact;
        }
        finally
        {
            if (Directory.Exists(buildOut))
                Directory.Delete(buildOut, true);
        }
    }

    private static async Task RunCommandAsync(string command, string args, IProgress<string> progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;

        var outTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
                progress.Report(line);
        }, ct);

        var errTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(ct) is { } line)
                progress.Report(line);
        }, ct);

        await Task.WhenAll(outTask, errTask);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            progress.Report($"EXIT {process.ExitCode}");
            throw new InvalidOperationException($"{command} exited with code {process.ExitCode}");
        }
    }
}
