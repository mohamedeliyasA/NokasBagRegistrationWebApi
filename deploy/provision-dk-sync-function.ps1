<#
.SYNOPSIS
  Provisions the Denmark counting-result sync Function App.

.DESCRIPTION
  The sync used to run as a background service inside api-nokas-bagreg-dk. That prevented the
  API from ever scaling out - a second instance would have run a second sync and the two would
  have raced on the watermark, silently skipping rows. A timer-triggered Function runs on exactly
  one instance no matter how many serve partners, so the API is now free to scale.

  Creates:
    - App Service 'func-test-nokas-bagreg-dk' on the EXISTING plan as-test-nokasopenapi
    - A storage account for the Functions runtime (required; Functions cannot run without one)
    - System-assigned managed identity + Cosmos data-plane RBAC + a Key Vault access policy for
      the ServiceNow credential

  Reuses without modifying: Cosmos account and its dk-countingresults / dk-kardex-map containers,
  App Insights, Key Vault.

  Idempotent: safe to re-run.
#>

$ErrorActionPreference = 'Continue'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) { Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."; exit 1 }
    Write-Host "  -> OK: $what"
}

$ResourceGroup   = "rg-nokasapi-cashmgmt"
$PlanResourceId  = "/subscriptions/d4008c31-6d0b-4eb0-8c95-a17c9ba9fb6c/resourceGroups/rg-appintegration-test-ops/providers/Microsoft.Web/serverfarms/as-test-nokasopenapi"
$CosmosAccount   = "nokasbagregistrationapitest"
$CosmosDatabase  = "BagRegistration"
$AppInsightsName = "api-nokas-app-insights"
$KeyVaultName    = "kv-dev-nokasapi-cashmgmt"

$FuncName        = "func-test-nokas-bagreg-dk"
$StorageName     = "stnokasbagregdkfunc"
$Location        = "norwayeast"

$SubId = az account show --query id -o tsv
Assert-Success "Resolved subscription id"

Write-Host "`n[1/6] Storage account for the Functions runtime ..."
$existingStorage = az storage account show --name $StorageName --resource-group $ResourceGroup --query id -o tsv 2>$null
if ($existingStorage) {
    Write-Host "  -> SKIP: storage account already exists"
} else {
    az storage account create --name $StorageName --resource-group $ResourceGroup --location $Location --sku Standard_LRS --min-tls-version TLS1_2 --allow-blob-public-access false -o none
    Assert-Success "Storage account created"
}

Write-Host "`n[2/6] Function App ..."
$existingFunc = az functionapp show --name $FuncName --resource-group $ResourceGroup --query id -o tsv 2>$null
if ($existingFunc) {
    Write-Host "  -> SKIP: function app already exists"
} else {
    az functionapp create --name $FuncName --resource-group $ResourceGroup --plan $PlanResourceId `
        --storage-account $StorageName --runtime dotnet-isolated --runtime-version 10 --functions-version 4 `
        --app-insights $AppInsightsName -o none
    Assert-Success "Function App created"
}

az resource tag --tags "market=DK" "purpose=counting-result-sync" --resource-group $ResourceGroup --name $FuncName --resource-type "Microsoft.Web/sites" -o none
Assert-Success "Tagged"

Write-Host "`n[3/6] Managed identity ..."
az functionapp identity assign --name $FuncName --resource-group $ResourceGroup -o none
Assert-Success "Managed identity assigned"
$PrincipalId = az functionapp identity show --name $FuncName --resource-group $ResourceGroup --query principalId -o tsv
Assert-Success "Resolved principalId"

Write-Host "`n[4/6] Cosmos data-plane access ..."
$CosmosRoleId = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
$CosmosScope  = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount"
$already = az cosmosdb sql role assignment list --account-name $CosmosAccount --resource-group $ResourceGroup --query "[?principalId=='$PrincipalId'].id" -o tsv
if ($already) {
    Write-Host "  -> SKIP: role assignment already present"
} else {
    az cosmosdb sql role assignment create --account-name $CosmosAccount --resource-group $ResourceGroup --role-definition-id $CosmosRoleId --principal-id $PrincipalId --scope $CosmosScope -o none
    Assert-Success "Cosmos data-plane role assigned"
}

# This vault uses the legacy access-policy model, so an RBAC grant would be accepted and then
# silently ignored. set-policy is what actually works here.
Write-Host "`n[5/6] Key Vault access policy for the ServiceNow credential ..."
az keyvault set-policy --name $KeyVaultName --object-id $PrincipalId --secret-permissions get list -o none
Assert-Success "Key Vault access policy set"

Write-Host "`n[6/6] App settings ..."
$VaultUri = "https://$KeyVaultName.vault.azure.net/"
az functionapp config appsettings set --name $FuncName --resource-group $ResourceGroup --settings `
    "KeyVault__Uri=$VaultUri" `
    "CosmosDb__AccountEndpoint=https://$CosmosAccount.documents.azure.com:443/" `
    "CosmosDb__DatabaseName=$CosmosDatabase" `
    "CosmosDb__CountingResultContainerName=dk-countingresults" `
    "CosmosDb__KardexMapContainerName=dk-kardex-map" `
    "ServiceNow__CountingResultSync__PollIntervalMinutes=10" `
    "ServiceNow__TimeoutSeconds=30" -o none
Assert-Success "App settings written"

Write-Host "`nDone. Deploy the code with deploy/deploy-dk-sync-function.ps1."
Write-Host "  NOTE: the sync interval is also fixed in the timer expression (every 10 minutes)."
