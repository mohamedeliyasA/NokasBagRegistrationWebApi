# Implementation Cards — Batch 02: Bag-Number Existence Cache (C6)

**Prerequisite infrastructure already provisioned** (done directly, not cards): Cosmos container `bagnumber-cache` (PK `/id`, no unique-key policy needed — Cosmos already enforces id-uniqueness within a partition, and every document is its own partition here), the Function's Managed Identity granted Cosmos DB Built-in Data Contributor, `CosmosDb__AccountEndpoint`/`CosmosDb__DatabaseName`/`CosmosDb__BagNumberCacheContainerName` set on both the Function App and the API App Service. Local config templates (`appsettings.json`, `appsettings.Development.json`) already updated with the new container name.

**Design reference:** `REGISTRATION_REWRITE_DESIGN.md` §6.1 (C6). One addition made while implementing, within that already-approved scope: the sync needs a **backfill mode**, not just delta sync, or the cache would start empty of all historical bag numbers. Built as one shared paged loop — same code path whether it's the first-ever catch-up or a routine 15-minute run, each page committed and watermarked incrementally so a partial run still makes safe, resumable progress.

Apply phases in order; build after each phase.

---

# PHASE 1 — Shared document models (used by both API and Function)

## Card 1 — Cache and sync-state documents

Both the Function (writer) and the API (reader) need the identical shape for these two documents — that's exactly the case `Nokas.CashMgmt.Shared` exists for (three real consumers across the solution, not speculative reuse).

**File (new):** `src/Nokas.CashMgmt.Shared/Documents/BagNumberCacheDocument.cs`

```csharp
using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Shared.Documents;

public sealed class BagNumberCacheDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("syncedAt")]
    public required DateTime SyncedAt { get; init; }
}
```

**File (new):** `src/Nokas.CashMgmt.Shared/Documents/SyncStateDocument.cs`

```csharp
using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.Shared.Documents;

public sealed class SyncStateDocument
{
    public const string DocumentId = "sync-state";

    [JsonPropertyName("id")]
    public string Id { get; init; } = DocumentId;

    [JsonPropertyName("lastSuccessfulRunUtc")]
    public required DateTime LastSuccessfulRunUtc { get; init; }
}
```

**Concept to learn:** `Id` is `init`-only with a compile-time default (`DocumentId`) rather than `required` — every sync-state document is always the same single document (`id = "sync-state"`), so there's nothing for a caller to supply; hardcoding it here removes an entire class of "typo'd the id" bugs, since callers just never provide one.

---

# PHASE 2 — Function: SQL delta/backfill query

## Card 2 — Extend the SQL service with a bag-number fetch method

**File:** `src/Nokas.CashMgmt.BagRegistration.Func/Services/IBagStatusService.cs`

Add this method to the interface:
```csharp
Task<IReadOnlyList<(string BagNumber, DateTime CreatedAt)>> GetBagNumbersCreatedAfterAsync(
    DateTime sinceUtc, int maxRows, CancellationToken cancellationToken);
```

**File:** `src/Nokas.CashMgmt.BagRegistration.Func/Services/BagStatusService.cs`

Add this method to the class:
```csharp
public async Task<IReadOnlyList<(string BagNumber, DateTime CreatedAt)>> GetBagNumbersCreatedAfterAsync(
    DateTime sinceUtc, int maxRows, CancellationToken cancellationToken)
{
    await using var connection = new SqlConnection(_cacheDbConnectionString);
    await connection.OpenAsync(cancellationToken);

    const string sql = """
        SELECT TOP (@MaxRows) BagNumber, CreatedAt
        FROM dbo.Registration
        WHERE CreatedAt > @SinceUtc AND BagNumber IS NOT NULL
        ORDER BY CreatedAt ASC
        """;

    var rows = await connection.QueryAsync<(string BagNumber, DateTime CreatedAt)>(
        sql, new { SinceUtc = sinceUtc, MaxRows = maxRows });

    return rows.ToList();
}
```

**Why `ORDER BY CreatedAt ASC` matters here, specifically:** the sync advances its watermark to the newest `CreatedAt` seen in each page. If rows came back in any other order, the watermark could jump past rows that haven't been processed yet, silently skipping them forever — ascending order guarantees the watermark only ever advances to a point where everything before it has genuinely been handled.

---

# PHASE 3 — Function: Cosmos wiring

## Card 3 — Add the Cosmos SDK to the Function project

```powershell
cd src/Nokas.CashMgmt.BagRegistration.Func
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Azure.Identity
cd ../..
```

## Card 4 — Function-side Cosmos options

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Func/Configuration/CosmosDbOptions.cs`

```csharp
namespace Nokas.CashMgmt.BagRegistration.Func.Configuration;

public sealed class CosmosDbOptions
{
    public required string AccountEndpoint { get; init; }
    public required string DatabaseName { get; init; }
    public required string BagNumberCacheContainerName { get; init; }
}
```

**Why no `[Required]`/`ValidateDataAnnotations` here, unlike the API's options classes:** the Functions isolated-worker host doesn't wire up `IOptions<T>` validation the same declarative way ASP.NET Core does by default — this project reads config directly via `IConfiguration` binding in `Program.cs` (Card 5) and fails fast there if a value's missing, which is the isolated-worker-idiomatic equivalent of the same fail-at-startup principle.

## Card 5 — Register the Cosmos client in the Function's Program.cs

**File:** `src/Nokas.CashMgmt.BagRegistration.Func/Program.cs`

Current:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nokas.CashMgmt.BagRegistration.Func.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton<IBagStatusService, BagStatusService>();

builder.Build().Run();
```
New:
```csharp
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nokas.CashMgmt.BagRegistration.Func.Configuration;
using Nokas.CashMgmt.BagRegistration.Func.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var cosmosOptions = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbOptions>()
    ?? throw new InvalidOperationException("CosmosDb configuration section is missing.");
builder.Services.AddSingleton(cosmosOptions);

builder.Services.AddSingleton(_ => new CosmosClient(cosmosOptions.AccountEndpoint, new DefaultAzureCredential()));

builder.Services.AddSingleton<IBagStatusService, BagStatusService>();
builder.Services.AddSingleton<IBagNumberCacheSyncService, BagNumberCacheSyncService>();

builder.Build().Run();
```

**Why plain `DefaultAzureCredential()` here, not the environment-branched version from the API:** this project has no local-run story yet for the sync function (nobody's running it outside Azure), so there's no local-dev Managed-Identity-probing problem to work around here the way there was in the API. If that changes later, apply the same fix.

---

# PHASE 4 — Function: the sync service itself

## Card 6 — The sync service interface and implementation

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Func/Services/IBagNumberCacheSyncService.cs`

```csharp
namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public interface IBagNumberCacheSyncService
{
    Task RunAsync(CancellationToken cancellationToken);
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Func/Services/BagNumberCacheSyncService.cs`

```csharp
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Configuration;
using Nokas.CashMgmt.Shared.Documents;

namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public sealed class BagNumberCacheSyncService(
    CosmosClient cosmosClient,
    CosmosDbOptions cosmosOptions,
    IBagStatusService bagStatusService,
    ILogger<BagNumberCacheSyncService> logger) : IBagNumberCacheSyncService
{
    private static readonly TimeSpan Overlap = TimeSpan.FromMinutes(2);
    private const int PageSize = 2000;
    private const int MaxPagesPerRun = 10;
    private static readonly DateTime DefaultBackfillStart = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var container = cosmosClient.GetContainer(cosmosOptions.DatabaseName, cosmosOptions.BagNumberCacheContainerName);

        var watermark = await GetWatermarkAsync(container, cancellationToken);
        var queryFrom = watermark - Overlap;

        var totalUpserted = 0;
        for (var page = 0; page < MaxPagesPerRun; page++)
        {
            var rows = await bagStatusService.GetBagNumbersCreatedAfterAsync(queryFrom, PageSize, cancellationToken);
            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                var doc = new BagNumberCacheDocument { Id = row.BagNumber, SyncedAt = DateTime.UtcNow };
                await container.UpsertItemAsync(doc, new PartitionKey(doc.Id), cancellationToken: cancellationToken);
            }

            totalUpserted += rows.Count;
            queryFrom = rows[^1].CreatedAt;

            // Commit the watermark after every page, not just at the end of the run - a page
            // that's already upserted is safe to record as done, so a run that stops partway
            // (crash, timeout, hitting MaxPagesPerRun) still leaves real, resumable progress.
            await SaveWatermarkAsync(container, queryFrom, cancellationToken);

            if (rows.Count < PageSize)
            {
                break;
            }
        }

        logger.LogInformation("Bag-number cache sync run complete. Rows upserted: {Count}, watermark: {Watermark}",
            totalUpserted, queryFrom);
    }

    private static async Task<DateTime> GetWatermarkAsync(Container container, CancellationToken cancellationToken)
    {
        try
        {
            var response = await container.ReadItemAsync<SyncStateDocument>(
                SyncStateDocument.DocumentId, new PartitionKey(SyncStateDocument.DocumentId), cancellationToken: cancellationToken);
            return response.Resource.LastSuccessfulRunUtc;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return DefaultBackfillStart;
        }
    }

    private static async Task SaveWatermarkAsync(Container container, DateTime watermark, CancellationToken cancellationToken)
    {
        var doc = new SyncStateDocument { LastSuccessfulRunUtc = watermark };
        await container.UpsertItemAsync(doc, new PartitionKey(SyncStateDocument.DocumentId), cancellationToken: cancellationToken);
    }
}
```

**Why `MaxPagesPerRun = 10` (up to 20,000 rows per invocation) instead of looping until fully caught up:** bounds how long a single timer invocation can run. If the historical backlog is larger than that, this run makes real progress and saves its watermark, and the *next* scheduled run (15 minutes later) picks up exactly where this one left off — self-healing without needing a separate one-off backfill script.

**Why `DefaultBackfillStart = 2020-01-01`, not something like `DateTime.MinValue`:** a sync-state document that doesn't exist yet means "never synced" — starting from a real, bounded date rather than the absolute minimum avoids asking SQL Server to scan from the year 1. Adjust this date if bag registrations actually predate it — worth confirming against real data before first run.

**Concept to learn:** this is a primary-constructor class again (`BagNumberCacheSyncService(...)`), same pattern used throughout the API project — constructor parameters are directly usable as fields without declaring them separately.

---

# PHASE 5 — Function: the timer trigger

## Card 7 — The timer-triggered function itself

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Func/Functions/SyncBagNumberCacheFunction.cs`

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Services;

namespace Nokas.CashMgmt.BagRegistration.Func.Functions;

public sealed class SyncBagNumberCacheFunction(
    IBagNumberCacheSyncService syncService,
    ILogger<SyncBagNumberCacheFunction> logger)
{
    [Function("SyncBagNumberCache")]
    public async Task RunAsync([TimerTrigger("0 */15 * * * *")] TimerInfo timer, FunctionContext context)
    {
        try
        {
            await syncService.RunAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Bag-number cache sync run failed.");
            throw;
        }
    }
}
```

**Why `logger.LogCritical` then `throw` again, instead of just swallowing the exception:** logging alone would hide the failure from the Functions host's own execution-tracking (which uses the exception to mark this specific run as failed in its own telemetry). Re-throwing after logging gets you both — an explicit Critical-severity trace for the alerting story we discussed earlier, *and* the host's native failure tracking still works correctly.

**Cron note:** `"0 */15 * * * *"` is a 6-field NCronTab expression (seconds first) meaning "at second 0 of every 15th minute" — fires at :00, :15, :30, :45 past every hour.

**Checkpoint:** `dotnet build` the Function project. Should be clean.

---

# PHASE 6 — API: Cosmos config + repository

## Card 8 — Add the cache container name to the API's Cosmos options

**File:** `src/Nokas.CashMgmt.BagRegistration.Api/Configuration/CosmosDbOptions.cs`

Add one property:
```csharp
    [Required]
    public required string BagNumberCacheContainerName { get; init; }
```

## Card 9 — The cache repository

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Repositories/IBagNumberCacheRepository.cs`

```csharp
namespace Nokas.CashMgmt.BagRegistration.Api.Repositories;

public interface IBagNumberCacheRepository
{
    Task<bool> ExistsAsync(string bagNumber, CancellationToken cancellationToken);
    Task<DateTime?> GetLastSyncedAtAsync(CancellationToken cancellationToken);
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Repositories/CosmosBagNumberCacheRepository.cs`

```csharp
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.Shared.Documents;

namespace Nokas.CashMgmt.BagRegistration.Api.Repositories;

public sealed class CosmosBagNumberCacheRepository : IBagNumberCacheRepository
{
    private readonly Container _container;

    public CosmosBagNumberCacheRepository(CosmosClient cosmosClient, IOptions<CosmosDbOptions> options)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.BagNumberCacheContainerName);
    }

    public async Task<bool> ExistsAsync(string bagNumber, CancellationToken cancellationToken)
    {
        try
        {
            await _container.ReadItemAsync<BagNumberCacheDocument>(
                bagNumber, new PartitionKey(bagNumber), cancellationToken: cancellationToken);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<DateTime?> GetLastSyncedAtAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<SyncStateDocument>(
                SyncStateDocument.DocumentId, new PartitionKey(SyncStateDocument.DocumentId), cancellationToken: cancellationToken);
            return response.Resource.LastSuccessfulRunUtc;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
```

**Note:** `bagnumber-cache` and `sync-state` live in the **same container** as each other here (the sync-state document is just one more document, id `"sync-state"`, alongside all the bag-number documents) — same pattern already used for `bagreg-v2` holding both bag documents and outbox documents side by side.

**Register in `Program.cs`** (append near the other repository registrations):
```csharp
builder.Services.AddScoped<IBagNumberCacheRepository, CosmosBagNumberCacheRepository>();
```

---

# PHASE 7 — API: rewire the controller, remove the dead HTTP-based check

## Card 10 — Replace the validate-Function call with the cache check + telemetry event

**File:** `src/Nokas.CashMgmt.BagRegistration.Api/Controllers/BagRegistrationController.cs`

Current constructor and the validate block:
```csharp
public sealed class BagRegistrationController(
    IBagRegistrationRepository repository,
    IValidateBagNumberClient validateClient,
    IPayloadArchiveQueue archiveQueue,
    ILogger<BagRegistrationController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(BagRegisteredResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateBagRegistration(
        [FromBody] BagRegistrationRequestDto request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - POST rejected, token missing clientId claim.", correlationId);
            return Unauthorized(new ErrorResponse
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                CorrelationId = correlationId,
                Code = "ERR_MISSING_CLIENT_ID"
            });
        }

        var bagNumber = request.BagRegistration.BagNumber;

        var existsResult = await validateClient.ExistsAsync(bagNumber, correlationId, cancellationToken);
        if (!existsResult.IsSuccess)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ToErrorResponse(existsResult.Error!, correlationId));
        }

        if (existsResult.Value)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Bag already exists in system: {BagNumber}", correlationId, bagNumber);
            return Conflict(new ErrorResponse
            {
                Title = $"Bag {bagNumber} already exists and cannot be re-registered.",
                Status = StatusCodes.Status409Conflict,
                CorrelationId = correlationId,
                Code = "ERR_DUPLICATE_BAG",
                Field = "BagNumber"
            });
        }

        var bagLifecycleId = Guid.NewGuid().ToString();
        var document = request.BagRegistration.ToDocument(clientId, bagLifecycleId, correlationId);
```

New:
```csharp
public sealed class BagRegistrationController(
    IBagRegistrationRepository repository,
    IBagNumberCacheRepository bagNumberCache,
    TelemetryClient telemetryClient,
    IPayloadArchiveQueue archiveQueue,
    ILogger<BagRegistrationController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(BagRegisteredResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateBagRegistration(
        [FromBody] BagRegistrationRequestDto request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var clientId = User.GetClientId();

        if (string.IsNullOrEmpty(clientId))
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - POST rejected, token missing clientId claim.", correlationId);
            return Unauthorized(new ErrorResponse
            {
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                CorrelationId = correlationId,
                Code = "ERR_MISSING_CLIENT_ID"
            });
        }

        var bagNumber = request.BagRegistration.BagNumber;

        bool exists;
        DateTime? cacheLastSyncedAt;
        try
        {
            exists = await bagNumberCache.ExistsAsync(bagNumber, cancellationToken);
            cacheLastSyncedAt = await bagNumberCache.GetLastSyncedAtAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Unable to read bag-number cache for bag {BagNumber}",
                correlationId, bagNumber);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponse
            {
                Title = "Unable to validate bag number.",
                Status = StatusCodes.Status503ServiceUnavailable,
                CorrelationId = correlationId,
                Code = "ERR_VALIDATE_UNAVAILABLE"
            });
        }

        if (exists)
        {
            logger.LogWarning("CorrelationId: {CorrelationId} - Bag already exists in system: {BagNumber}", correlationId, bagNumber);
            return Conflict(new ErrorResponse
            {
                Title = $"Bag {bagNumber} already exists and cannot be re-registered.",
                Status = StatusCodes.Status409Conflict,
                CorrelationId = correlationId,
                Code = "ERR_DUPLICATE_BAG",
                Field = "BagNumber"
            });
        }

        var bagLifecycleId = Guid.NewGuid().ToString();
        var document = request.BagRegistration.ToDocument(clientId, bagLifecycleId, correlationId);
```

**Then, add the telemetry event right before the `return StatusCode(StatusCodes.Status201Created, responseBody);` line at the end of the method** (after the successful Cosmos write, so it only fires on genuine success):
```csharp
        telemetryClient.TrackEvent("BagNumberCacheCheck", new Dictionary<string, string>
        {
            ["bagNumber"] = bagNumber,
            ["bagLifecycleId"] = bagLifecycleId,
            ["clientId"] = clientId,
            ["correlationId"] = correlationId,
            ["cacheLastSyncedAt"] = cacheLastSyncedAt?.ToString("O") ?? "unknown"
        });

        return StatusCode(StatusCodes.Status201Created, responseBody);
```

**Add the using statement:**
```csharp
using Microsoft.ApplicationInsights;
using Nokas.CashMgmt.BagRegistration.Api.Repositories;
```

**Why `TelemetryClient` needs no new DI registration:** `AddApplicationInsightsTelemetry()` (already in `Program.cs` from Card 12) registers `TelemetryClient` in the container itself — it's the same instance the Serilog AI sink already uses. Just adding it as a constructor parameter is enough.

**Why the try/catch here now, instead of the old `Result<T>` pattern the HTTP client used:** a Cosmos point read either succeeds or throws — there's no separate "downstream unavailable" outcome type to model the way there was for an HTTP call, so a plain try/catch mapping straight to the existing `ERR_VALIDATE_UNAVAILABLE` response is the simplest correct translation, not a step backward from the Result-pattern principle (which is about not using exceptions for *expected* outcomes like a duplicate — a Cosmos read failure is still a genuine fault, exactly the case exceptions are for).

## Card 11 — Remove the now-dead HTTP validate client

Delete these two files entirely:
- `src/Nokas.CashMgmt.BagRegistration.Api/Services/IValidateBagNumberClient.cs`
- `src/Nokas.CashMgmt.BagRegistration.Api/Services/ValidateBagNumberClient.cs`

**File:** `src/Nokas.CashMgmt.BagRegistration.Api/Program.cs`

Remove this block entirely:
```csharp
builder.Services.AddHttpClient<IValidateBagNumberClient, ValidateBagNumberClient>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.AddStandardResilienceHandler(resilience =>
{
    resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(8);
    resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
});
```

**Do NOT remove `BagStatusFunctionOptions` or its config** (`BagStatusFunction:BaseUrl`/`:FunctionKey` in `appsettings.json`, and the Key Vault reference on the deployed app) — DELETE's status-check call, the next batch, reuses this exact same config shape to call the Function's `status` endpoint. Only the *validate-specific* HTTP client is dead; the config and the Function's own `validate` endpoint both stay (the endpoint as an internal fallback tool, per the design doc).

**Checkpoint:** `dotnet build` the whole solution from the repo root. Should be clean, zero errors.

---

# PHASE 8 — Tests

## Card 12 — Unit test for the cache-based duplicate check

**File (new):** `tests/Nokas.CashMgmt.BagRegistration.Api.Tests/Repositories/CosmosBagNumberCacheRepositoryTests.cs`

This one needs the Cosmos emulator (per the testing strategy already locked in `REGISTRATION_REWRITE_DESIGN.md` §8 — integration-level, skipped gracefully where the emulator isn't available). If you don't have the emulator running locally, skip this card for now and come back to it — it's not required to test the live behavior we've already verified by hand.

```csharp
// Structure only - fill in once the Cosmos emulator is available locally:
// 1. Seed a BagNumberCacheDocument with id="9999999"
// 2. ExistsAsync("9999999") should return true
// 3. ExistsAsync("0000000") (never seeded) should return false
// 4. Seed a SyncStateDocument, GetLastSyncedAtAsync() should return its LastSuccessfulRunUtc
// 5. With no SyncStateDocument seeded, GetLastSyncedAtAsync() should return null
```

---

# What happens on first deploy

The `bagnumber-cache` container starts empty. The first sync run will begin backfilling from `2020-01-01` (Card 6's `DefaultBackfillStart`) in pages of 2,000, up to 10 pages (20,000 rows) per 15-minute run. **Until the backfill catches up to "now," the cache will report `false` for bag numbers that actually exist** — meaning duplicate-check protection is *not yet fully reliable* immediately after first deploy. Confirm roughly how many historical rows exist in `dbo.Registration` before going live, so we know how many 15-minute cycles the initial catch-up will actually take, and don't cut over the controller to depend on this cache until that first backfill is confirmed complete.
