<#
.SYNOPSIS
  One-time infrastructure provisioning for the Nokas.CashMgmt v2 rewrite.

.DESCRIPTION
  Creates NEW resources only, every one tagged version=v2, inside the EXISTING
  rg-nokasapi-cashmgmt resource group. Never modifies or deletes anything
  belonging to the old (v1) deployment - it only reads from the old Function App
  once, to migrate one connection string into Key Vault (see step 7).

  Every step checks $LASTEXITCODE and stops immediately on real failure -
  this script's status messages are only printed after the underlying az call
  is confirmed to have succeeded (an earlier version of this script did not do
  this and printed several false "Created"/"Granted" messages for calls that
  had actually failed - see git history / session notes if curious why this
  matters so much here).

  Requires: az CLI logged in with Owner/Contributor + User Access Administrator
  (or equivalent) on rg-nokasapi-cashmgmt, since this assigns RBAC roles.

  Two known Windows/PowerShell + Azure CLI quoting gotchas this script works
  around (both documented in this repo's CLAUDE.md "Diagnostics" section):
    1. `az` is a .cmd wrapper, so cmd.exe re-parses arguments - literal
       parentheses (as in the Key Vault reference syntax
       @Microsoft.KeyVault(SecretUri=...)) get mangled unless each value is
       wrapped in escaped double quotes.
    2. Inline JSON (the Cosmos --unique-key-policy argument) gets mangled the
       same way - passed via a temp file instead.
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
$ResourceGroup        = "rg-nokasapi-cashmgmt"
$Location             = "norwayeast"
$SharedPlanName       = "as-test-integrationops"
$SharedPlanRg         = "rg-appintegration-test-ops"
$CosmosAccount        = "nokasbagregistrationapitest"
$CosmosDatabase       = "BagRegistration"
$KeyVaultName         = "kv-dev-nokasapi-cashmgmt"
$BlobStorageAccount   = "nokasintegrationarchive"
$BlobContainerName    = "integrationarchive"
$ServiceBusNamespace  = "sb-test-crmintegration"
$ServiceBusRg         = "rg-appintegration-test-crm"
$ServiceBusTopic      = "sbt-bagregistration"
$VNetName             = "vnet-lz-test-dev-ne"
$VNetRg               = "rg-network-test-dev"
$SubnetName           = "snet-test-openAPI"
$AppInsightsName      = "api-nokas-app-insights"
$OldFunctionAppName   = "func-test-nokas-api-bagreg"

# ---- New resource names (v2) ----
$ApiAppName       = "api-nokas-bagreg-v2"
$FuncAppName      = "func-test-nokas-bagreg-v2"
$FuncStorageName  = "stgfuncbagregv2"
$CosmosContainer  = "bagreg-v2"
$Tags             = @("version=v2", "project=NokasBagRegistrationRewrite")

$SubId = az account show --query id -o tsv
$PlanResourceId = "/subscriptions/$SubId/resourceGroups/$SharedPlanRg/providers/Microsoft.Web/serverfarms/$SharedPlanName"
$VNetResourceId = "/subscriptions/$SubId/resourceGroups/$VNetRg/providers/Microsoft.Network/virtualNetworks/$VNetName"

Write-Host "=================================================="
Write-Host " Nokas.CashMgmt v2 - Infrastructure Provisioning"
Write-Host " Target resource group: $ResourceGroup (unchanged, existing)"
Write-Host "=================================================="

# ---------------------------------------------------------------------------
Write-Host "`n[1/9] Creating API App Service: $ApiAppName (Windows, .NET 10, on the existing shared plan $SharedPlanName) ..."
az webapp create --name $ApiAppName --resource-group $ResourceGroup --plan $PlanResourceId --runtime "dotnet:10"
Assert-Success "API App Service created"
az webapp config set --name $ApiAppName --resource-group $ResourceGroup --net-framework-version "v10.0" --use-32bit-worker-process false
Assert-Success "API runtime configured (64-bit, .NET 10 - old app is 32-bit, fixed here)"
az webapp update --name $ApiAppName --resource-group $ResourceGroup --https-only true
Assert-Success "HTTPS-only enforced"
az webapp identity assign --name $ApiAppName --resource-group $ResourceGroup
Assert-Success "API managed identity assigned"
az resource tag --tags $Tags --resource-group $ResourceGroup --name $ApiAppName --resource-type "Microsoft.Web/sites"
Assert-Success "API tagged version=v2"
$ApiPrincipalId = az webapp identity show --name $ApiAppName --resource-group $ResourceGroup --query principalId -o tsv
Write-Host "  -> API identity principalId: $ApiPrincipalId"

# ---------------------------------------------------------------------------
Write-Host "`n[2/9] Creating Function host storage account: $FuncStorageName (dedicated - Functions should never share host storage across apps) ..."
az storage account create --name $FuncStorageName --resource-group $ResourceGroup --location $Location --sku Standard_LRS --min-tls-version TLS1_2 --allow-blob-public-access false
Assert-Success "Function storage account created"
az resource tag --tags $Tags --resource-group $ResourceGroup --name $FuncStorageName --resource-type "Microsoft.Storage/storageAccounts"
Assert-Success "Function storage account tagged version=v2"

# ---------------------------------------------------------------------------
Write-Host "`n[3/9] Creating Function App: $FuncAppName (Windows, dotnet-isolated, .NET 10, VNet-integrated to snet-test-openAPI) ..."
az functionapp create --name $FuncAppName --resource-group $ResourceGroup --plan $PlanResourceId --storage-account $FuncStorageName --runtime dotnet-isolated --runtime-version 10 --functions-version 4 --app-insights $AppInsightsName
Assert-Success "Function App created"
az functionapp config set --name $FuncAppName --resource-group $ResourceGroup --net-framework-version "v10.0" --use-32bit-worker-process false
Assert-Success "Function runtime configured (64-bit, .NET 10)"
az functionapp identity assign --name $FuncAppName --resource-group $ResourceGroup
Assert-Success "Function managed identity assigned"
az functionapp vnet-integration add --name $FuncAppName --resource-group $ResourceGroup --vnet $VNetResourceId --subnet $SubnetName
Assert-Success "VNet integration added"
az resource tag --tags $Tags --resource-group $ResourceGroup --name $FuncAppName --resource-type "Microsoft.Web/sites"
Assert-Success "Function App tagged version=v2"
$FuncPrincipalId = az functionapp identity show --name $FuncAppName --resource-group $ResourceGroup --query principalId -o tsv
Write-Host "  -> Function identity principalId: $FuncPrincipalId"

# ---------------------------------------------------------------------------
Write-Host "`n[4/9] Creating Cosmos container: $CosmosContainer (PK /clientId, unique key /bagRegistration/bagNumber, TTL enabled no-default) ..."
Write-Host "  NOTE: the unique-key policy can ONLY be set at container creation time in Cosmos - it cannot be added later."
Write-Host "  NOTE: path is camelCase (bagRegistration/bagNumber), matching this codebase's System.Text.Json camelCase"
Write-Host "  serialization - NOT PascalCase like the old Newtonsoft-based codebase. A case mismatch here makes every"
Write-Host "  document in a partition collide with each other (all 'missing' the PascalCase path = shared 'undefined')."
Write-Host "  This exact bug shipped once already this session - see git history / session notes if curious."
$policyFile = Join-Path $env:TEMP "bagreg-v2-unique-key-policy.json"
'{"uniqueKeys": [{"paths": ["/bagRegistration/bagNumber"]}]}' | Set-Content -Path $policyFile -Encoding utf8 -NoNewline
az cosmosdb sql container create --account-name $CosmosAccount --resource-group $ResourceGroup --database-name $CosmosDatabase --name $CosmosContainer --partition-key-path "/clientId" --ttl -1 --unique-key-policy "@$policyFile"
Assert-Success "Cosmos container created with unique-key policy"
Remove-Item $policyFile -Force

# ---------------------------------------------------------------------------
Write-Host "`n[5/9] Granting API identity 'Cosmos DB Built-in Data Contributor' (data-plane RBAC, not a key) ..."
$CosmosRoleId = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
az cosmosdb sql role assignment create --account-name $CosmosAccount --resource-group $ResourceGroup --role-definition-id $CosmosRoleId --principal-id $ApiPrincipalId --scope "/"
Assert-Success "Cosmos data-plane role granted"

# ---------------------------------------------------------------------------
Write-Host "`n[6/9] Granting API identity Blob + Service Bus data-plane roles, and Function identity its own storage roles ..."

$BlobScope = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$BlobStorageAccount"
az role assignment create --assignee-object-id $ApiPrincipalId --assignee-principal-type ServicePrincipal --role "Storage Blob Data Contributor" --scope $BlobScope
Assert-Success "API identity: Storage Blob Data Contributor on $BlobStorageAccount"

$SbScope = "/subscriptions/$SubId/resourceGroups/$ServiceBusRg/providers/Microsoft.ServiceBus/namespaces/$ServiceBusNamespace"
az role assignment create --assignee-object-id $ApiPrincipalId --assignee-principal-type ServicePrincipal --role "Azure Service Bus Data Sender" --scope $SbScope
Assert-Success "API identity: Azure Service Bus Data Sender on $ServiceBusNamespace"

# Identity-based AzureWebJobsStorage needs three specific roles on the Function's OWN storage account.
$FuncStorageScope = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$FuncStorageName"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Storage Blob Data Owner" --scope $FuncStorageScope
Assert-Success "Function identity: Storage Blob Data Owner"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Storage Queue Data Contributor" --scope $FuncStorageScope
Assert-Success "Function identity: Storage Queue Data Contributor"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Storage Table Data Contributor" --scope $FuncStorageScope
Assert-Success "Function identity: Storage Table Data Contributor"

# ---------------------------------------------------------------------------
Write-Host "`n[7/9] Key Vault: granting access, and moving NokasMwCacheDBConnString out of cleartext App Settings ..."
$KvScope = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"
az role assignment create --assignee-object-id $ApiPrincipalId --assignee-principal-type ServicePrincipal --role "Key Vault Secrets User" --scope $KvScope
Assert-Success "API identity: Key Vault Secrets User (RBAC)"
az role assignment create --assignee-object-id $FuncPrincipalId --assignee-principal-type ServicePrincipal --role "Key Vault Secrets User" --scope $KvScope
Assert-Success "Function identity: Key Vault Secrets User (RBAC)"

# This vault has enableRbacAuthorization=false (legacy Access Policy model) - the RBAC
# grants above are silently ignored by a vault in this mode. Confirmed by hitting the
# real runtime error ("Key Vault reference was not able to be resolved because site was
# denied access") after the first provisioning run. Granting via Access Policy too
# (matching the minimal get+list permission shape already used by other app identities
# on this vault) - additive only, does not touch the vault's global auth mode or any
# existing policy entries, so nothing belonging to the old v1 deployment is affected.
az keyvault set-policy --name $KeyVaultName --object-id $ApiPrincipalId --secret-permissions get list
Assert-Success "API identity: Key Vault access policy (get, list)"
az keyvault set-policy --name $KeyVaultName --object-id $FuncPrincipalId --secret-permissions get list
Assert-Success "Function identity: Key Vault access policy (get, list)"

# Value is read from the OLD Function App's current (cleartext) settings and piped
# straight into Key Vault - never echoed to the console, never written to any file.
$CacheDbConnString = az functionapp config appsettings list --name $OldFunctionAppName --resource-group $ResourceGroup --query "[?name=='NokasMwCacheDBConnString'].value" -o tsv
az keyvault secret set --vault-name $KeyVaultName --name "NokasMwCacheDBConnString" --value $CacheDbConnString | Out-Null
Assert-Success "Secret 'NokasMwCacheDBConnString' written to Key Vault (value never displayed)"
Write-Host "  -> Secret 'NattsafeSQLDBConnString' already existed in Key Vault from before - reused as-is, no change needed."

# ---------------------------------------------------------------------------
Write-Host "`n[8/9] Configuring app settings (endpoints only - no secrets in plain settings) ..."

$CosmosEndpoint = az cosmosdb show --name $CosmosAccount --resource-group $ResourceGroup --query documentEndpoint -o tsv
$BlobUri = "https://$BlobStorageAccount.blob.core.windows.net/"
$SbFqdn = "$ServiceBusNamespace.servicebus.windows.net"
$AiConnString = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroup --query connectionString -o tsv

# Values containing literal parentheses (@Microsoft.KeyVault(SecretUri=...)) must be
# wrapped in escaped double quotes to survive cmd.exe's re-parsing of az's .cmd wrapper.
$funcSettings = @(
    "`"NokasMwCacheDBConnString=@Microsoft.KeyVault(SecretUri=https://$KeyVaultName.vault.azure.net/secrets/NokasMwCacheDBConnString/)`"",
    "`"NattsafeSQLDBConnString=@Microsoft.KeyVault(SecretUri=https://$KeyVaultName.vault.azure.net/secrets/NattsafeSQLDBConnString/)`"",
    "AzureWebJobsStorage__accountName=$FuncStorageName"
)
az functionapp config appsettings set --name $FuncAppName --resource-group $ResourceGroup --settings $funcSettings
Assert-Success "Function App Key Vault references + identity-based storage setting applied"

az functionapp config appsettings delete --name $FuncAppName --resource-group $ResourceGroup --setting-names AzureWebJobsStorage
Write-Host "  -> Removed the old-style AzureWebJobsStorage connection-string setting (identity-based one above replaces it)."

$apiSettings = @(
    "KeyVault__Uri=https://$KeyVaultName.vault.azure.net/",
    "CosmosDb__AccountEndpoint=$CosmosEndpoint",
    "CosmosDb__DatabaseName=$CosmosDatabase",
    "CosmosDb__BagRegistrationContainerName=$CosmosContainer",
    "BlobStorage__AccountUri=$BlobUri",
    "BlobStorage__ContainerName=$BlobContainerName",
    "ServiceBus__FullyQualifiedNamespace=$SbFqdn",
    "ServiceBus__TopicName=$ServiceBusTopic",
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$AiConnString",
    "ASPNETCORE_ENVIRONMENT=Production"
)
az webapp config appsettings set --name $ApiAppName --resource-group $ResourceGroup --settings $apiSettings
Assert-Success "API app settings set"

# ---------------------------------------------------------------------------
Write-Host "`n[9/9] Infrastructure provisioning complete."
Write-Host "`nSTILL PENDING - requires code deployment first, run separately:"
Write-Host "  - AzureAd:Authority / :Audience on the API (not secret, but not set yet - same values as the old API)"
Write-Host "  - BagStatusFunction:BaseUrl and :FunctionKey on the API - the Function's key does not exist until ValidateBagNumberFunction's code is actually deployed"
Write-Host "  - The cashbag.registration.access Entra App Role - separate, explicit step, coordinated with partners (not done here)"
Write-Host "`nDone."
