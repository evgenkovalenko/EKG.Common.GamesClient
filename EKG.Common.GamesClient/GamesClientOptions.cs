namespace EKG.Common.GamesClient;

public class GamesClientOptions
{
    public string GamesRepoToken { get; set; } = default!;
    public string OperatorGamesRepoToken { get; set; } = default!;
    public string Workspace { get; set; } = default!;
    public string GamesRepo { get; set; } = default!;
    public string OperatorGamesRepo { get; set; } = default!;
    public string Branch { get; set; } = "main";
    public string GamesChangedQueueName { get; set; } = default!;
}
