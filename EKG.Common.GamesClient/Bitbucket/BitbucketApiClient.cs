using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EKG.Common.GamesClient.Bitbucket;

internal class BitbucketApiClient : IBitbucketApiClient
{
    private readonly HttpClient _http;
    private readonly GamesClientOptions _options;

    private const string ApiBase = "https://api.bitbucket.org/2.0/repositories";

    public BitbucketApiClient(IHttpClientFactory httpClientFactory, IOptions<GamesClientOptions> options)
    {
        _http = httpClientFactory.CreateClient(nameof(BitbucketApiClient));
        _options = options.Value;
    }

    private string ResolveToken(string repo) =>
        repo == _options.OperatorGamesRepo ? _options.OperatorGamesRepoToken : _options.GamesRepoToken;

    private HttpRequestMessage BuildGet(string url, string repo)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ResolveToken(repo));
        return req;
    }

    public async Task<List<string>> ListFilesAsync(string repo, string path)
    {
        var entries = await ListEntriesAsync(repo, path);
        return entries.Where(e => e.Type == "commit_file").Select(e => e.Path).ToList();
    }

    public async Task<List<string>> ListDirectoriesAsync(string repo, string path)
    {
        var entries = await ListEntriesAsync(repo, path);
        return entries.Where(e => e.Type == "commit_directory").Select(e => e.Path).ToList();
    }

    public async Task<string?> GetFileContentAsync(string repo, string filePath)
    {
        var url = $"{ApiBase}/{_options.Workspace}/{repo}/src/{_options.Branch}/{filePath}";
        var response = await _http.SendAsync(BuildGet(url, repo));
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<List<BitbucketEntry>> ListEntriesAsync(string repo, string path)
    {
        var all = new List<BitbucketEntry>();
        var url = $"{ApiBase}/{_options.Workspace}/{repo}/src/{_options.Branch}/{path}?pagelen=100";

        while (url != null)
        {
            var response = await _http.SendAsync(BuildGet(url, repo));
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                break;
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var page = JsonSerializer.Deserialize<BitbucketPage>(json, JsonOptions);
            if (page == null) break;

            all.AddRange(page.Values);
            url = page.Next;
        }

        return all;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private class BitbucketPage
    {
        public List<BitbucketEntry> Values { get; set; } = [];
        public string? Next { get; set; }
    }

    private class BitbucketEntry
    {
        public string Path { get; set; } = default!;
        public string Type { get; set; } = default!;
    }
}
