<#
.SYNOPSIS
  Provisions the Denmark counting-result containers and app settings.

.DESCRIPTION
  Creates in the EXISTING Cosmos account:
    - 'dk-kardex-map'     PK /id (= kardex / customerRefNumber), no TTL. First-write-wins tenancy claim.
    - 'dk-countingresults' PK /clientId, TTL 548 days. Synced ServiceNow counting results + sync-state doc.
  Writes app settings on the EXISTING api-nokas-bagreg-dk app. No new RBAC: the app already holds
  Cosmos Data Contributor at account scope.

  NOT done here (owner-only, manual): creating the Entra app role and granting it to a partner app.
  The role name written below must exist in the manifest before a token can pass the policy.

  Idempotent. Requires az CLI logged in with Contributor on rg-nokasapi-cashmgmt.
#>

$ErrorActionPreference = 'Continue'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) { Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."; exit 1 }
    Write-Host "  -> OK: $what"
}

$ResourceGroup  = "rg-nokasapi-cashmgmt"
$CosmosAccount  = "nokasbagregistrationapitest"
$CosmosDatabase = "BagRegistration"
$AppName        = "api-nokas-bagreg-dk"
$KardexMap      = "dk-kardex-map"
$CountingResult = "dk-countingresults"
$CountingRole   = "cashbag.dk.countingresult.read"
$TtlSecs        = 548 * 86400

function Ensure-Container($name, $pk, $ttl) {
    $existing = az cosmosdb sql container show --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $name --query id -o tsv 2>$null
    if ($existing) { Write-Host "  -> SKIP: '$name' already exists"; return }
    if ($ttl) {
        az cosmosdb sql container create --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $name --partition-key-path $pk --ttl $ttl -o none
    } else {
        az cosmosdb sql container create --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $name --partition-key-path $pk -o none
    }
    Assert-Success "container '$name' created"
}

Write-Host "`n[1/2] Containers ..."
Ensure-Container $KardexMap "/id" $null
Ensure-Container $CountingResult "/clientId" $TtlSecs

Write-Host "`n[2/2] App settings ..."
# No values here start with '/', so Git Bash path mangling is not a risk for this call.
az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings `
    "CosmosDb__KardexMapContainerName=$KardexMap" `
    "CosmosDb__CountingResultContainerName=$CountingResult" `
    "AzureAd__CountingResultRequiredRole=$CountingRole" -o none
Assert-Success "App settings written"

Write-Host "`nDone. Deploy the code next (deploy/deploy-dk-mock.ps1)."
