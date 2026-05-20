namespace EKG.Common.GamesClient.Models;

public class GameQuery
{
    public List<string>? Countries { get; set; }
    public List<string>? Tags { get; set; }
    public List<string>? Vendors { get; set; }
    public List<int>? GameIds { get; set; }
}
