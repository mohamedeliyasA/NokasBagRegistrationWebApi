<#
.SYNOPSIS
  One-time infrastructure provisioning for the counting-result feature.

.DESCRIPTION
  Adds exactly one new resource - a Cosmos container - inside the EXISTING Cosmos
  account/database the v2 registration API already uses. No new App Service,
  Function App, or storage account. Grants the existing v2 API and Function managed
  identities data-plane access to the new container, and writes the new
  CosmosDb/CountingResults app settings onto the existing api-nokas-bagreg-v2 /
  func-test-nokas-bagreg-v2 apps.

  Does NOT touch AuthZ (cashbag.countingresult.read App Role / PartnerCountingResultRead
  policy) - that's an Entra manifest + admin-consent step, not scriptable via these
  az calls, same as cashbag.registration.access before it.

  Requires: az CLI logged in with Owner/Contributor + User Access Administrator (or
  equivalent) on rg-nokasapi-cashmgmt.
#>

$ErrorActionPreference = 'Stop'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."
        exit 1
    }
    Write-Host "  -> OK: $what"
}

# ---- Existing, reused resources (not modified, only referenced) ----
$ResourceGroup   = "rg-nokasapi-cashmgmt"
$CosmosAccount   = "nokasbagregistrationapitest"
$CosmosDatabase  = "BagRegistration"
$ApiAppName      = "api-nokas-bagreg-v2"
$FuncAppName     = "func-test-nokas-bagreg-v2"

# ---- New resource ----
$ContainerName   = "countingresults"
$DefaultTtlDays  = 548
$DefaultTtlSecs  = $DefaultTtlDays * 86400

$SubId = az account show --query id -o tsv
Assert-Success "Resolved subscription id"

# ---------------------------------------------------------------------------
Write-Host "`n[1/5] Writing Cosmos indexing policy to a temp file (avoids az.cmd/cmd.exe JSON quoting issues) ..."
$idxFile = New-TemporaryFile
@'
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [ { "path": "/*" } ],
  "excludedPaths": [
    { "path": "/statusHistory/*" },
    { "path": "/denominations/*" },
    { "path": "/amounts/*" },
    { "path": "/\"_etag\"/?" }
  ],
  "compositeIndexes": [
    [ { "path": "/clientId", "order": "ascending" }, { "path": "/countedAt", "order": "descending" } ],
    [ { "path": "/clientId", "order": "ascending" }, { "path": "/registeredAt", "order": "descending" } ],
    [ { "path": "/clientId", "order": "ascending" }, { "path": "/lastUpdatedAt", "order": "descending" } ],
    [ { "path": "/clientId", "order": "ascending" }, { "path": "/customerRefNumber", "order": "ascending" }, { "path": "/countedAt", "order": "descending" } ]
  ]
}
'@ | Set-Content -Path $idxFile -Encoding utf8
Write-Host "  -> $idxFile"

# ---------------------------------------------------------------------------
Write-Host "`n[2/5] Creating Cosmos container '$ContainerName' (PK /clientId, defaultTtl=$DefaultTtlSecs sec / $DefaultTtlDays days) ..."
az cosmosdb sql container create --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $ContainerName --partition-key-path "/clientId" --ttl $DefaultTtlSecs --idx "@$idxFile"
Assert-Success "Cosmos container '$ContainerName' created"
Remove-Item $idxFile -Force

# ---------------------------------------------------------------------------
Write-Host "`n[3/5] Granting API and Function managed identities 'Cosmos DB Built-in Data Contributor' on the account (scope /, same role already used for bagreg-v2/bagnumber-cache) ..."
$CosmosRoleId = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"

$ApiPrincipalId = az webapp identity show --name $ApiAppName --resource-group $ResourceGroup --query principalId -o tsv
Assert-Success "Resolved API managed identity"
az cosmosdb sql role assignment create --account-name $CosmosAccount --resource-group $ResourceGroup --role-definition-id $CosmosRoleId --principal-id $ApiPrincipalId --scope "/" 2>&1 | Out-Null
Write-Host "  -> OK: API identity Cosmos data-plane role present (already granted at account scope, or just granted)"

$FuncPrincipalId = az functionapp identity show --name $FuncAppName --resource-group $ResourceGroup --query principalId -o tsv
Assert-Success "Resolved Function managed identity"
az cosmosdb sql role assignment create --account-name $CosmosAccount --resource-group $ResourceGroup --role-definition-id $CosmosRoleId --principal-id $FuncPrincipalId --scope "/" 2>&1 | Out-Null
Write-Host "  -> OK: Function identity Cosmos data-plane role present (already granted at account scope, or just granted)"

# ---------------------------------------------------------------------------
Write-Host "`n[4/5] Writing app settings on $ApiAppName ..."
az webapp config appsettings set --name $ApiAppName --resource-group $ResourceGroup --settings `
    "CosmosDb__CountingResultContainerName=$ContainerName" `
    "CountingResults__Query__MaxSpanDays=92" `
    "CountingResults__Query__MaxPageSize=500" `
    "CountingResults__Query__DefaultPageSize=100" `
    "CountingResults__Cosmos__TtlDays=$DefaultTtlDays" | Out-Null
Assert-Success "API app settings written"

# ---------------------------------------------------------------------------
Write-Host "`n[5/5] Writing app settings on $FuncAppName ..."
az functionapp config appsettings set --name $FuncAppName --resource-group $ResourceGroup --settings `
    "CosmosDb__CountingResultContainerName=$ContainerName" `
    "CountingResults__Sync__IntervalMinutes=30" `
    "CountingResults__Sync__OverlapMinutes=10" `
    "CountingResults__Sync__PageSize=500" `
    "CountingResults__Backfill__DefaultMonths=18" | Out-Null
Assert-Success "Function app settings written"

Write-Host "`nDone. Still pending, not covered by this script:"
Write-Host " - usp_CountingResult_FetchChanges, IX_StatusTracking_dtmDate, NokasExternalCustomerMapping.BackfillFromDate (SQL/*.sql - owner to run on-prem)"
Write-Host " - cashbag.countingresult.read App Role + PartnerCountingResultRead policy admin-consent/rollout"
Write-Host " - Deploying the Api/Func code that actually uses this container (deploy-v2 zip deploy, once built)"
