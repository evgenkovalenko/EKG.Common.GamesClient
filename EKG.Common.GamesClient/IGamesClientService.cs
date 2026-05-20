using EKG.Common.GamesClient.Models;

namespace EKG.Common.GamesClient;

public interface IGamesClientService
{
    Task InitAsync();
    Task<List<Game>> GetGamesPerOperatorAsync(long domainId, GameQuery? extraFilters = null);
    Task<List<Game>> GetOriginalGamesAsync(GameQuery? extraFilters = null);
}
