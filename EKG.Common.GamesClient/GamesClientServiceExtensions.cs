using EKG.Common.GamesClient.Bitbucket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EKG.Common.GamesClient;

public static class GamesClientServiceExtensions
{
    public static IServiceCollection AddGamesClient(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GamesClientOptions>(configuration.GetSection("GamesClient"));

        services.AddHttpClient(nameof(BitbucketApiClient));

        services.AddSingleton<IBitbucketApiClient>(sp => new BitbucketApiClient(
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<GamesClientOptions>>()));

        services.AddSingleton<IGamesClientService>(sp => new GamesClientService(
            sp.GetRequiredService<IBitbucketApiClient>(),
            sp.GetRequiredService<IOptions<GamesClientOptions>>(),
            sp.GetRequiredService<ILogger<GamesClientService>>()));

        return services;
    }
}
