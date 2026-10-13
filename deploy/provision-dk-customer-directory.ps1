<#
.SYNOPSIS
  Provisions the DK customer directory (clientId -> customer name) and seeds ShopBox.

.DESCRIPTION
  The endpoint we send to ServiceNow is generic and shared by every Danish customer. So that
  ServiceNow can always tell which onboarded customer a request came from - without maintaining
  their own partner directory or needing a new field/endpoint per customer - every request we
  forward carries a "nokasSource" object: { customerId, customerName }. customerId is always the
  caller's own client id (no lookup needed, cannot be spoofed - it comes from the validated
  token). customerName is looked up from this directory.

  Creates:
    - Cosmos container 'dk-customers' (PK /id, no TTL - this is master data) in the EXISTING
      account, reusing the same Cosmos data-plane role the DK app already has at account scope.

  Writes app setting CosmosDb__CustomerDirectoryContainerName on the EXISTING api-nokas-bagreg-dk
  app. Then upserts one entry: ShopBox.

  Idempotent: safe to re-run. Existing resources and entries are detected and left alone,
  except the ShopBox entry, which is always upserted so its name stays correct.

  Requires: az CLI logged in with Contributor on rg-nokasapi-cashmgmt.
#>

$ErrorActionPreference = 'Continue'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."
        exit 1
    }
    Write-Host "  -> OK: $what"
}

# ---- Existing, reused resources ----
$ResourceGroup   = "rg-nokasapi-cashmgmt"
$CosmosAccount   = "nokasbagregistrationapitest"
$CosmosDatabase  = "BagRegistration"
$AppName         = "api-nokas-bagreg-dk"

# ---- New resource ----
$ContainerName   = "dk-customers"

# ---- Seed data ----
$ShopBoxClientId = "abe243be-3e72-46fe-8467-f16c57dc3489"
$ShopBoxName     = "ShopBox"

# ---------------------------------------------------------------------------
Write-Host "`n[1/3] Creating Cosmos container '$ContainerName' (PK /id, no TTL) ..."
$existing = az cosmosdb sql container show --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $ContainerName --query id -o tsv 2>$null
if ($existing) {
    Write-Host "  -> SKIP: container already exists"
} else {
    az cosmosdb sql container create --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $ContainerName --partition-key-path "/id"
    Assert-Success "Cosmos container '$ContainerName' created"
}

# ---------------------------------------------------------------------------
# No new RBAC grant needed: provision-dk-mock-infrastructure.ps1 already granted the DK app's
# managed identity 'Cosmos DB Built-in Data Contributor' at the ACCOUNT scope, which covers
# every container in the account, this one included.
Write-Host "`n[2/3] Writing app setting ..."
az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings `
    "CosmosDb__CustomerDirectoryContainerName=$ContainerName" -o none
Assert-Success "App setting written"

# ---------------------------------------------------------------------------
# There is no `az cosmosdb sql container item` command - the az cosmosdb sql group is control
# plane only. Writing a single document needs a data-plane call, so this grants the CURRENT
# CLI user temporary Cosmos RBAC, writes via the AAD-authenticated REST API, then revokes it -
# the same pattern already used once in this project for counting-result seed data.
Write-Host "`n[3/3] Seeding ShopBox ..."

$MyObjectId = az ad signed-in-user show --query id -o tsv
Assert-Success "Resolved signed-in user's object id"

$SubId = az account show --query id -o tsv
Assert-Success "Resolved subscription id"

$CosmosRoleId = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
$CosmosScope  = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount"

$alreadyGranted = az cosmosdb sql role assignment list --account-name $CosmosAccount --resource-group $ResourceGroup --query "[?principalId=='$MyObjectId'].id" -o tsv
$grantedHere = $false
if ($alreadyGranted) {
    Write-Host "  -> SKIP: temporary role assignment already present"
} else {
    az cosmosdb sql role assignment create --account-name $CosmosAccount --resource-group $ResourceGroup --role-definition-id $CosmosRoleId --principal-id $MyObjectId --scope $CosmosScope -o none
    Assert-Success "Temporary Cosmos data-plane role granted to signed-in user"
    $grantedHere = $true
    Write-Host "  -> waiting 20s for the grant to propagate ..."
    Start-Sleep -Seconds 20
}

python "$PSScriptRoot\seed-dk-customer.py" $CosmosAccount $CosmosDatabase $ContainerName $ShopBoxClientId $ShopBoxName
$seedExitCode = $LASTEXITCODE

if ($grantedHere) {
    $assignmentId = az cosmosdb sql role assignment list --account-name $CosmosAccount --resource-group $ResourceGroup --query "[?principalId=='$MyObjectId'].id" -o tsv
    if ($assignmentId) {
        az cosmosdb sql role assignment delete --account-name $CosmosAccount --resource-group $ResourceGroup --role-assignment-id $assignmentId --yes -o none
        Write-Host "  -> OK: temporary role assignment revoked"
    }
}

if ($seedExitCode -ne 0) {
    Write-Host "  -> FAILED: seeding ShopBox (exit code $seedExitCode)"
    exit 1
}
Write-Host "  -> OK: ShopBox entry upserted ($ShopBoxClientId -> $ShopBoxName)"

Write-Host "`nDone. Restart the app (or wait for the next natural restart) so the new app setting is picked up."
Write-Host "  az webapp restart --name $AppName --resource-group $ResourceGroup"
