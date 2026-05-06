using System.Diagnostics;
using System.Text;
using VPNDashboard.AdminWeb.Models;

namespace VPNDashboard.AdminWeb.Services;

public interface IGitWorkspace
{
    Task EnsureClonedAsync(BuildSettings settings, IProgress<string>? progress = null);
    Task FetchAsync(IProgress<string>? progress = null);
    Task CheckoutAsync(string branch, IProgress<string>? progress = null);
    Task<CommitInfo> GetHeadCommitAsync(string branch);
    string RepoDir { get; }
}

public class GitWorkspace : IGitWorkspace
{
    private readonly IConfiguration _config;
    private readonly IBuildSettingsStore _settingsStore;

    public GitWorkspace(IConfiguration config, IBuildSettingsStore settingsStore)
    {
        _config = config;
        _settingsStore = settingsStore;
    }

    public string RepoDir => Path.Combine(
        _config.GetValue<string>("Build:WorkDir") ?? "/var/lib/vpndashboard-admin/build",
        "repo");

    public async Task EnsureClonedAsync(BuildSettings settings, IProgress<string>? progress = null)
    {
        var repoDir = RepoDir;
        var cloneUrl = BuildAuthenticatedUrl(settings);

        if (Directory.Exists(Path.Combine(repoDir, ".git")))
        {
            var currentUrl = await RunGitAsync(repoDir, "remote get-url origin", null);
            var rawSavedUrl = settings.RepositoryUrl.TrimEnd('/');
            var rawCurrentUrl = currentUrl.Trim().TrimEnd('/');

            if (!StripCredentials(rawCurrentUrl).Equals(StripCredentials(rawSavedUrl), StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report("Repository URL changed. Re-cloning...");
                Directory.Delete(repoDir, true);
            }
            else
            {
                await RunGitAsync(repoDir, $"remote set-url origin {cloneUrl}", progress);
                return;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(repoDir)!);
        progress?.Report($"Cloning {settings.RepositoryUrl}...");
        await RunGitAsync(null, $"clone {cloneUrl} {repoDir}", progress, settings);
    }

    public async Task FetchAsync(IProgress<string>? progress = null)
    {
        await RunGitAsync(RepoDir, "fetch --all --prune", progress);
    }

    public async Task CheckoutAsync(string branch, IProgress<string>? progress = null)
    {
        await RunGitAsync(RepoDir, $"checkout {branch}", progress);
        await RunGitAsync(RepoDir, $"reset --hard origin/{branch}", progress);
    }

    public async Task<CommitInfo> GetHeadCommitAsync(string branch)
    {
        string sha;
        try
        {
            sha = (await RunGitAsync(RepoDir, $"rev-parse origin/{branch}", null)).Trim();
        }
        catch (InvalidOperationException)
        {
            // Most common cause: caller asked for a branch that doesn't exist on the
            // remote (e.g. "main" when the repo's default is "master"). Replace the
            // raw "exited with code 128" with something the user can act on.
            var available = await ListRemoteBranchesAsync();
            var hint = available.Count > 0
                ? $" Available branches: {string.Join(", ", available)}."
                : "";
            throw new InvalidOperationException(
                $"Branch '{branch}' not found on remote 'origin'.{hint}");
        }

        var log = await RunGitAsync(RepoDir, $"log -1 --format=%H%n%an%n%aI%n%s origin/{branch}", null);
        var lines = log.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        return new CommitInfo
        {
            Sha = lines.Length > 0 ? lines[0] : sha,
            Author = lines.Length > 1 ? lines[1] : "unknown",
            Date = lines.Length > 2 && DateTime.TryParse(lines[2], out var dt) ? dt : DateTime.UtcNow,
            Message = lines.Length > 3 ? lines[3] : ""
        };
    }

    private async Task<List<string>> ListRemoteBranchesAsync()
    {
        try
        {
            var output = await RunGitAsync(RepoDir,
                "for-each-ref --format=%(refname:short) refs/remotes/origin", null);
            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r) && !r.EndsWith("/HEAD"))
                .Select(r => r.StartsWith("origin/") ? r["origin/".Length..] : r)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private string BuildAuthenticatedUrl(BuildSettings settings)
    {
        var url = settings.RepositoryUrl;
        var token = _settingsStore.DecryptToken(settings);
        if (string.IsNullOrEmpty(token)) return url;

        var uri = new Uri(url);
        var user = string.IsNullOrEmpty(settings.GitHubUsername) ? "oauth2" : settings.GitHubUsername;
        return $"{uri.Scheme}://{user}:{token}@{uri.Host}{uri.PathAndQuery}";
    }

    private static string StripCredentials(string url)
    {
        try
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}{uri.PathAndQuery}";
        }
        catch
        {
            return url;
        }
    }

    private static string MaskToken(string text, BuildSettings? settings)
    {
        if (settings == null) return text;
        return text;
    }

    private async Task<string> RunGitAsync(string? workDir, string args, IProgress<string>? progress, BuildSettings? settings = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = workDir != null ? $"-C {workDir} {args}" : args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var output = new StringBuilder();

        var outTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                output.AppendLine(line);
                progress?.Report(line);
            }
        });

        var errTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                progress?.Report(line);
            }
        });

        await Task.WhenAll(outTask, errTask);
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {args} exited with code {process.ExitCode}");

        return output.ToString();
    }
}
