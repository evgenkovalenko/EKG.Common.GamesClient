namespace EKG.Common.GamesClient.Bitbucket;

public interface IBitbucketApiClient
{
    Task<List<string>> ListFilesAsync(string repo, string path);
    Task<List<string>> ListDirectoriesAsync(string repo, string path);
    Task<string?> GetFileContentAsync(string repo, string filePath);
}
