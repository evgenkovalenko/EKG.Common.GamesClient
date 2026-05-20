using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using EKG.Common.GamesClient.Bitbucket;
using EKG.Common.GamesClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EKG.Common.GamesClient;

public class GamesClientService : IGamesClientService
{
    private readonly IBitbucketApiClient _bitbucket;
    private readonly GamesClientOptions _options;
    private readonly ILogger<GamesClientService> _logger;

    private readonly ConcurrentDictionary<string, Game> _games = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, GameOverride>> _overrides = new();
    private readonly ConcurrentDictionary<long, GameFilter> _filters = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal GamesClientService(
        IBitbucketApiClient bitbucket,
        IOptions<GamesClientOptions> options,
        ILogger<GamesClientService> logger)
    {
        _bitbucket = bitbucket;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitAsync()
    {
        var sw = Stopwatch.StartNew();
        _games.Clear();
        _overrides.Clear();
        _filters.Clear();

        await LoadGamesAsync();
        await LoadOperatorDataAsync();

        sw.Stop();
        _logger.LogInformation(
            "GamesClient: loaded {GamesCount} games, {OverridesCount} override files across {DomainsCount} domains, {FiltersCount} filters in {ElapsedMs}ms",
            _games.Count,
            _overrides.Values.Sum(d => d.Count),
            _overrides.Count,
            _filters.Count,
            sw.ElapsedMilliseconds);
    }

    public Task<List<Game>> GetGamesPerOperatorAsync(long domainId, GameQuery? extraFilters = null)
    {
        var games = _games.Values.AsEnumerable();

        if (_filters.TryGetValue(domainId, out var filter))
            games = ApplyFilter(games, filter);

        var domainOverrides = _overrides.TryGetValue(domainId, out var ov) ? ov : null;

        var result = games
            .Select(g => domainOverrides != null && domainOverrides.TryGetValue(g.Slug, out var ovr)
                ? MergeOverride(g, ovr)
                : g)
            .ToList();

        if (extraFilters != null)
            result = ApplyExtraFilters(result, extraFilters);

        return Task.FromResult(result);
    }

    public Task<List<Game>> GetOriginalGamesAsync(GameQuery? extraFilters = null)
    {
        var games = _games.Values.ToList();

        if (extraFilters != null)
            games = ApplyExtraFilters(games, extraFilters);

        return Task.FromResult(games);
    }

    private async Task LoadGamesAsync()
    {
        var files = await _bitbucket.ListFilesAsync(_options.GamesRepo, "");
        var jsonFiles = files.Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();

        var tasks = jsonFiles.Select(async filePath =>
        {
            var content = await _bitbucket.GetFileContentAsync(_options.GamesRepo, filePath);
            if (string.IsNullOrWhiteSpace(content)) return;

            var game = JsonSerializer.Deserialize<Game>(content, JsonOptions);
            if (game?.Slug != null)
                _games[game.Slug] = game;
        });

        await Task.WhenAll(tasks);
    }

    private async Task LoadOperatorDataAsync()
    {
        var directories = await _bitbucket.ListDirectoriesAsync(_options.OperatorGamesRepo, "");

        var tasks = directories.Select(async dirPath =>
        {
            if (!long.TryParse(dirPath.TrimEnd('/'), out var domainId))
                return;

            var domainOverrides = _overrides.GetOrAdd(domainId, _ => new ConcurrentDictionary<string, GameOverride>(StringComparer.OrdinalIgnoreCase));

            var files = await _bitbucket.ListFilesAsync(_options.OperatorGamesRepo, dirPath);

            var fileTasks = files.Select(async filePath =>
            {
                var fileName = Path.GetFileName(filePath);

                if (fileName.Equals("filter.json", StringComparison.OrdinalIgnoreCase))
                {
                    var content = await _bitbucket.GetFileContentAsync(_options.OperatorGamesRepo, filePath);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var gameFilter = JsonSerializer.Deserialize<GameFilter>(content, JsonOptions);
                        if (gameFilter != null)
                        {
                            gameFilter.DomainId = domainId;
                            _filters[domainId] = gameFilter;
                        }
                    }
                    return;
                }

                if (!filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    return;

                var overrideContent = await _bitbucket.GetFileContentAsync(_options.OperatorGamesRepo, filePath);
                if (string.IsNullOrWhiteSpace(overrideContent)) return;

                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var underscoreIdx = nameWithoutExt.IndexOf('_');
                if (underscoreIdx < 0) return;

                var vendor = nameWithoutExt[..underscoreIdx];
                var slug = nameWithoutExt[(underscoreIdx + 1)..];

                var node = JsonNode.Parse(overrideContent)?.AsObject();
                if (node == null) return;

                var changedFields = node
                    .Where(kv => kv.Value != null)
                    .ToDictionary(
                        kv => kv.Key,
                        kv => JsonSerializer.Deserialize<JsonElement>(kv.Value!.ToJsonString(), JsonOptions));

                domainOverrides[slug] = new GameOverride
                {
                    DomainId = domainId,
                    Slug = slug,
                    Vendor = vendor,
                    ChangedFields = changedFields,
                };
            });

            await Task.WhenAll(fileTasks);
        });

        await Task.WhenAll(tasks);
    }

    private static Game MergeOverride(Game original, GameOverride gameOverride)
    {
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var node = JsonNode.Parse(json)!.AsObject();

        foreach (var kv in gameOverride.ChangedFields)
            node[kv.Key] = JsonNode.Parse(JsonSerializer.Serialize(kv.Value, JsonOptions));

        return JsonSerializer.Deserialize<Game>(node.ToJsonString(), JsonOptions)!;
    }

    private static IEnumerable<Game> ApplyFilter(IEnumerable<Game> games, GameFilter filter)
    {
        if (filter.IncludeVendors?.Count > 0)
            games = games.Where(g => filter.IncludeVendors.Contains(g.Vendor, StringComparer.OrdinalIgnoreCase));

        if (filter.ExcludeVendors?.Count > 0)
            games = games.Where(g => !filter.ExcludeVendors.Contains(g.Vendor, StringComparer.OrdinalIgnoreCase));

        if (filter.IncludeGameIds?.Count > 0)
            games = games.Where(g => filter.IncludeGameIds.Contains(g.Id));

        if (filter.ExcludeGameIds?.Count > 0)
            games = games.Where(g => !filter.ExcludeGameIds.Contains(g.Id));

        return games;
    }

    private static List<Game> ApplyExtraFilters(List<Game> games, GameQuery query)
    {
        var result = games.AsEnumerable();

        if (query.Vendors?.Count > 0)
            result = result.Where(g => query.Vendors.Contains(g.Vendor, StringComparer.OrdinalIgnoreCase));

        if (query.GameIds?.Count > 0)
            result = result.Where(g => query.GameIds.Contains(g.Id));

        if (query.Countries?.Count > 0)
            result = result.Where(g => !query.Countries.Any(c =>
                g.RestrictedTerritories.Contains(c, StringComparer.OrdinalIgnoreCase)));

        if (query.Tags?.Count > 0)
            result = result.Where(g => query.Tags.Any(t =>
                g.Categories.Contains(t, StringComparer.OrdinalIgnoreCase)));

        return result.ToList();
    }
}
