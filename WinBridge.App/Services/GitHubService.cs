using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinBridge.App.Services;

/// <summary>
/// Contains basic repository information fetched from GitHub API.
/// </summary>
public class GitHubRepoInfo
{
    public string DefaultBranch { get; set; } = "main";
    public List<string> Branches { get; set; } = new();
}

/// <summary>
/// Provides methods to interact with GitHub API for template fetching.
/// </summary>
public class GitHubService
{
    private readonly HttpClient _client;

    public GitHubService()
    {
        _client = new HttpClient();
        
        _client.DefaultRequestHeaders.Add("User-Agent", "WinBridge-App");
    }

    /// <summary>
    /// Retrieves details about a public GitHub repository.
    /// </summary>
    /// <param name="url">The HTML URL of the repository.</param>
    /// <returns>Repository information including default branch and branch list.</returns>
    public async Task<GitHubRepoInfo> GetRepositoryDetails(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        var (owner, repo) = ParseGitHubUrl(url);

        var repoInfoUrl = $"https://api.github.com/repos/{owner}/{repo}";
        var repoResponse = await _client.GetFromJsonAsync<GitHubApiRepoResponse>(repoInfoUrl);

        var branchesUrl = $"https://api.github.com/repos/{owner}/{repo}/branches";
        var branchesResponse = await _client.GetFromJsonAsync<List<GitHubApiBranchResponse>>(branchesUrl);

        return new GitHubRepoInfo
        {
            DefaultBranch = repoResponse?.DefaultBranch ?? "main",
            Branches = branchesResponse?.Select(b => b.Name).ToList() ?? new List<string>()
        };
    }

    /// <summary>
    /// Downloads a repository archive (zip) for a specific branch and extracts it.
    /// Moves content to the destination path.
    /// </summary>
    /// <param name="url">The repository HTML URL.</param>
    /// <param name="branch">The branch name to download.</param>
    /// <param name="destinationPath">The local path where content should be placed.</param>
    public async Task DownloadBranchZip(string url, string branch, string destinationPath)
    {
        var (owner, repo) = ParseGitHubUrl(url);
        var zipUrl = $"https://github.com/{owner}/{repo}/archive/refs/heads/{branch}.zip";
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"winbridge_{Guid.NewGuid()}.zip");
        var tempExtractPath = Path.Combine(Path.GetTempPath(), $"winbridge_{Guid.NewGuid()}");

        try
        {
            
            var bytes = await _client.GetByteArrayAsync(zipUrl);
            await File.WriteAllBytesAsync(tempZipPath, bytes);

            if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
            ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

            var extractedDirs = Directory.GetDirectories(tempExtractPath);
            string sourceDir = extractedDirs.Length > 0 ? extractedDirs[0] : tempExtractPath;

            if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destinationPath, Path.GetFileName(file));
                
                if (File.Exists(destFile)) File.Delete(destFile);
                File.Move(file, destFile);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destDir = Path.Combine(destinationPath, dirName);

                MoveDirectory(dir, destDir);
            }
        }
        finally
        {
            
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
            if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
        }
    }

    private void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(target))
        {
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(source))
        {
            string destFile = Path.Combine(target, Path.GetFileName(file));
            if (File.Exists(destFile)) File.Delete(destFile);
            File.Move(file, destFile);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            string destDir = Path.Combine(target, Path.GetFileName(dir));
            MoveDirectory(dir, destDir);
        }
    }

    private (string Owner, string Repo) ParseGitHubUrl(string url)
    {

        url = url.TrimEnd('/');
        
        var uri = new Uri(url);
        if (uri.Host != "github.com")
            throw new ArgumentException("Only github.com URLs are supported.");

        var segments = uri.Segments; 

        if (segments.Length < 3)
            throw new ArgumentException("Invalid GitHub URL format.");

        var owner = segments[1].TrimEnd('/');
        var repo = segments[2].TrimEnd('/');

        if (repo.EndsWith(".git")) repo = repo[..^4];

        return (owner, repo);
    }

    private class GitHubApiRepoResponse
    {
        [JsonPropertyName("default_branch")]
        public string DefaultBranch { get; set; } = "main";
    }

    private class GitHubApiBranchResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
