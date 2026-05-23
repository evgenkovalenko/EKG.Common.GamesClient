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
    "GamesRepoToken": "token-for-ekg.caas.games",
    "OperatorGamesRepoToken": "token-for-ekg.caas.operatorgames",
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
| `GamesClient:GamesRepoToken` | Bitbucket access token for the games repository |
| `GamesClient:OperatorGamesRepoToken` | Bitbucket access token for the operator-games repository |
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

## Game Model

The `Game` class maps directly to the JSON files in the Bitbucket games repository. All nested objects use concrete types:

| Property | Type | Description |
|---|---|---|
| `Additional` | `Dictionary<string, AdditionalFeature>` | Feature flags (e.g. `highStake`, `fullScreen`); each has `DisplayName` and a `Value` (bool/string/number) |
| `Bonus` | `GameBonus` | `Contribution` (double), `Overridable` (bool) |
| `Creation` | `GameCreation` | `LastModified`, `Time`, `NewGameExpiryTime` (DateTime), `UniversalId` |
| `PlayMode` | `GamePlayMode` | `Anonymity`, `Fun`, `RealMoney` (bool) |
| `Popularity` | `GamePopularity` | `Coefficient` (double) |
| `Presentation` | `GamePresentation` | Localized string dictionaries for `GameName`, `Thumbnail`, `Logo`, etc.; `Icons` keyed by pixel size |
| `Property` | `GameProperty` | `FreeSpin`, `HitFrequency`, `Terminal`, `Width`, `Height`, `License` |
| `Report` | `GameReport` | `Category`, `InvoicingGroup` |
| `RuleUrl` | `Dictionary<string, string>` | Locale → URL map |
| `Currencies` | `JsonElement` | Structure varies by vendor |
| `MaintenanceWindows` | `JsonElement` | Structure varies by vendor |
| `VendorLimits` | `JsonElement` | Structure varies by vendor |

All supporting types are defined in `Models/GameTypes.cs`.

---

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

Pushing any commit to `main` triggers the GitHub Actions workflow at [evgenkovalenko/EKG.Common.GamesClient](https://github.com/evgenkovalenko/EKG.Common.GamesClient), which builds and publishes the `EKG.Common.GamesClient` package to GitHub Packages. Version scheme: `{major}.{minor}.{run_number}`.

The workflow requires a repository secret `NUGET_READ_TOKEN` — a GitHub PAT with `read:packages` scope (needed to restore the `EKG.Common.Messages` dependency from GitHub Packages).

```bash
gh secret set NUGET_READ_TOKEN --repo evgenkovalenko/EKG.Common.GamesClient
```
