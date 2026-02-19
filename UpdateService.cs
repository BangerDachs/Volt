using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Windows;

namespace Volt
{
    internal sealed class UpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly string _owner;
        private readonly string _repo;

        public UpdateService(string owner, string repo)
        {
            _owner = owner;
            _repo = repo;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Volt");
        }

        public async Task CheckForUpdatesAsync(Window ownerWindow, CancellationToken cancellationToken)
        {
            var latest = await GetLatestReleaseAsync(cancellationToken);
            if (latest?.Version is null || latest.HtmlUrl is null)
            {
                return;
            }

            var current = GetCurrentVersion();
            if (current is null || latest.Version <= current)
            {
                return;
            }

            var result = MessageBox.Show(
                ownerWindow,
                $"Neue Version {latest.Version} verfügbar. Möchtest du die Release-Seite öffnen?",
                "Update verfügbar",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(latest.HtmlUrl)
                {
                    UseShellExecute = true
                });
            }
        }

        private async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
                var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url, cancellationToken);
                if (release?.TagName is null || string.IsNullOrWhiteSpace(release.HtmlUrl))
                {
                    return null;
                }

                if (!TryParseVersion(release.TagName, out var version))
                {
                    return null;
                }

                return new ReleaseInfo(version, release.HtmlUrl);
            }
            catch
            {
                return null;
            }
        }

        private static Version? GetCurrentVersion()
        {
            return Assembly.GetEntryAssembly()?.GetName().Version;
        }

        private static bool TryParseVersion(string tagName, out Version version)
        {
            var trimmed = tagName.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[1..];
            }

            return Version.TryParse(trimmed, out version);
        }

        private sealed record ReleaseInfo(Version Version, string HtmlUrl);

        private sealed record GitHubRelease(
            [property: JsonPropertyName("tag_name")] string TagName,
            [property: JsonPropertyName("html_url")] string HtmlUrl);
    }
}
