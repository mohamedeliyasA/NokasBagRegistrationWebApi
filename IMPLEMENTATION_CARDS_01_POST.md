# Implementation Cards — Batch 01: POST Bag Registration (Vertical Slice)

**How to use this file:** apply cards in order (later cards depend on earlier ones existing). Each card is self-contained — file path, full code, why, and the concept it teaches. After each *phase* (not every card), run `dotnet build` to catch mistakes early rather than typing 40 cards blind. Ask me about a specific card by its number if anything doesn't make sense.

**Scope of this batch:** a working, testable `POST /api/cash-mgmt/bag-registration` end-to-end — new solution, new API project, new Function project (just the `validate` endpoint POST depends on), Cosmos write via transactional batch + outbox, Service Bus publish, Blob archive, auth, error handling, and unit tests. GET/DELETE, the status-code map, and counting-result come in later batches.

**Ground rules this batch follows** (from `REGISTRATION_REWRITE_DESIGN.md`, locked): System.Text.Json everywhere including Cosmos; Managed Identity for Cosmos, Blob, and Service Bus (no connection strings/keys); Result-pattern instead of exceptions-as-control-flow; hand-written mapping, no AutoMapper; `registrationType=Valuta` returns 400; `CorrelationId` + `ClientId` enriched onto every log line via `LogContext`; single Warning/Critical logging policy.

---

# PHASE 0 — Solution & Project Scaffolding

## Card 1 — Create the folder structure and solution file

Run these from the repo root (`F:\Avarn\Git\NokasBagRegistrationWebApi`). This does not touch any existing file.

```powershell
mkdir src, tests
cd src
dotnet new sln -n Nokas.CashMgmt -o ..
```

This creates `Nokas.CashMgmt.sln` at the repo root (sits alongside the old projects, doesn't reference them). We'll add projects to it in the next cards.

**Concept:** keeping the `.sln` at repo root (not inside `src/`) means `dotnet build`/`dotnet test` work from the root without extra path arguments — matches how the existing repo's projects are discoverable today.

---

## Card 2 — Create the Shared class library

```powershell
cd src
dotnet new classlib -n Nokas.CashMgmt.Shared -o Nokas.CashMgmt.Shared -f net10.0
cd ..
dotnet sln add src/Nokas.CashMgmt.Shared/Nokas.CashMgmt.Shared.csproj
```

Delete the auto-generated `Class1.cs` in that folder — we'll add real files in Phase 1.

**Why this project exists:** the error model and correlation constants are used by both the API and (eventually) the sync Function — one definition, not copy-pasted. Per the design doc, don't put anything here that only one project needs.

---

## Card 3 — Create the API project

```powershell
cd src
dotnet new webapi -n Nokas.CashMgmt.BagRegistration.Api -o Nokas.CashMgmt.BagRegistration.Api -f net10.0 --use-controllers
cd ..
dotnet sln add src/Nokas.CashMgmt.BagRegistration.Api/Nokas.CashMgmt.BagRegistration.Api.csproj
dotnet add src/Nokas.CashMgmt.BagRegistration.Api reference src/Nokas.CashMgmt.Shared
```

Delete the template's `WeatherForecast.cs` and `Controllers/WeatherForecastController.cs` — we don't need them.

**`--use-controllers`**: the `dotnet new webapi` template defaults to Minimal APIs in recent SDKs. We're deliberately using MVC controllers, matching the existing codebase's structure (attributes for `[Authorize]`, `[ProducesResponseType]`, content negotiation) — a real architectural choice, not an accident.

---

## Card 4 — Add NuGet packages to the API project

```powershell
cd src/Nokas.CashMgmt.BagRegistration.Api
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Azure.Identity
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Messaging.ServiceBus
dotnet add package Microsoft.Identity.Web
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.ApplicationInsights
dotnet add package Serilog.Sinks.Seq
dotnet add package Microsoft.ApplicationInsights.AspNetCore
cd ../..
```

No version numbers pinned here deliberately — `dotnet add package` resolves the current latest compatible with `net10.0`. Check `dotnet list package --outdated` occasionally once the project exists, but don't chase every patch release.

**One more, and it looks like a contradiction of the "System.Text.Json everywhere" decision — it isn't:**

```powershell
cd src/Nokas.CashMgmt.BagRegistration.Api
dotnet add package Newtonsoft.Json
cd ../..
```

The Microsoft.Azure.Cosmos SDK has a **build-time check** requiring `Newtonsoft.Json` (>= 10.0.2) to be referenced in any consuming project, regardless of which `CosmosSerializer` you configure — some internal SDK codepaths depend on it independently of item serialization. Card 14's custom `SystemTextJsonCosmosSerializer` still handles all of *our* document (de)serialization; this reference exists purely to satisfy the SDK's own packaging requirement, not to use Newtonsoft anywhere in our code. Don't reach for `AzureCosmosDisableNewtonsoftJsonCheck=true` instead — that suppresses a real check and risks turning a clear build error into a confusing runtime one later.

**Note — no AutoMapper.** That absence is intentional (R4 locked: hand-written mapping).

---

## Card 5 — Create the Function project

```powershell
cd src
func init Nokas.CashMgmt.BagRegistration.Func --worker-runtime dotnet-isolated --target-framework net10.0
cd Nokas.CashMgmt.BagRegistration.Func
func new --name ValidateBagNumberFunction --template "HTTP trigger" --authlevel function
cd ../..
dotnet sln add src/Nokas.CashMgmt.BagRegistration.Func/Nokas.CashMgmt.BagRegistration.Func.csproj
dotnet add src/Nokas.CashMgmt.BagRegistration.Func reference src/Nokas.CashMgmt.Shared
```

We'll replace the generated `ValidateBagNumberFunction.cs` content in Phase 7 — this just gets the scaffolding and correct trigger wiring in place. Requires Azure Functions Core Tools (`func`) installed; if `func` isn't recognized, tell me and we'll install it first.

```powershell
cd src/Nokas.CashMgmt.BagRegistration.Func
dotnet add package Microsoft.Data.SqlClient
dotnet add package Dapper
cd ../..
```

---

## Card 6 — Create the test project

```powershell
cd tests
dotnet new xunit -n Nokas.CashMgmt.BagRegistration.Api.Tests -o Nokas.CashMgmt.BagRegistration.Api.Tests -f net10.0
cd ..
dotnet sln add tests/Nokas.CashMgmt.BagRegistration.Api.Tests/Nokas.CashMgmt.BagRegistration.Api.Tests.csproj
dotnet add tests/Nokas.CashMgmt.BagRegistration.Api.Tests reference src/Nokas.CashMgmt.BagRegistration.Api
```

Delete the template's `UnitTest1.cs`.

**Checkpoint:** run `dotnet build` from the repo root now. Everything should compile (there's no real code yet, just scaffolding) with zero errors. Tell me if it doesn't before continuing.

---

# PHASE 1 — Shared Foundations

## Card 7 — Result pattern (replaces exceptions-as-control-flow)

**File (new):** `src/Nokas.CashMgmt.Shared/Results/Result.cs`

```csharp
namespace Nokas.CashMgmt.Shared.Results;

public enum ErrorKind
{
    Validation,
    Conflict,
    NotFound,
    Unauthorized,
    DownstreamUnavailable,
    Internal
}

public sealed record Error(ErrorKind Kind, string Code, string Message, string? Field = null)
{
    public static Error Validation(string code, string message, string? field = null) =>
        new(ErrorKind.Validation, code, message, field);

    public static Error Conflict(string code, string message, string? field = null) =>
        new(ErrorKind.Conflict, code, message, field);

    public static Error NotFound(string code, string message) =>
        new(ErrorKind.NotFound, code, message);

    public static Error Unauthorized(string code, string message) =>
        new(ErrorKind.Unauthorized, code, message);

    public static Error DownstreamUnavailable(string code, string message) =>
        new(ErrorKind.DownstreamUnavailable, code, message);

    public static Error Internal(string code, string message) =>
        new(ErrorKind.Internal, code, message);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
```

**Why:** today's code throws `DuplicateBagNumberException` to signal an expected business outcome (a duplicate bag), then catches it three layers up in middleware — exceptions for control flow are expensive (stack unwinding) and, worse, make it easy to accidentally let one escape uncaught. A `Result<T>` return value forces every caller to explicitly handle both outcomes at compile time; there's no "forgot to catch it" failure mode. Reserve real C# exceptions for things that are *actually* exceptional (Cosmos unreachable, unhandled bugs) — which is exactly the Warning-vs-Critical split you already have locked in the logging policy.

**Concept to learn:** this is the same shape as Rust's `Result<T, E>` or F#'s `Result` — increasingly common in modern C# as an alternative to exceptions for expected failure paths. `readonly struct` (not `class`) avoids a heap allocation for every result, which matters because this type gets constructed on every single request.

---

## Card 8 — Shared error response (wire contract, unchanged shape)

**File (new):** `src/Nokas.CashMgmt.Shared/Errors/ErrorResponse.cs`

```csharp
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.Shared.Errors;

[XmlRoot("ErrorResponse")]
public sealed class ErrorResponse
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("status")]
    public required int Status { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }

    [JsonPropertyName("controller")]
    public string? Controller { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("exception")]
    public string? Exception { get; init; }

    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; init; }
}
```

**Why unchanged shape:** this is a frozen contract (§3.1 of the design doc) — partners parsing today's `ErrorResponse` JSON must keep working. Only the *internals producing it* change (Result-pattern instead of exceptions), not the wire format.

**Why `required` instead of constructor validation:** C# 11's `required` modifier makes the compiler reject any construction that omits `Title`/`Status`/`CorrelationId` — enforced at compile time, no runtime null-checks needed for these three.

---

## Card 9 — Correlation constants

**File (new):** `src/Nokas.CashMgmt.Shared/Correlation/CorrelationConstants.cs`

```csharp
namespace Nokas.CashMgmt.Shared.Correlation;

public static class CorrelationConstants
{
    public const string HeaderName = "X-Correlation-ID";
    public const string HttpContextItemsKey = "CorrelationId";
    public const string LogPropertyName = "CorrelationId";
}
```

**Why a shared constants class for three string literals:** today's codebase has `"X-Correlation-ID"` typed as a literal string in at least three separate files (middleware, exception handler, Function invoker) — a typo in any one of them silently breaks correlation for that code path with no compiler warning. One `const`, referenced everywhere, makes that class of bug impossible.

**Checkpoint:** `dotnet build` from repo root — Shared should compile clean.

---

# PHASE 2 — API Configuration & Startup

## Card 10 — Options classes

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Configuration/CosmosDbOptions.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class CosmosDbOptions
{
    [Required]
    public required string AccountEndpoint { get; init; }

    [Required]
    public required string DatabaseName { get; init; }

    [Required]
    public required string BagRegistrationContainerName { get; init; }
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Configuration/BlobStorageOptions.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class BlobStorageOptions
{
    [Required]
    public required string AccountUri { get; init; }

    [Required]
    public required string ContainerName { get; init; }
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Configuration/ServiceBusOptions.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class ServiceBusOptions
{
    [Required]
    public required string FullyQualifiedNamespace { get; init; }

    [Required]
    public required string TopicName { get; init; }
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Configuration/BagStatusFunctionOptions.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class BagStatusFunctionOptions
{
    [Required]
    public required string BaseUrl { get; init; }

    [Required]
    public required string FunctionKey { get; init; }
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Configuration/AzureAdOptions.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public sealed class AzureAdOptions
{
    [Required]
    public required string Authority { get; init; }

    [Required]
    public required string Audience { get; init; }
}
```

**Why `AccountEndpoint`/`AccountUri`/`FullyQualifiedNamespace` instead of `KeyValue`/`ConnectionString`:** this is the Managed Identity change — these three Azure clients authenticate via `DefaultAzureCredential` against a plain resource *endpoint* (a URI or namespace host name), not a secret. There is no key or connection string anywhere in this project's configuration.

**Concept to learn:** `required` on `init`-only properties (not `{ get; set; }`) — the values are supplied once at construction/binding time and never mutated afterward. Combined with `[Required]` + `ValidateOnStart()` (next card), a missing config value fails at startup with a clear message, not as a confusing null-reference deep in a request.

---

## Card 11 — appsettings.json

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/appsettings.json`

```json
{
  "AzureAd": {
    "Authority": "",
    "Audience": ""
  },
  "CosmosDb": {
    "AccountEndpoint": "",
    "DatabaseName": "",
    "BagRegistrationContainerName": ""
  },
  "BlobStorage": {
    "AccountUri": "",
    "ContainerName": ""
  },
  "ServiceBus": {
    "FullyQualifiedNamespace": "",
    "TopicName": ""
  },
  "BagStatusFunction": {
    "BaseUrl": "",
    "FunctionKey": ""
  },
  "KeyVault": {
    "Uri": ""
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "AllowedHosts": "*"
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/appsettings.Development.json`

Fill this with your actual TEST values (Cosmos endpoint, Blob URI, Service Bus namespace, Function base URL — everything except `FunctionKey`, which still needs to come from Key Vault since it's a genuine secret, not an endpoint). Same Key Vault URI as the old project (`kv-dev-nokasapi-cashmgmt`) — your existing `az login` session already has access, confirmed earlier this session.

**Why the committed `appsettings.json` stays all-empty:** unchanged convention from the old project — real values come from Key Vault or environment variables, never committed.

---

## Card 12 — Program.cs: logging, Key Vault, options binding

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Program.cs` (start of the file — more sections added by later cards, don't run yet)

```csharp
using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

#region Logging

builder.Logging.ClearProviders();

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryClient>(),
            TelemetryConverter.Traces,
            restrictedToMinimumLevel: LogEventLevel.Warning);

    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
        configuration.WriteTo.Seq("http://localhost:5341");
    }
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.EnableAdaptiveSampling = false;
});

#endregion

#region Key Vault

builder.Host.ConfigureAppConfiguration((context, config) =>
{
    var builtConfig = config.Build();
    var keyVaultUri = builtConfig["KeyVault:Uri"];
    if (!string.IsNullOrEmpty(keyVaultUri))
    {
        config.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
    }
});

#endregion

#region Options

builder.Services.AddOptions<CosmosDbOptions>()
    .Bind(builder.Configuration.GetSection("CosmosDb"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection("BlobStorage"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection("ServiceBus"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BagStatusFunctionOptions>()
    .Bind(builder.Configuration.GetSection("BagStatusFunction"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureAdOptions>()
    .Bind(builder.Configuration.GetSection("AzureAd"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

#endregion
```

**Why the AI sink is `restrictedToMinimumLevel: Warning` from day one here** (not retrofitted later like in the old codebase): this was locked earlier this session — Information-level noise never needed to reach Application Insights in the first place. Seq and Console still get everything in Development.

**Concept to learn:** `ConfigureAppConfiguration` runs *before* the rest of the DI container is built, which is why it has to build a throwaway temporary config (`config.Build()`) just to read the Key Vault URI — the "real" configuration (with Key Vault values merged in) doesn't exist yet at that point in startup.

---

## Card 13 — Program.cs: Cosmos client with Managed Identity + STJ serializer

**Append to `Program.cs`:**

```csharp
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.Api.Infrastructure;

#region Cosmos DB

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

    var clientOptions = new CosmosClientOptions
    {
        Serializer = new SystemTextJsonCosmosSerializer(),
        ConnectionMode = ConnectionMode.Direct
    };

    return new CosmosClient(options.AccountEndpoint, new DefaultAzureCredential(), clientOptions);
});

#endregion
```

**Why `new DefaultAzureCredential()` instead of an account key:** this is the Managed Identity decision (R5) — in Azure, `DefaultAzureCredential` resolves to the App Service's system-assigned managed identity automatically; locally, it falls back to your `az login` session (`AzureCliCredential`), exactly like Key Vault access already does in this project. One credential type, one code path, works in both places without an `if (isDevelopment)` branch.

**Deploy-time requirement, not a code concern right now:** the App Service's managed identity needs the **Cosmos DB Built-in Data Contributor** role assigned on the account (a data-plane RBAC role, not the older key-based access) — this goes in the deploy guide, not here.

---

## Card 14 — The System.Text.Json Cosmos serializer

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Infrastructure/SystemTextJsonCosmosSerializer.cs`

```csharp
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace Nokas.CashMgmt.BagRegistration.Api.Infrastructure;

public sealed class SystemTextJsonCosmosSerializer : CosmosSerializer
{
    private readonly JsonSerializerOptions _options;

    public SystemTextJsonCosmosSerializer()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                return default!;
            }

            if (typeof(T) == typeof(Stream))
            {
                return (T)(object)stream;
            }

            return JsonSerializer.Deserialize<T>(stream, _options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, _options);
        stream.Position = 0;
        return stream;
    }
}
```

**Why this exists:** the Cosmos SDK defaults to Newtonsoft.Json internally, completely separately from whatever serializer your ASP.NET Core responses use. Today's codebase runs three different serialization conventions (Newtonsoft for MVC+Cosmos, System.Text.Json for Service Bus/Function calls, plus XML formatters) — this is the fix, one serializer for the whole solution, Cosmos included. This is also *why* the `ttl`-must-never-be-null lesson from the old codebase needs restating here: we'll apply `[JsonIgnore(Condition = WhenWritingNull)]` on the document model in Phase 3, same underlying Cosmos rule, different serializer.

**Concept to learn:** `CosmosSerializer` is an abstract class the SDK lets you fully replace — implementing `FromStream`/`ToStream` is the entire contract. This is a common pattern in .NET SDKs: provide a sensible default, but expose an extension point for exactly this kind of "we want one serializer everywhere" requirement.

---

## Card 15 — Program.cs: Blob and Service Bus clients with Managed Identity

**Append to `Program.cs`:**

```csharp
using Azure.Storage.Blobs;
using Azure.Messaging.ServiceBus;

#region Blob Storage

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BlobStorageOptions>>().Value;
    return new BlobServiceClient(new Uri(options.AccountUri), new DefaultAzureCredential());
});

#endregion

#region Service Bus

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
    return new ServiceBusClient(options.FullyQualifiedNamespace, new DefaultAzureCredential());
});

#endregion
```

**Deploy-time requirement (not code):** the managed identity additionally needs **Storage Blob Data Contributor** on the storage account and **Azure Service Bus Data Sender** on the namespace/topic. Same RBAC pattern as Cosmos, different roles. Goes in the deploy guide.

---

## Card 16 — Auth: JWT bearer + the registration role policy

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Configuration/AuthConfig.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Nokas.CashMgmt.BagRegistration.Api.Configuration;

public static class AuthConfig
{
    public const string RegistrationRolePolicy = "PartnerRegistrationAccess";
    public const string RegistrationRoleName = "cashbag.registration.access";

    public static void AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var azureAd = configuration.GetSection("AzureAd").Get<AzureAdOptions>()
            ?? throw new InvalidOperationException("AzureAd configuration section is missing.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = azureAd.Authority;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = azureAd.Audience
                    // Deliberately not setting RoleClaimType — the JWT handler's default inbound
                    // claim mapping already renames the token's "roles" claim to ClaimTypes.Role,
                    // which matches the default. Overriding it breaks the match (documented gotcha
                    // from the old codebase, carried forward — confirmed by decoding actual tokens).
                };
            });
    }

    public static void AddApiAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RegistrationRolePolicy, policy =>
                policy.RequireRole(RegistrationRoleName));
        });
    }
}
```

**Why this is `cashbag.registration.access` / `PartnerRegistrationAccess`, not the old `cashbag.api.access` / `PartnerAccess`:** this is the planned role split from `CLAUDE.md`, implemented as designed rather than deferred — locked in your auth-scope answer earlier this session.

**Deploy-time requirement (not code):** the App Role `cashbag.registration.access` needs to exist in the Entra app registration's manifest (`allowedMemberTypes: ["Application"]`), granted as an Application permission on each partner app, admin-consented. This is coordinated, breaking-change work for existing partners — not something this card does by itself.

**Append to `Program.cs`:**

```csharp
using Nokas.CashMgmt.BagRegistration.Api.Configuration;

builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddApiAuthorizationPolicies();
```

---

## Card 17 — Program.cs: controllers, XML formatters, validation-failure logging

**Append to `Program.cs`:**

```csharp
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.Shared.Errors;

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
})
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
})
.AddXmlSerializerFormatters();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var correlationId = context.HttpContext.Items[Nokas.CashMgmt.Shared.Correlation.CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "N/A";
        var errorMessages = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .ToList();

        var error = new ErrorResponse
        {
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            CorrelationId = correlationId,
            Code = "VALIDATION_ERROR",
            Exception = string.Join(" | ", errorMessages)
        };

        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("CorrelationId: {CorrelationId} - Model validation failed: {Errors}",
            correlationId, error.Exception);

        return new BadRequestObjectResult(error);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();
```

**Note this card builds in the validation-logging lesson from Tier 1/Card 3 of the old codebase's hardening work** *from the start*, instead of needing a retrofit — this is the whole point of doing a rewrite: known-good fixes become the original design, not a patch.

**Why `AddJsonOptions` with `DefaultIgnoreCondition = WhenWritingNull`:** this is the same "never emit a null field that a strict consumer might choke on" discipline as the Cosmos serializer — applied consistently to the wire responses too, not just Cosmos documents.

---

## Card 18 — Program.cs: middleware pipeline and app startup

**Append to `Program.cs` (this is the final section — the file is now complete for this batch):**

```csharp
var app = builder.Build();

#region Middleware

app.UseHttpsRedirection();

app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.CorrelationMiddleware>();
app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.ExceptionHandlingMiddleware>();
app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.PayloadArchivingMiddleware>();

app.UseAuthentication();
app.UseMiddleware<Nokas.CashMgmt.BagRegistration.Api.Middleware.ClientIdMiddleware>();
app.UseAuthorization();

#endregion

app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();
```

**Why `ClientIdMiddleware` sits between `UseAuthentication()` and `UseAuthorization()`:** this is the placement fix from earlier this session — `UseAuthorization()` short-circuits (writes 403 and never calls `next()`) for a valid-but-wrong-role token, so anything registered *after* it never runs for that request. Sitting between the two means `ClientId` is available (authentication already populated `context.User`) and still gets logged even when authorization then rejects the request.

We haven't written the three middleware classes yet — that's the next three cards. The project won't compile until they exist; that's expected, don't panic.

---

# PHASE 3 — Middleware

## Card 19 — CorrelationMiddleware

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Middleware/CorrelationMiddleware.cs`

```csharp
using Nokas.CashMgmt.Shared.Correlation;
using Serilog.Context;

namespace Nokas.CashMgmt.BagRegistration.Api.Middleware;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationConstants.HeaderName].ToString();

        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationConstants.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        context.Items[CorrelationConstants.HttpContextItemsKey] = correlationId;

        using (LogContext.PushProperty(CorrelationConstants.LogPropertyName, correlationId))
        {
            await next(context);
        }
    }
}
```

**Concept to learn — primary constructors:** `CorrelationMiddleware(RequestDelegate next)` in the class declaration is C# 12's primary constructor syntax — it replaces writing out a private field, a constructor, and an assignment (5 lines in the old codebase's version of this exact file) with one line. `next` is available directly in `InvokeAsync` as if it were a field. This is the current idiomatic way to write simple DI-constructed classes; we'll use it throughout this rewrite for classes with straightforward constructor injection, and *not* use it where the constructor needs real logic (null-checks, derived state) — a judgment call, not a blanket rule.

---

## Card 20 — ClientIdMiddleware

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Middleware/ClientIdMiddleware.cs`

```csharp
using Serilog.Context;

namespace Nokas.CashMgmt.BagRegistration.Api.Middleware;

public sealed class ClientIdMiddleware(RequestDelegate next)
{
    private const string LogPropertyName = "ClientId";

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = context.User.Claims.FirstOrDefault(c => c.Type is "azp" or "appid")?.Value;

        if (string.IsNullOrEmpty(clientId))
        {
            await next(context);
            return;
        }

        using (LogContext.PushProperty(LogPropertyName, clientId))
        {
            await next(context);
        }
    }
}
```

**Note:** this folds the old codebase's separate `ClaimsHelper.GetClientId` static helper directly in here, since this is currently the *only* place that needs it during this batch (the controller will get `ClientId` a different way — see Card 30). If a second consumer needs the same claim-extraction logic later, that's the signal to promote it to a shared extension method — not before, per the "don't build for hypothetical future needs" principle.

---

## Card 21 — ExceptionHandlingMiddleware

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Middleware/ExceptionHandlingMiddleware.cs`

```csharp
using System.Net;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.Azure.Cosmos;
using Nokas.CashMgmt.Shared.Correlation;
using Nokas.CashMgmt.Shared.Errors;

namespace Nokas.CashMgmt.BagRegistration.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[CorrelationConstants.HttpContextItemsKey]?.ToString() ?? context.TraceIdentifier;
            var isDev = env.IsDevelopment();

            int statusCode;
            string title;
            string code;

            if (ex is CosmosException cosmosEx)
            {
                // CosmosException.Message can be null for some batch failures — documented lesson
                // from the previous codebase, carried forward unconditionally.
                var raw = cosmosEx.Message ?? string.Empty;
                statusCode = (int)cosmosEx.StatusCode;

                if (raw.Contains("Unique index constraint violation"))
                {
                    title = "Bag number already exists. Please use a unique BagNumber.";
                    code = "ERR_DUPLICATE_BAG";
                    logger.LogWarning(cosmosEx, "CorrelationId: {CorrelationId} - Cosmos unique index violation.", correlationId);
                }
                else
                {
                    title = "A database error occurred while processing your request.";
                    code = "ERR_COSMOS_GENERIC";
                    logger.LogCritical(cosmosEx, "CorrelationId: {CorrelationId} - Cosmos DB failure.", correlationId);
                }
            }
            else
            {
                title = isDev ? ex.Message : "An unexpected error occurred.";
                statusCode = (int)HttpStatusCode.InternalServerError;
                code = "ERR_INTERNAL";
                logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Unhandled exception.", correlationId);
            }

            var error = new ErrorResponse
            {
                Title = title,
                Status = statusCode,
                CorrelationId = correlationId,
                Code = code,
                Exception = isDev ? ex.Message : null,
                StackTrace = isDev ? ex.StackTrace : null
            };

            context.Response.StatusCode = statusCode;

            var acceptsXml = context.Request.Headers.Accept.ToString().Contains("application/xml", StringComparison.OrdinalIgnoreCase);
            if (acceptsXml)
            {
                context.Response.ContentType = "application/xml";
                var serializer = new XmlSerializer(typeof(ErrorResponse));
                await using var ms = new MemoryStream();
                serializer.Serialize(ms, error);
                ms.Position = 0;
                await ms.CopyToAsync(context.Response.Body);
            }
            else
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(error,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            }
        }
    }
}
```

**Important — this middleware is now a narrower safety net than in the old codebase, on purpose.** The Result-pattern (Card 7) means *expected* outcomes — duplicate bag, validation failure, Function unreachable — never throw at all; the controller handles them directly and this middleware never sees them. What reaches here is genuinely exceptional: a Cosmos SDK exception, or a true unhandled bug. That's why there's no `DuplicateBagNumberException` branch anymore — there's no such exception type in this codebase. You'll see exactly how the controller handles the duplicate-bag case without throwing in Card 31.

---

## Card 22 — PayloadArchivingMiddleware (captures the raw body for Blob archive)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Middleware/PayloadArchivingMiddleware.cs`

```csharp
namespace Nokas.CashMgmt.BagRegistration.Api.Middleware;

public sealed class PayloadArchivingMiddleware(RequestDelegate next, Services.IPayloadArchiveQueue queue)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/api/cash-mgmt/bag-registration"))
        {
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            var correlationId = context.Items[Nokas.CashMgmt.Shared.Correlation.CorrelationConstants.HttpContextItemsKey]?.ToString() ?? "unknown";
            var blobName = $"bag_create_{Guid.NewGuid():N}.json";

            await queue.EnqueueAsync(new Services.PayloadArchiveItem(blobName, body, correlationId));
            context.Items["ArchivedBlobName"] = blobName;
        }

        await next(context);
    }
}
```

**Why `EnableBuffering()`:** an `HttpRequest.Body` stream can normally only be read once, front-to-back — reading it here would leave nothing for the controller's model binder to read afterward. `EnableBuffering()` makes the stream seekable, so after we read it and reset `Position = 0`, the rest of the pipeline can read it again from the start, unaffected.

We haven't written `IPayloadArchiveQueue` yet — that's Phase 5 (Card 27). This file won't compile in isolation; that's expected at this point in the batch.

---

# PHASE 4 — Domain: Wire DTOs, Cosmos Documents, Mapping

## Card 23 — Wire request DTO (NOK enforced, Valuta fields kept but rejected)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Models/BagRegistrationRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.BagRegistration.Api.Models;

[XmlRoot("BagRegistrationRequest")]
public sealed class BagRegistrationRequestDto
{
    [Required]
    [JsonPropertyName("bagRegistration")]
    [XmlElement("BagRegistration")]
    public required BagRegistrationDto BagRegistration { get; init; }
}

public sealed class BagRegistrationDto : IValidatableObject
{
    [Required]
    [JsonPropertyName("customerRefNumber")]
    [XmlElement("CustomerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [Required]
    [RegularExpression("^(NOK|Valuta)$", ErrorMessage = "registrationType must be NOK or Valuta.")]
    [JsonPropertyName("registrationType")]
    [XmlElement("RegistrationType")]
    public required string RegistrationType { get; init; }

    [Required]
    [JsonPropertyName("messageId")]
    [XmlElement("MessageId")]
    public required string MessageId { get; init; }

    [Required]
    [JsonPropertyName("bagNumber")]
    [XmlElement("BagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("registrationDate")]
    [XmlElement("RegistrationDate")]
    public string? RegistrationDate { get; init; }

    [JsonPropertyName("turnOverDate")]
    [XmlElement("TurnOverDate")]
    public string? TurnOverDate { get; init; }

    [JsonPropertyName("registeredNotes")]
    [XmlElement("RegisteredNotes")]
    public string? RegisteredNotes { get; init; }

    [JsonPropertyName("registeredCoins")]
    [XmlElement("RegisteredCoins")]
    public string? RegisteredCoins { get; init; }

    [JsonPropertyName("registeredChecks")]
    [XmlElement("RegisteredChecks")]
    public string? RegisteredChecks { get; init; }

    [JsonPropertyName("totalOrderAmount")]
    [XmlElement("TotalOrderAmount")]
    public string? TotalOrderAmount { get; init; }

    [JsonPropertyName("bagItemsLC")]
    [XmlElement("BagItemsLC")]
    public BagItemsLcDto? BagItemsLC { get; init; }

    // Kept for a future phase per design doc C1 — validated as always-rejected until then.
    [JsonPropertyName("bagItemsFC")]
    [XmlArray("BagItemsFC")]
    [XmlArrayItem("BagItemFC")]
    public List<BagItemFcDto>? BagItemsFC { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.Equals(RegistrationType, "Valuta", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "registrationType 'Valuta' is not supported by this API. Only NOK is supported.",
                [nameof(RegistrationType)]);

            yield break;
        }

        // registrationType is NOK from here on.
        if (string.IsNullOrWhiteSpace(TotalOrderAmount))
        {
            yield return new ValidationResult(
                "totalOrderAmount is required when registrationType is NOK.",
                [nameof(TotalOrderAmount)]);
        }

        if (BagItemsFC is { Count: > 0 })
        {
            yield return new ValidationResult(
                "bagItemsFC must be empty when registrationType is NOK.",
                [nameof(BagItemsFC)]);
        }
    }
}

public sealed class BagItemsLcDto
{
    [JsonPropertyName("seddel1000")]
    [XmlElement("Seddel1000")]
    public string? Seddel1000 { get; init; }

    [JsonPropertyName("seddel500")]
    [XmlElement("Seddel500")]
    public string? Seddel500 { get; init; }

    [JsonPropertyName("seddel200")]
    [XmlElement("Seddel200")]
    public string? Seddel200 { get; init; }

    [JsonPropertyName("seddel100")]
    [XmlElement("Seddel100")]
    public string? Seddel100 { get; init; }

    [JsonPropertyName("seddel50")]
    [XmlElement("Seddel50")]
    public string? Seddel50 { get; init; }
}

public sealed class BagItemFcDto
{
    [JsonPropertyName("itemCurrencyCode")]
    [XmlElement("ItemCurrencyCode")]
    public string? ItemCurrencyCode { get; init; }

    [JsonPropertyName("items")]
    [XmlArray("Items")]
    [XmlArrayItem("Item")]
    public List<ForeignCurrencyItemDto>? Items { get; init; }
}

public sealed class ForeignCurrencyItemDto
{
    [JsonPropertyName("Denomination")]
    [XmlElement("Denomination")]
    public string? Denomination { get; init; }

    [JsonPropertyName("CurrencyAmount")]
    [XmlElement("CurrencyAmount")]
    public string? CurrencyAmount { get; init; }
}
```

**This is C1 from the design doc, made concrete:** the `RegularExpression` still technically allows `"Valuta"` through model binding (so a partner sending it gets a *meaningful* validation message, not a generic regex-mismatch error) — the actual rejection happens in `Validate()`, with a clear, specific message and an early `yield break` so the NOK-specific rules don't also fire confusingly on a Valuta request.

**Concept to learn:** `required init` properties on DTOs mean the compiler enforces "this field must be provided," which is stronger than `[Required]` alone — `[Required]` only catches a missing/empty value at *runtime* during model validation; `required` catches a missing value at *compile time* if you ever construct this object directly in C# (e.g., in a test).

---

## Card 24 — Cosmos documents (STJ, `ttl` lesson applied)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Documents/BagRegistrationDocument.cs`

```csharp
using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.BagRegistration.Api.Documents;

public sealed class BagRegistrationDocument
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("bagLifecycleId")]
    public required string BagLifecycleId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("registrationStatus")]
    public required string RegistrationStatus { get; init; }

    [JsonPropertyName("bagRegistration")]
    public required BagRegistrationDetail BagRegistration { get; init; }

    [JsonPropertyName("operationHistory")]
    public List<OperationHistoryEntry> OperationHistory { get; init; } = [];

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "bagRegistration";
}

public sealed class BagRegistrationDetail
{
    [JsonPropertyName("customerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("registrationType")]
    public required string RegistrationType { get; init; }

    [JsonPropertyName("messageId")]
    public required string MessageId { get; init; }

    [JsonPropertyName("bagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("actionFlag")]
    public string ActionFlag { get; init; } = "Create";

    [JsonPropertyName("registeredBy")]
    public required string RegisteredBy { get; init; }

    [JsonPropertyName("registrationDate")]
    public string? RegistrationDate { get; init; }

    [JsonPropertyName("turnOverDate")]
    public string? TurnOverDate { get; init; }

    [JsonPropertyName("registeredNotes")]
    public string? RegisteredNotes { get; init; }

    [JsonPropertyName("registeredCoins")]
    public string? RegisteredCoins { get; init; }

    [JsonPropertyName("registeredChecks")]
    public string? RegisteredChecks { get; init; }

    [JsonPropertyName("totalOrderAmount")]
    public string? TotalOrderAmount { get; init; }
}

public sealed class OperationHistoryEntry
{
    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; }

    [JsonPropertyName("bagLifecycleId")]
    public required string BagLifecycleId { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("performedBy")]
    public required string PerformedBy { get; init; }

    [JsonPropertyName("archiveBlobName")]
    public string? ArchiveBlobName { get; init; }
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Documents/OutboxMessageDocument.cs`

```csharp
using System.Text.Json.Serialization;

namespace Nokas.CashMgmt.BagRegistration.Api.Documents;

public sealed class OutboxMessageDocument
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonPropertyName("clientId")]
    public required string ClientId { get; set; }

    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    [JsonPropertyName("payload")]
    public required string Payload { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "Pending";

    [JsonPropertyName("docType")]
    public string DocType { get; init; } = "outbox";

    // Reserved Cosmos property — MUST be absent (not null) until published.
    // Documented lesson from the previous codebase: a null "ttl" gets the whole
    // transactional batch rejected with 400, because ttl must be a valid integer
    // whenever the property is present at all.
    [JsonPropertyName("ttl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Ttl { get; set; }

    // Unique-key placeholder — the container's unique-key path is
    // /BagRegistration/BagNumber. Without this, Cosmos treats a document missing
    // that path as sharing one "undefined" value per partition, so a second outbox
    // write in the same partition collides with the first and fails the whole batch.
    [JsonPropertyName("bagRegistration")]
    public object BagRegistration { get; init; } = new { BagNumber = (string?)null };
}
```

**Both lessons from the old codebase's worst production incident are encoded directly into the type now**, not left as something a future developer has to remember: the `ttl` ignore-when-null attribute, and the unique-key placeholder object. Neither can be silently dropped by someone editing this file later without visibly removing the comment explaining why it's there.

---

## Card 25 — Hand-written mapping (R4 — no AutoMapper)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Mapping/BagRegistrationMappings.cs`

```csharp
using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.BagRegistration.Api.Models;

namespace Nokas.CashMgmt.BagRegistration.Api.Mapping;

public static class BagRegistrationMappings
{
    public static BagRegistrationDocument ToDocument(
        this BagRegistrationDto dto,
        string clientId,
        string bagLifecycleId,
        string correlationId)
    {
        var registeredBy = $"API_{clientId}";

        return new BagRegistrationDocument
        {
            Id = dto.BagNumber,
            ClientId = clientId,
            BagLifecycleId = bagLifecycleId,
            CorrelationId = correlationId,
            RegistrationStatus = "In-Progress",
            BagRegistration = new BagRegistrationDetail
            {
                CustomerRefNumber = dto.CustomerRefNumber,
                RegistrationType = dto.RegistrationType,
                MessageId = dto.MessageId,
                BagNumber = dto.BagNumber,
                RegisteredBy = registeredBy,
                RegistrationDate = dto.RegistrationDate,
                TurnOverDate = dto.TurnOverDate,
                RegisteredNotes = dto.RegisteredNotes,
                RegisteredCoins = dto.RegisteredCoins,
                RegisteredChecks = dto.RegisteredChecks,
                TotalOrderAmount = dto.TotalOrderAmount
            },
            OperationHistory =
            [
                new OperationHistoryEntry
                {
                    Action = "Create",
                    Timestamp = DateTime.UtcNow,
                    BagLifecycleId = bagLifecycleId,
                    CorrelationId = correlationId,
                    PerformedBy = clientId
                }
            ]
        };
    }
}
```

**Why this reads more verbosely than `_mapper.Map<T>(source)` and why that's the point:** every field assignment here is visible in a diff, greppable, and breaks at compile time if a required field on either side changes shape. An AutoMapper profile hides this same logic behind convention-based reflection — convenient at 50 mappings, actively worse than plain code at the ~4 mappings this project actually has (per R4's reasoning).

---

# PHASE 5 — Services

## Card 26 — Repository interface and implementation

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Repositories/IBagRegistrationRepository.cs`

```csharp
using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Repositories;

public interface IBagRegistrationRepository
{
    Task<Result<BagRegistrationDocument>> CreateAsync(
        BagRegistrationDocument document,
        OutboxMessageDocument outboxMessage,
        CancellationToken cancellationToken);
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Repositories/CosmosBagRegistrationRepository.cs`

```csharp
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Repositories;

public sealed class CosmosBagRegistrationRepository : IBagRegistrationRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosBagRegistrationRepository> _logger;

    public CosmosBagRegistrationRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosBagRegistrationRepository> logger)
    {
        var config = options.Value;
        _container = cosmosClient.GetContainer(config.DatabaseName, config.BagRegistrationContainerName);
        _logger = logger;
    }

    public async Task<Result<BagRegistrationDocument>> CreateAsync(
        BagRegistrationDocument document,
        OutboxMessageDocument outboxMessage,
        CancellationToken cancellationToken)
    {
        outboxMessage.ClientId = document.ClientId;

        var batch = _container.CreateTransactionalBatch(new PartitionKey(document.ClientId))
            .CreateItem(document)
            .CreateItem(outboxMessage);

        using var response = await batch.ExecuteAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return Result<BagRegistrationDocument>.Success(document);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            // Logged once, as a Warning, by the caller (controller) where the business
            // outcome is known — not here, where we only know it was a conflict.
            return Error.Conflict("ERR_DUPLICATE_BAG", $"Bag number '{document.BagRegistration.BagNumber}' already exists.", "BagNumber");
        }

        // A batch reports one status for the whole transaction; the failing operation
        // carries the real status, the others report 424 FailedDependency. Log each
        // operation's own result — this is what identified the root cause the one time
        // this actually broke production in the previous codebase.
        var perOperation = new List<string>();
        for (var i = 0; i < response.Count; i++)
        {
            var opName = i == 0 ? "bagDocument" : "outboxMessage";
            perOperation.Add($"[{i}] {opName} => {(int)response[i].StatusCode} {response[i].StatusCode}");
        }

        _logger.LogCritical(
            "Transactional batch failed with status {StatusCode} for BagNumber: {BagNumber}. ErrorMessage: {ErrorMessage}. Operations: {Operations}. ActivityId: {ActivityId}",
            response.StatusCode, document.BagRegistration.BagNumber, response.ErrorMessage ?? "(none)",
            string.Join(" | ", perOperation), response.ActivityId);

        return Error.Internal("ERR_COSMOS_GENERIC", "A database error occurred while processing your request.");
    }
}
```

**This is the biggest structural difference from the old codebase, and it's deliberate:** the old `CreateBagRegistration` threw `DuplicateBagNumberException` for a conflict and let it propagate three layers up to be caught and logged in middleware. Here, a conflict is just a `Result<T>.Failure` returned normally — the controller (Card 31) handles it inline, with full context, in one place, with one log line. Only a genuine Cosmos infrastructure failure (the `else` branch) still becomes a `LogCritical` — matching the Warning/Critical split locked in the design doc, but now structural instead of retrofitted.

**Register in `Program.cs`** (append):

```csharp
using Nokas.CashMgmt.BagRegistration.Api.Repositories;

builder.Services.AddScoped<IBagRegistrationRepository, CosmosBagRegistrationRepository>();
```

---

## Card 27 — Blob payload archiving (queue + background service)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Services/IPayloadArchiveQueue.cs`

```csharp
namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public sealed record PayloadArchiveItem(string BlobName, string Content, string CorrelationId);

public interface IPayloadArchiveQueue
{
    ValueTask EnqueueAsync(PayloadArchiveItem item);
    IAsyncEnumerable<PayloadArchiveItem> ReadAllAsync(CancellationToken cancellationToken);
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Services/InMemoryPayloadArchiveQueue.cs`

```csharp
using System.Threading.Channels;

namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public sealed class InMemoryPayloadArchiveQueue : IPayloadArchiveQueue
{
    private readonly Channel<PayloadArchiveItem> _channel = Channel.CreateUnbounded<PayloadArchiveItem>();

    public ValueTask EnqueueAsync(PayloadArchiveItem item) => _channel.Writer.WriteAsync(item);

    public IAsyncEnumerable<PayloadArchiveItem> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Services/PayloadArchiverBackgroundService.cs`

```csharp
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;

namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public sealed class PayloadArchiverBackgroundService(
    IPayloadArchiveQueue queue,
    BlobServiceClient blobServiceClient,
    IOptions<BlobStorageOptions> options,
    ILogger<PayloadArchiverBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var container = blobServiceClient.GetBlobContainerClient(options.Value.ContainerName);

        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                var blob = container.GetBlobClient(item.BlobName);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(item.Content));
                await blob.UploadAsync(stream, overwrite: true, stoppingToken);
            }
            catch (Exception ex)
            {
                // One failed upload must not kill archiving permanently — documented lesson
                // from the previous codebase. Known accepted limitation (R2, locked): a crash
                // between enqueue and upload loses this item silently; a durable queue
                // (Azure Storage Queue) is the fix if this ever becomes a real problem.
                logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Failed to archive payload {BlobName}.",
                    item.CorrelationId, item.BlobName);
            }
        }
    }
}
```

**Register in `Program.cs`** (append):

```csharp
using Nokas.CashMgmt.BagRegistration.Api.Services;

builder.Services.AddSingleton<IPayloadArchiveQueue, InMemoryPayloadArchiveQueue>();
builder.Services.AddHostedService<PayloadArchiverBackgroundService>();
```

---

## Card 28 — Outbox dispatcher (publishes to Service Bus)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Services/OutboxDispatcherBackgroundService.cs`

```csharp
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.BagRegistration.Api.Documents;

namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public sealed class OutboxDispatcherBackgroundService(
    IServiceScopeFactory scopeFactory,
    ServiceBusClient serviceBusClient,
    IOptions<ServiceBusOptions> serviceBusOptions,
    IOptions<CosmosDbOptions> cosmosOptions,
    ILogger<OutboxDispatcherBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sender = serviceBusClient.CreateSender(serviceBusOptions.Value.TopicName);
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();
                var container = cosmosClient.GetContainer(cosmosOptions.Value.DatabaseName, cosmosOptions.Value.BagRegistrationContainerName);

                var query = new QueryDefinition("SELECT TOP @maxItems * FROM c WHERE c.docType = 'outbox' AND c.status = 'Pending'")
                    .WithParameter("@maxItems", BatchSize);

                using var iterator = container.GetItemQueryIterator<OutboxMessageDocument>(query);

                while (iterator.HasMoreResults)
                {
                    foreach (var message in await iterator.ReadNextAsync(stoppingToken))
                    {
                        try
                        {
                            await sender.SendMessageAsync(
                                new ServiceBusMessage(message.Payload) { Subject = message.Subject, MessageId = message.Id },
                                stoppingToken);

                            await container.PatchItemAsync<OutboxMessageDocument>(
                                message.Id,
                                new PartitionKey(message.ClientId),
                                [
                                    PatchOperation.Set("/status", "Published"),
                                    PatchOperation.Set("/ttl", (int)TimeSpan.FromDays(2).TotalSeconds)
                                ],
                                cancellationToken: stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogCritical(ex, "Failed to dispatch outbox message {OutboxId} for client {ClientId}. Left pending for retry.",
                                message.Id, message.ClientId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Outbox dispatch cycle failed.");
            }
        }
    }
}
```

**Why `MessageId = message.Id`:** this is the broker-side deduplication key — if this dispatcher somehow sends the same outbox message twice (e.g., a crash between the Service Bus send succeeding and the Cosmos patch marking it Published), Service Bus recognizes the duplicate `MessageId` and drops the second copy rather than delivering it twice to BizTalk. Unchanged from the old codebase — this part was already correct.

**Register in `Program.cs`** (append):

```csharp
builder.Services.AddHostedService<OutboxDispatcherBackgroundService>();
```

---

## Card 29 — Validate-bag-number Function client (typed HttpClient)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Services/IValidateBagNumberClient.cs`

```csharp
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public interface IValidateBagNumberClient
{
    Task<Result<bool>> ExistsAsync(string bagNumber, string correlationId, CancellationToken cancellationToken);
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Services/ValidateBagNumberClient.cs`

```csharp
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.Shared.Correlation;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Services;

public sealed class ValidateBagNumberClient(
    HttpClient httpClient,
    IOptions<BagStatusFunctionOptions> options,
    ILogger<ValidateBagNumberClient> logger) : IValidateBagNumberClient
{
    public async Task<Result<bool>> ExistsAsync(string bagNumber, string correlationId, CancellationToken cancellationToken)
    {
        var config = options.Value;
        var url = $"{config.BaseUrl}/api/validate/{bagNumber}?code={config.FunctionKey}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add(CorrelationConstants.HeaderName, correlationId);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: cancellationToken);

            var exists = json.TryGetProperty("exists", out var existsProp) && existsProp.GetBoolean();
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - Unable to reach validate Function for bag {BagNumber}",
                correlationId, bagNumber);
            return Error.DownstreamUnavailable("ERR_VALIDATE_UNAVAILABLE", "Unable to validate bag number.");
        }
    }
}
```

**Register in `Program.cs`** (append):

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

(`AddStandardResilienceHandler` needs the `Microsoft.Extensions.Http.Resilience` package: `dotnet add package Microsoft.Extensions.Http.Resilience` in the API project.)

**Why this stays fail-closed (503) on any failure** — deliberately re-affirmed, not an accident: this is C5 in the design doc. If we can't confirm a bag number is genuinely new, we must not let the partner believe it registered successfully only to have it rejected downstream later.

---

# PHASE 6 — Controller

## Card 30 — ClientId access in the controller

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Helpers/ClaimsPrincipalExtensions.cs`

```csharp
using System.Security.Claims;

namespace Nokas.CashMgmt.BagRegistration.Api.Helpers;

public static class ClaimsPrincipalExtensions
{
    public static string? GetClientId(this ClaimsPrincipal user) =>
        user.Claims.FirstOrDefault(c => c.Type is "azp" or "appid")?.Value;
}
```

Yes, this duplicates the one-line lookup already inside `ClientIdMiddleware` (Card 20). That's fine — it's one line, used in two genuinely different contexts (middleware operates on `context.User` directly for logging; the controller needs it as `ClaimsPrincipal` extension for its own logic), and extracting a "shared" helper for a single line used twice adds an indirection that isn't earning its cost. Revisit if a third consumer appears.

---

## Card 31 — The POST controller action

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Controllers/BagRegistrationController.cs`

```csharp
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.BagRegistration.Api.Configuration;
using Nokas.CashMgmt.BagRegistration.Api.Documents;
using Nokas.CashMgmt.BagRegistration.Api.Helpers;
using Nokas.CashMgmt.BagRegistration.Api.Mapping;
using Nokas.CashMgmt.BagRegistration.Api.Models;
using Nokas.CashMgmt.BagRegistration.Api.Repositories;
using Nokas.CashMgmt.BagRegistration.Api.Services;
using Nokas.CashMgmt.Shared.Correlation;
using Nokas.CashMgmt.Shared.Errors;
using Nokas.CashMgmt.Shared.Results;

namespace Nokas.CashMgmt.BagRegistration.Api.Controllers;

[Authorize(Policy = AuthConfig.RegistrationRolePolicy)]
[Route("api/cash-mgmt/bag-registration")]
[ApiController]
[Produces("application/json", "application/xml")]
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

        if (HttpContext.Items["ArchivedBlobName"] is string blobName)
        {
            document.OperationHistory[0] = document.OperationHistory[0] with { };
            logger.LogInformation("Payload queued for archiving. BlobName: {BlobName}", blobName);
        }

        var outboxMessage = new OutboxMessageDocument
        {
            ClientId = clientId,
            Subject = "bagregistration",
            Payload = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        };

        var createResult = await repository.CreateAsync(document, outboxMessage, cancellationToken);

        if (!createResult.IsSuccess)
        {
            var error = createResult.Error!;
            var statusCode = error.Kind switch
            {
                ErrorKind.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            if (error.Kind == ErrorKind.Conflict)
            {
                logger.LogWarning("CorrelationId: {CorrelationId} - Duplicate bag number error: {Message}", correlationId, error.Message);
            }

            return StatusCode(statusCode, ToErrorResponse(error, correlationId));
        }

        logger.LogInformation("BagLifecycleId: {BagLifecycleId}, CorrelationId: {CorrelationId} - Bag registration created successfully.",
            bagLifecycleId, correlationId);

        var responseBody = new BagRegisteredResponse
        {
            CustomerRefNumber = document.BagRegistration.CustomerRefNumber,
            BagNumber = document.BagRegistration.BagNumber,
            RegistrationStatus = document.RegistrationStatus,
            Description = "Bag Registered Successfully",
            BagLifecycleId = bagLifecycleId,
            RequestCorrelationId = correlationId
        };

        return StatusCode(StatusCodes.Status201Created, responseBody);
    }

    private static ErrorResponse ToErrorResponse(Error error, string correlationId) => new()
    {
        Title = error.Message,
        Status = error.Kind switch
        {
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.DownstreamUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        },
        CorrelationId = correlationId,
        Code = error.Code,
        Field = error.Field
    };
}
```

**Read this side by side with the old `CreateBagRegistration` action.** Every branch that used to be a `try/catch` around a thrown exception is now a plain `if (!result.IsSuccess)` check — the Result-pattern (Card 7) made that possible. `ToErrorResponse` is the **single mapping point** from `ErrorKind` to HTTP status — one switch expression, not scattered `if (ex is X)` checks across multiple files.

**One thing genuinely missing vs. the old code, on purpose for this batch:** no `[ApiController]` automatic 201 `Location` header via `CreatedAtRoute` yet — that needs the GET endpoint's route to exist, which is the next batch. Using plain `StatusCode(201, ...)` for now is a deliberate, temporary simplification; we'll switch to `CreatedAtRoute` once GET exists in Batch 02, restoring the `Location` header the frozen contract expects.

---

## Card 32 — Response DTO

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Api/Models/BagRegisteredResponse.cs`

```csharp
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Nokas.CashMgmt.BagRegistration.Api.Models;

[XmlRoot("BagRegResponse")]
public sealed class BagRegisteredResponse
{
    [JsonPropertyName("customerRefNumber")]
    [XmlElement("CustomerRefNumber")]
    public required string CustomerRefNumber { get; init; }

    [JsonPropertyName("bagNumber")]
    [XmlElement("BagNumber")]
    public required string BagNumber { get; init; }

    [JsonPropertyName("description")]
    [XmlElement("Description")]
    public required string Description { get; init; }

    [JsonPropertyName("registrationStatus")]
    [XmlElement("RegistrationStatus")]
    public required string RegistrationStatus { get; init; }

    [JsonPropertyName("bagLifecycleId")]
    [XmlElement("BagLifecycleId")]
    public required string BagLifecycleId { get; init; }

    [JsonPropertyName("requestCorrelationId")]
    [XmlElement("RequestCorrelationId")]
    public required string RequestCorrelationId { get; init; }
}
```

**Checkpoint: `dotnet build` the whole solution now.** The API project should compile clean. If it doesn't, tell me the exact error — don't guess at fixes, since a wrong guess here can compound across the many files this batch touched.

---

# PHASE 7 — Function: the `validate` endpoint POST depends on

## Card 33 — SQL service interface + implementation (validate only, for this batch)

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Func/Services/IBagStatusService.cs`

```csharp
namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public interface IBagStatusService
{
    Task<bool> ValidateBagNumberAsync(string bagNumber, CancellationToken cancellationToken);
}
```

**File (new):** `src/Nokas.CashMgmt.BagRegistration.Func/Services/BagStatusService.cs`

```csharp
using Dapper;
using Microsoft.Data.SqlClient;

namespace Nokas.CashMgmt.BagRegistration.Func.Services;

public sealed class BagStatusService : IBagStatusService
{
    private readonly string _cacheDbConnectionString;

    public BagStatusService()
    {
        _cacheDbConnectionString = Environment.GetEnvironmentVariable("NokasMwCacheDBConnString")
            ?? throw new InvalidOperationException("Missing 'NokasMwCacheDBConnString' environment variable.");
    }

    public async Task<bool> ValidateBagNumberAsync(string bagNumber, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_cacheDbConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "SELECT TOP 1 1 FROM dbo.Registration WHERE BagNumber = @BagNumber";
        var result = await connection.ExecuteScalarAsync<int?>(sql, new { BagNumber = bagNumber });

        return result.HasValue;
    }
}
```

**Unchanged query, deliberately** — this is a frozen SQL touchpoint (§3.4 of the design doc); we don't get to redesign a query against a table BizTalk and the Cash Portal also write to.

**Note for later batches:** this constructor reads the connection string directly from an environment variable, matching today's pattern. The Function App's managed-identity + Key Vault-reference fix (flagged as a known 🔴 pitfall in `CLAUDE.md`) is real, planned work — but it's an *App Service configuration* change, not a code change, so it's not a card in this batch. Flagging so it doesn't get forgotten.

---

## Card 34 — Program.cs for the Function

**File (replace generated content):** `src/Nokas.CashMgmt.BagRegistration.Func/Program.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nokas.CashMgmt.BagRegistration.Func.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton<IBagStatusService, BagStatusService>();

builder.Build().Run();
```

(If your scaffolded `Program.cs` from Card 5 already has slightly different boilerplate from the `func new` template, tell me what it generated and I'll adjust this card — isolated-worker templates vary slightly by Core Tools version.)

---

## Card 35 — The ValidateBagNumberFunction

**File (replace the scaffolded content):** `src/Nokas.CashMgmt.BagRegistration.Func/Functions/ValidateBagNumberFunction.cs`

(Move/rename the file into a `Functions/` folder if the template put it at the project root — purely organizational.)

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Nokas.CashMgmt.BagRegistration.Func.Services;

namespace Nokas.CashMgmt.BagRegistration.Func.Functions;

public sealed class ValidateBagNumberFunction(IBagStatusService bagStatusService, ILogger<ValidateBagNumberFunction> logger)
{
    [Function("ValidateBagNumber")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "validate/{bagNumber}")] HttpRequestData req,
        string bagNumber,
        FunctionContext context)
    {
        var correlationId = req.Headers.TryGetValues("x-correlation-id", out var values)
            ? values.FirstOrDefault() ?? Guid.NewGuid().ToString()
            : Guid.NewGuid().ToString();

        try
        {
            var exists = await bagStatusService.ValidateBagNumberAsync(bagNumber, context.CancellationToken);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { exists, correlationId });
            return response;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "CorrelationId: {CorrelationId} - SQL error validating bag {BagNumber}", correlationId, bagNumber);

            var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await errorResponse.WriteAsJsonAsync(new { message = "Database error occurred", correlationId });
            return errorResponse;
        }
    }
}
```

**Checkpoint: `dotnet build` the whole solution.** Everything — Shared, Api, Func — should compile.

---

# PHASE 8 — Tests

## Card 36 — Validation tests

**File (new):** `tests/Nokas.CashMgmt.BagRegistration.Api.Tests/Models/BagRegistrationDtoValidationTests.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using Nokas.CashMgmt.BagRegistration.Api.Models;
using Xunit;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.Models;

public class BagRegistrationDtoValidationTests
{
    private static List<ValidationResult> Validate(BagRegistrationDto dto)
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        results.AddRange(dto.Validate(context));
        return results;
    }

    [Fact]
    public void Nok_WithTotalOrderAmount_IsValid()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "123",
            RegistrationType = "NOK",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "999",
            TotalOrderAmount = "5000"
        };

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Nok_WithoutTotalOrderAmount_IsInvalid()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "123",
            RegistrationType = "NOK",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "999"
        };

        var results = Validate(dto);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BagRegistrationDto.TotalOrderAmount)));
    }

    [Fact]
    public void Valuta_IsAlwaysRejected()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "123",
            RegistrationType = "Valuta",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "999",
            BagItemsFC = [new BagItemFcDto { ItemCurrencyCode = "USD" }]
        };

        var results = Validate(dto);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(BagRegistrationDto.RegistrationType)));
    }
}
```

**Concept to learn:** this test directly encodes C1 from the design doc as an executable, permanent check — if anyone ever "helpfully" removes the Valuta rejection later without reading the design doc, this test fails immediately in CI, long before it reaches a partner.

---

## Card 37 — Mapping test (contract-shape guard)

**File (new):** `tests/Nokas.CashMgmt.BagRegistration.Api.Tests/Mapping/BagRegistrationMappingsTests.cs`

```csharp
using Nokas.CashMgmt.BagRegistration.Api.Mapping;
using Nokas.CashMgmt.BagRegistration.Api.Models;
using Xunit;

namespace Nokas.CashMgmt.BagRegistration.Api.Tests.Mapping;

public class BagRegistrationMappingsTests
{
    [Fact]
    public void ToDocument_SetsRegisteredByFromClientId()
    {
        var dto = new BagRegistrationDto
        {
            CustomerRefNumber = "5790002334764",
            RegistrationType = "NOK",
            MessageId = Guid.NewGuid().ToString(),
            BagNumber = "20250822162",
            TotalOrderAmount = "10000"
        };

        var document = dto.ToDocument(
            clientId: "1a732120-1149-4d97-98b5-d454d144b383",
            bagLifecycleId: "life-1",
            correlationId: "corr-1");

        Assert.Equal("API_1a732120-1149-4d97-98b5-d454d144b383", document.BagRegistration.RegisteredBy);
        Assert.Equal(dto.BagNumber, document.Id);
        Assert.Equal("In-Progress", document.RegistrationStatus);
        Assert.Single(document.OperationHistory);
    }
}
```

This uses the exact `RegisteredBy = API_{clientId}` example from `ExistingFullFlow.md`'s Nattsafe sample row (`vchMyReference = API_1a732120-1149-4d97-98b5-d454d144b383`) — a small, deliberate link back to the real production example you gave me, so this test reads as "this is the behavior that produced that real row," not an arbitrary fixture.

**Checkpoint — final for this batch:** `dotnet test` from the repo root. Both tests should pass.

---

# What's NOT in this batch (by design, coming later)

- GET / DELETE endpoints, the status-code map, `CreatedAtRoute`/`Location` header restoration.
- Golden-file Service Bus contract tests (needs a captured real payload sample from you first).
- Cosmos container creation itself (indexing policy, unique key, TTL) — infrastructure, not code; goes in the deploy guide / a setup script.
- App Service / Function App managed-identity RBAC assignments — deploy-time, flagged inline above where relevant.
- Counting-result surface entirely.

Work through Cards 1–37 in order, run the two checkpoints, and tell me where you land — especially if `func new`'s generated boilerplate in Card 5/34 doesn't match what I wrote (Core Tools versions drift), or if anything doesn't compile. Don't guess past an error; stop and ask by card number.
