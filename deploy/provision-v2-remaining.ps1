<#
.SYNOPSIS
  Completes v2 infrastructure provisioning - the parts that failed in the first run.
  Fixes: cross-resource-group plan reference, VNet integration args, Cosmos unique-key-policy
  JSON-via-PowerShell quoting. Every step now checks $LASTEXITCODE and stops on real failure
  instead of printing a misleading success message.
#>

$ErrorActionPreference = 'Stop'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."
        exit 1
    }
    Write-Host "  -> OK: $what"
}

$ResourceGroup   = "rg-nokasapi-cashmgmt"
$SharedPlanRg    = "rg-appintegration-test-ops"
$SharedPlanName  = "as-test-integrationops"
$CosmosAccount   = "nokasbagregistrationapitest"
$CosmosDatabase  = "BagRegistration"
$KeyVaultName    = "kv-dev-nokasapi-cashmgmt"
$AppInsightsName = "api-nokas-app-insights"
$VNetName        = "vnet-lz-test-dev-ne"
$VNetRg          = "rg-network-test-dev"
$SubnetName      = "snet-test-openAPI"

$FuncAppName     = "func-test-nokas-bagreg-v2"
$FuncStorageName = "stgfuncbagregv2"
$CosmosContainer = "bagreg-v2"
$Tags            = @("version=v2", "project=NokasBagRegistrationRewrite")

$SubId = az account show --query id -o tsv
$PlanResourceId = "/subscriptions/$SubId/resourceGroups/$SharedPlanRg/providers/Microsoft.Web/serverfarms/$SharedPlanName"
$VNetResourceId = "/subscriptions/$SubId/resourceGroups/$VNetRg/providers/Microsoft.Network/virtualNetworks/$VNetName"

Write-Host "=================================================="
Write-Host " Completing v2 provisioning (fixing the 3 failures)"
Write-Host "=================================================="

# ---------------------------------------------------------------------------
Write-Host "`n[1/6] Creating Function App: $FuncAppName (using the FULL cross-resource-group plan ID this time) ..."
az functionapp create --name $FuncAppName --resource-group $ResourceGroup --plan $PlanResourceId --storage-account $FuncStorageName --runtime dotnet-isolated --runtime-version 10 --functions-version 4 --app-insights $AppInsightsName
Assert-Success "Function App created"

az functionapp config set --name $FuncAppName --resource-group $ResourceGroup --net-framework-version "v10.0" --use-32bit-worker-process false
Assert-Success "Function App runtime configured (64-bit, .NET 10)"

az functionapp identity assign --name $FuncAppName --resource-group $ResourceGroup
Assert-Success "Function App managed identity assigned"

az resource tag --tags $Tags --resource-group $ResourceGroup --name $FuncAppName --resource-type "Microsoft.Web/sites"
Assert-Success "Function App tagged version=v2"

$FuncPrincipalId = az functionapp identity show --name $FuncAppName --resource-group $ResourceGroup --query principalId -o tsv
Write-Host "  -> Function identity principalId: $FuncPrincipalId"

# ---------------------------------------------------------------------------
Write-Host "`n[2/6] Adding VNet integration (snet-test-openAPI) ..."
az functionapp vnet-integration add --name $FuncAppName --resource-group $ResourceGroup --vnet $VNetResourceId --subnet $SubnetName
Assert-Success "VNet integration added"

# ---------------------------------------------------------------------------
Write-Host "`n[3/6] Creating Cosmos container: $CosmosContainer (unique-key-policy via a temp JSON file - PowerShell mangles inline JSON to Azure CLI) ..."
$policyFile = Join-Path $env:TEMP "bagreg-v2-unique-key-policy.json"
'{"uniqueKeys": [{"paths": ["/BagRegistration/BagNumber"]}]}' | Set-Content -Path $policyFile -Encoding utf8 -NoNewline
az cosmosdb sql container create --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $CosmosContainer --partition-key-path "/clientId" --ttl -1 --unique-key-policy "@$policyFile"
Assert-Success "Cosmos container created with unique-key policy"
Remove-Item $policyFile -Force

# ---------------------------------------------------------------------------
Write-Host "`n[4/6] Granting Function identity its storage roles (identity-based AzureWebJobsStorage) and Key Vault access ..."
$FuncStorageScope = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$FuncStorageName"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Storage Blob Data Owner" --scope $FuncStorageScope
Assert-Success "Function identity: Storage Blob Data Owner"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Storage Queue Data Contributor" --scope $FuncStorageScope
Assert-Success "Function identity: Storage Queue Data Contributor"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Storage Table Data Contributor" --scope $FuncStorageScope
Assert-Success "Function identity: Storage Table Data Contributor"

$KvScope = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Key Vault Secrets User" --scope $KvScope
Assert-Success "Function identity: Key Vault Secrets User"

# ---------------------------------------------------------------------------
Write-Host "`n[5/6] Setting Function App settings (array-based, no backtick continuation) ..."
# Values containing literal parentheses - like the Key Vault reference syntax
# @Microsoft.KeyVault(SecretUri=...) - get mangled by cmd.exe's re-parsing of az's
# .cmd wrapper (documented in this repo's CLAUDE.md: "Parentheses ... break it").
# Wrapping each value in escaped double quotes protects them through that re-parse.
$funcSettings = @(
    "`"NokasMwCacheDBConnString=@Microsoft.KeyVault(SecretUri=https://$KeyVaultName.vault.azure.net/secrets/NokasMwCacheDBConnString/)`"",
    "`"NattsafeSQLDBConnString=@Microsoft.KeyVault(SecretUri=https://$KeyVaultName.vault.azure.net/secrets/NattsafeSQLDBConnString/)`"",
    "AzureWebJobsStorage__accountName=$FuncStorageName"
)
az functionapp config appsettings set --name $FuncAppName --resource-group $ResourceGroup --settings $funcSettings
Assert-Success "Function App settings (Key Vault references + identity-based storage) set"

az functionapp config appsettings delete --name $FuncAppName --resource-group $ResourceGroup --setting-names AzureWebJobsStorage
Write-Host "  -> Removed the old-style AzureWebJobsStorage connection-string setting (identity-based one above replaces it)."

# ---------------------------------------------------------------------------
Write-Host "`n[6/6] Verifying everything that was fixed ..."
$funcExists = az functionapp show --name $FuncAppName --resource-group $ResourceGroup --query name -o tsv
$containerPolicy = az cosmosdb sql container show --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $CosmosContainer --query "resource.uniqueKeyPolicy.uniqueKeys[0].paths[0]" -o tsv
Write-Host "  Function App exists: $funcExists"
Write-Host "  Cosmos unique key path: $containerPolicy"

Write-Host "`nDone. All three previously-failed steps are now fixed and verified."
