namespace EKG.Common.GamesClient.Models;

public class GameFilter
{
    public long DomainId { get; set; }
    public List<string>? IncludeVendors { get; set; }
    public List<string>? ExcludeVendors { get; set; }
    public List<int>? IncludeGameIds { get; set; }
    public List<int>? ExcludeGameIds { get; set; }
}
