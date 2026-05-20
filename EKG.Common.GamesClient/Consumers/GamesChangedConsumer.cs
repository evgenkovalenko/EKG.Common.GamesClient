using EKG.Common.GamesClient.Messages;
using EKG.Common.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace EKG.Common.GamesClient.Consumers;

public class GamesChangedConsumer : BaseConsumer<GamesChangedMessage>
{
    private readonly IGamesClientService _gamesClient;
    private readonly ILogger<GamesChangedConsumer> _logger;

    public GamesChangedConsumer(IGamesClientService gamesClient, ILogger<GamesChangedConsumer> logger)
    {
        _gamesClient = gamesClient;
        _logger = logger;
    }

    protected override async Task ConsumeMessage(ConsumeContext<GamesChangedMessage> context)
    {
        _logger.LogInformation("GamesChangedConsumer: received notification, refreshing games cache");
        await _gamesClient.InitAsync();
    }
}
