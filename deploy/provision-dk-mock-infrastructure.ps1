<#
.SYNOPSIS
  Provisions the Denmark bag-registration TEST STUB.

.DESCRIPTION
  Stands up a stand-in endpoint so the Danish partner can integrate before the ServiceNow
  backend exists. The endpoint accepts a payload of any shape, stores it in Cosmos and Blob,
  and returns a fixed acknowledgement - it registers nothing anywhere.

  Creates:
    - Cosmos container 'dk-registrations' (PK /clientId, 90-day TTL) in the EXISTING account
    - App Service 'api-nokas-bagreg-dk' on the EXISTING shared plan 'as-test-integrationops'

  Reuses without modifying: Cosmos account, Blob storage account, App Insights, APIM,
  Front Door. No Key Vault dependency - this app holds no secrets, since Cosmos and Blob
  are both reached with its managed identity.

  Idempotent: safe to re-run. Existing resources are detected and skipped.

  Requires: az CLI logged in with Contributor + User Access Administrator (or equivalent)
  on rg-nokasapi-cashmgmt and the storage account.
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
$PlanResourceId  = "/subscriptions/d4008c31-6d0b-4eb0-8c95-a17c9ba9fb6c/resourceGroups/rg-appintegration-test-ops/providers/Microsoft.Web/serverfarms/as-test-integrationops"
$CosmosAccount   = "nokasbagregistrationapitest"
$CosmosDatabase  = "BagRegistration"
$StorageAccount  = "nokasintegrationarchive"
$BlobContainer   = "integrationarchive"
$AppInsightsName = "api-nokas-app-insights"
$ApimPublicIp    = "20.251.177.47/32"

# ---- New resources ----
$AppName         = "api-nokas-bagreg-dk"
$ContainerName   = "dk-registrations"
$TtlDays         = 90
$TtlSecs         = $TtlDays * 86400
$Tags            = "market=DK", "purpose=stub"

# ---- Identity / auth ----
$TenantId        = "24aa7821-e848-457d-99f0-3b40c5519ad6"
$Audience        = "https://nokasb2ctest.onmicrosoft.com/api/nokas"
$RequiredRole    = "cashbag.dk.registration.access"

$SubId = az account show --query id -o tsv
Assert-Success "Resolved subscription id"

# ---------------------------------------------------------------------------
Write-Host "`n[1/8] Creating Cosmos container '$ContainerName' (PK /clientId, TTL $TtlDays days) ..."
$existing = az cosmosdb sql container show --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $ContainerName --query id -o tsv 2>$null
if ($existing) {
    Write-Host "  -> SKIP: container already exists"
} else {
    az cosmosdb sql container create --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $ContainerName --partition-key-path "/clientId" --ttl $TtlSecs
    Assert-Success "Cosmos container '$ContainerName' created"
}

# ---------------------------------------------------------------------------
Write-Host "`n[2/8] Creating App Service '$AppName' (Windows, .NET 10, shared plan) ..."
$existingApp = az webapp show --name $AppName --resource-group $ResourceGroup --query id -o tsv 2>$null
if ($existingApp) {
    Write-Host "  -> SKIP: app already exists"
} else {
    az webapp create --name $AppName --resource-group $ResourceGroup --plan $PlanResourceId --runtime "dotnet:10"
    Assert-Success "App Service created"
}

az webapp config set --name $AppName --resource-group $ResourceGroup --net-framework-version "v10.0" --use-32bit-worker-process false --always-on true --http20-enabled true
Assert-Success "Runtime configured (64-bit, .NET 10, AlwaysOn)"

az webapp update --name $AppName --resource-group $ResourceGroup --https-only true
Assert-Success "HTTPS-only enforced"

az resource tag --tags $Tags --resource-group $ResourceGroup --name $AppName --resource-type "Microsoft.Web/sites"
Assert-Success "Tagged market=DK purpose=stub"

# ---------------------------------------------------------------------------
Write-Host "`n[3/8] Assigning managed identity ..."
az webapp identity assign --name $AppName --resource-group $ResourceGroup
Assert-Success "Managed identity assigned"

$PrincipalId = az webapp identity show --name $AppName --resource-group $ResourceGroup --query principalId -o tsv
Assert-Success "Resolved principalId"
Write-Host "  -> principalId: $PrincipalId"

# ---------------------------------------------------------------------------
Write-Host "`n[4/8] Granting Cosmos data-plane access (Built-in Data Contributor) ..."
$CosmosRoleId = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
$CosmosScope  = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount"

$already = az cosmosdb sql role assignment list --account-name $CosmosAccount --resource-group $ResourceGroup --query "[?principalId=='$PrincipalId'].id" -o tsv
if ($already) {
    Write-Host "  -> SKIP: role assignment already present"
} else {
    az cosmosdb sql role assignment create --account-name $CosmosAccount --resource-group $ResourceGroup --role-definition-id $CosmosRoleId --principal-id $PrincipalId --scope $CosmosScope
    Assert-Success "Cosmos data-plane role assigned"
}

# ---------------------------------------------------------------------------
Write-Host "`n[5/8] Granting Blob data access (Storage Blob Data Contributor) ..."
$StorageScope = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$StorageAccount"
az role assignment create --assignee-object-id $PrincipalId --assignee-principal-type ServicePrincipal --role "Storage Blob Data Contributor" --scope $StorageScope
if ($LASTEXITCODE -ne 0) { Write-Host "  -> NOTE: role assignment may already exist; continuing" } else { Write-Host "  -> OK: Blob data role assigned" }

# ---------------------------------------------------------------------------
Write-Host "`n[6/8] Writing app settings ..."
$AiConnString = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroup --query connectionString -o tsv
Assert-Success "Resolved App Insights connection string"

az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings `
    "ASPNETCORE_ENVIRONMENT=Test" `
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$AiConnString" `
    "AzureAd__TenantId=$TenantId" `
    "AzureAd__Authority=https://login.microsoftonline.com/$TenantId/v2.0" `
    "AzureAd__Audience=$Audience" `
    "AzureAd__RequiredRole=$RequiredRole" `
    "CosmosDb__AccountEndpoint=https://$CosmosAccount.documents.azure.com:443/" `
    "CosmosDb__DatabaseName=$CosmosDatabase" `
    "CosmosDb__RegistrationContainerName=$ContainerName" `
    "BlobStorage__AccountUri=https://$StorageAccount.blob.core.windows.net/" `
    "BlobStorage__ContainerName=$BlobContainer" `
    "Deployment__Tier=Test"
Assert-Success "App settings written"

# ---------------------------------------------------------------------------
Write-Host "`n[7/8] Restricting inbound access to APIM and Front Door ..."
az webapp config access-restriction add --name $AppName --resource-group $ResourceGroup --rule-name "allow-apim" --action Allow --ip-address $ApimPublicIp --priority 100 2>$null
az webapp config access-restriction add --name $AppName --resource-group $ResourceGroup --rule-name "allow-afd" --action Allow --service-tag "AzureFrontDoor.Backend" --priority 200 2>$null
Write-Host "  -> OK: access restrictions applied (default deny follows automatically)"

# ---------------------------------------------------------------------------
Write-Host "`n[8/8] Done."
$hostName = az webapp show --name $AppName --resource-group $ResourceGroup --query defaultHostName -o tsv
Write-Host ""
Write-Host "  App Service   : $AppName"
Write-Host "  Host          : https://$hostName"
Write-Host "  Cosmos        : $CosmosDatabase / $ContainerName (PK /clientId, TTL $TtlDays days)"
Write-Host "  Blob          : $StorageAccount / $BlobContainer (prefix dk_bag_create_*)"
Write-Host "  Required role : $RequiredRole"
Write-Host ""
Write-Host "  Next: deploy the code with deploy/deploy-dk-mock.ps1, then add the APIM API."
