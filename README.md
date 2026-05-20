# EKG.Common.GamesClient

Bitbucket-backed games library for EKG services. Loads original game definitions and per-operator overrides and filters from Bitbucket repositories into memory. Subscribes to RabbitMQ to refresh the cache when games change.

## How it works

On `InitAsync()` the library fetches three datasets from Bitbucket:

- **Original games** — JSON files from the games repo root, stored as `ConcurrentDictionary<slug, Game>`.
- **Operator overrides** — JSON files under `{domainId}/` directories in the operator-games repo, stored as `ConcurrentDictionary<domainId, ConcurrentDictionary<slug, GameOverride>>`. Each file contains only the fields that differ from the original.
- **Operator filters** — `{domainId}/filter.json` files from the operator-games repo, stored as `ConcurrentDictionary<domainId, GameFilter>`.

When a `GamesChangedMessage` arrives on the configured RabbitMQ queue, `InitAsync()` is called automatically to refresh all caches.

## Package

```xml
<PackageReference Include="EKG.Common.GamesClient" Version="1.0.*" />
```

## Configuration

```json
{
  "GamesClient": {
    "AccessToken": "your-bitbucket-access-token",
    "Workspace": "evkgroup",
    "GamesRepo": "ekg.caas.games",
    "OperatorGamesRepo": "ekg.caas.operatorgames",
    "Branch": "main",
    "GamesChangedQueueName": "games_changed"
  },
  "MessageBroker": {
    "Host": "amqps://rabbit-host",
    "Username": "user",
    "Password": "pass",
    "Topics": {
      "GamesChangedConsumer": {
        "QueueName": "games_changed",
        "PrefetchCount": 1,
        "ConcurrentMessageLimit": 1,
        "Timeout": 300
      }
    }
  }
}
```

| Key | Description |
|---|---|
| `GamesClient:AccessToken` | Bitbucket HTTP access token |
| `GamesClient:Workspace` | Bitbucket workspace name |
| `GamesClient:GamesRepo` | Slug of the original games repository |
| `GamesClient:OperatorGamesRepo` | Slug of the operator-games repository |
| `GamesClient:Branch` | Branch to read from (default: `main`) |
| `GamesClient:GamesChangedQueueName` | RabbitMQ queue name for refresh notifications |

## Registration

```csharp
// Register the games client service and Bitbucket HTTP client
builder.Services.AddGamesClient(builder.Configuration);

// Register the RabbitMQ consumer (chain with your other consumers)
builder.Services
    .AddMessageBroker("MessageBroker")
    .AddConsumer<GamesChangedConsumer, GamesChangedMessage>()
    .Build();

// Initialize the cache on startup
var gamesClient = app.Services.GetRequiredService<IGamesClientService>();
await gamesClient.InitAsync();
```

## Usage

```csharp
public class GameLobbyService(IGamesClientService gamesClient)
{
    // All games for an operator (filter + overrides applied)
    public Task<List<Game>> GetLobby(long domainId, string country)
        => gamesClient.GetGamesPerOperatorAsync(domainId,
               new GameQuery { Countries = [country] });

    // All original (unmodified) games
    public Task<List<Game>> GetAllGames()
        => gamesClient.GetOriginalGamesAsync();
}
```

## API Reference

### `IGamesClientService`

| Method | Description |
|---|---|
| `InitAsync()` | Loads all games, overrides and filters from Bitbucket into memory. Logs count and elapsed time. |
| `GetGamesPerOperatorAsync(domainId, extraFilters?)` | Returns games for an operator: apply domain filter → apply overrides → apply extra filters. |
| `GetOriginalGamesAsync(extraFilters?)` | Returns unmodified games from the cache with optional extra filters. |

### `GameQuery` extra filters

| Field | Description |
|---|---|
| `Countries` | Exclude games where the country appears in `RestrictedTerritories` |
| `Tags` | Include only games whose `Categories` contain at least one of the provided tags |
| `Vendors` | Include only games from these vendors |
| `GameIds` | Include only games with these IDs |

## Running tests

```bash
dotnet test EKG.Common.GamesClient.Tests.Unit
```

## Publishing

Pushing any commit to `main` triggers the GitHub Actions workflow, which builds and publishes the `EKG.Common.GamesClient` package to GitHub Packages. Version scheme: `{major}.{minor}.{run_number}`.

The workflow requires a repository secret `NUGET_READ_TOKEN` — a GitHub PAT with `read:packages` scope (needed to restore the `EKG.Common.Messages` dependency from GitHub Packages).
