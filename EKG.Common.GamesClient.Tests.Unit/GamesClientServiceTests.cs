using EKG.Common.GamesClient.Bitbucket;
using EKG.Common.GamesClient.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EKG.Common.GamesClient.Tests.Unit;

public class GamesClientServiceTests
{
    private static GamesClientService CreateService(IBitbucketApiClient bitbucket) =>
        new(bitbucket,
            Options.Create(new GamesClientOptions
            {
                GamesRepoToken = "token",
                OperatorGamesRepoToken = "token",
                Workspace = "ws",
                GamesRepo = "games",
                OperatorGamesRepo = "opGames",
                Branch = "main",
                GamesChangedQueueName = "games_changed",
            }),
            NullLogger<GamesClientService>.Instance);

    private static string GameJson(int id, string slug, string vendor) =>
        $$"""
        {
          "id": {{id}},
          "slug": "{{slug}}",
          "vendor": "{{vendor}}",
          "vendorID": 1,
          "gameID": "{{slug}}",
          "gameCode": "{{slug}}",
          "gameBundleID": "{{slug}}",
          "contentProvider": "Provider",
          "originalVendor": "{{vendor}}",
          "enabled": true,
          "operatorVisible": true,
          "url": "https://example.com",
          "helpUrl": "",
          "theoreticalPayOut": 0.95,
          "fpp": 0.2,
          "hash": 1,
          "hash2": 1,
          "categories": [],
          "languages": [],
          "restrictedTerritories": [],
          "currencies": [],
          "maintenanceWindows": []
        }
        """;

    [Fact]
    public async Task InitAsync_LoadsGamesFromBitbucket()
    {
        var bitbucket = Substitute.For<IBitbucketApiClient>();
        bitbucket.ListFilesAsync("games", "").Returns(["game1.json", "game2.json"]);
        bitbucket.ListDirectoriesAsync("opGames", "").Returns([]);
        bitbucket.GetFileContentAsync("games", "game1.json").Returns(GameJson(1, "game-one", "Netent"));
        bitbucket.GetFileContentAsync("games", "game2.json").Returns(GameJson(2, "game-two", "Quickspin"));

        var svc = CreateService(bitbucket);
        await svc.InitAsync();

        var games = await svc.GetOriginalGamesAsync();
        Assert.Equal(2, games.Count);
    }

    [Fact]
    public async Task GetOriginalGames_ExtraFilterByVendor_ReturnsFiltered()
    {
        var bitbucket = Substitute.For<IBitbucketApiClient>();
        bitbucket.ListFilesAsync("games", "").Returns(["g1.json", "g2.json"]);
        bitbucket.ListDirectoriesAsync("opGames", "").Returns([]);
        bitbucket.GetFileContentAsync("games", "g1.json").Returns(GameJson(1, "g1", "Netent"));
        bitbucket.GetFileContentAsync("games", "g2.json").Returns(GameJson(2, "g2", "Quickspin"));

        var svc = CreateService(bitbucket);
        await svc.InitAsync();

        var games = await svc.GetOriginalGamesAsync(new GameQuery { Vendors = ["Netent"] });
        Assert.Single(games);
        Assert.Equal("g1", games[0].Slug);
    }

    [Fact]
    public async Task GetGamesPerOperator_AppliesFilter_ExcludesVendor()
    {
        var bitbucket = Substitute.For<IBitbucketApiClient>();
        bitbucket.ListFilesAsync("games", "").Returns(["g1.json", "g2.json"]);
        bitbucket.ListDirectoriesAsync("opGames", "").Returns(["1001"]);
        bitbucket.GetFileContentAsync("games", "g1.json").Returns(GameJson(1, "g1", "Netent"));
        bitbucket.GetFileContentAsync("games", "g2.json").Returns(GameJson(2, "g2", "Wazdan"));
        bitbucket.ListFilesAsync("opGames", "1001").Returns(["1001/filter.json"]);
        bitbucket.GetFileContentAsync("opGames", "1001/filter.json")
            .Returns("""{"domainId":1001,"excludeVendors":["Wazdan"]}""");

        var svc = CreateService(bitbucket);
        await svc.InitAsync();

        var games = await svc.GetGamesPerOperatorAsync(1001);
        Assert.Single(games);
        Assert.Equal("g1", games[0].Slug);
    }

    [Fact]
    public async Task GetGamesPerOperator_AppliesOverride_MergesChangedFields()
    {
        var bitbucket = Substitute.For<IBitbucketApiClient>();
        bitbucket.ListFilesAsync("games", "").Returns(["g1.json"]);
        bitbucket.ListDirectoriesAsync("opGames", "").Returns(["1001"]);
        bitbucket.GetFileContentAsync("games", "g1.json").Returns(GameJson(1, "g1", "Netent"));
        bitbucket.ListFilesAsync("opGames", "1001").Returns(["1001/Netent_g1.json"]);
        bitbucket.GetFileContentAsync("opGames", "1001/Netent_g1.json")
            .Returns("""{"enabled":false}""");

        var svc = CreateService(bitbucket);
        await svc.InitAsync();

        var games = await svc.GetGamesPerOperatorAsync(1001);
        Assert.Single(games);
        Assert.False(games[0].Enabled);
    }

    [Fact]
    public async Task GetGamesPerOperator_NoOverrideForDomain_ReturnsOriginal()
    {
        var bitbucket = Substitute.For<IBitbucketApiClient>();
        bitbucket.ListFilesAsync("games", "").Returns(["g1.json"]);
        bitbucket.ListDirectoriesAsync("opGames", "").Returns([]);
        bitbucket.GetFileContentAsync("games", "g1.json").Returns(GameJson(1, "g1", "Netent"));

        var svc = CreateService(bitbucket);
        await svc.InitAsync();

        var games = await svc.GetGamesPerOperatorAsync(9999);
        Assert.Single(games);
        Assert.True(games[0].Enabled);
    }

    [Fact]
    public async Task InitAsync_SkipsNonJsonFiles()
    {
        var bitbucket = Substitute.For<IBitbucketApiClient>();
        bitbucket.ListFilesAsync("games", "").Returns(["game.json", "readme.md"]);
        bitbucket.ListDirectoriesAsync("opGames", "").Returns([]);
        bitbucket.GetFileContentAsync("games", "game.json").Returns(GameJson(1, "g1", "Netent"));

        var svc = CreateService(bitbucket);
        await svc.InitAsync();

        var games = await svc.GetOriginalGamesAsync();
        Assert.Single(games);

        await bitbucket.DidNotReceive().GetFileContentAsync("games", "readme.md");
    }
}
