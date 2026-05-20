using System.Text.Json;

namespace EKG.Common.GamesClient.Models;

public class GameOverride
{
    public long DomainId { get; set; }
    public string Slug { get; set; } = default!;
    public string Vendor { get; set; } = default!;
    public Dictionary<string, JsonElement> ChangedFields { get; set; } = [];
}
