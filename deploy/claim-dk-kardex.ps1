<#
.SYNOPSIS
  Manually claims one or more DK kardexes (customerRefNumber) for a client id.

.EXAMPLE
  .\claim-dk-kardex.ps1 -ClientId abe243be-3e72-46fe-8467-f16c57dc3489 -Kardex 3818,4200000198

.DESCRIPTION
  Writes claim documents into dk-kardex-map so the counting-result sync starts storing rows for
  those kardexes without a registration having to come first. Existing claims are never
  overwritten. Grants the signed-in CLI user a temporary Cosmos data-plane role, then revokes it.
#>
param(
    [Parameter(Mandatory)][string]$ClientId,
    [Parameter(Mandatory)][string[]]$Kardex
)

$ErrorActionPreference = 'Continue'
$ResourceGroup  = "rg-nokasapi-cashmgmt"
$CosmosAccount  = "nokasbagregistrationapitest"
$CosmosDatabase = "BagRegistration"
$Container      = "dk-kardex-map"

$MyObjectId = az ad signed-in-user show --query id -o tsv
$SubId      = az account show --query id -o tsv
$Scope      = "/subscriptions/$SubId/resourceGroups/$ResourceGroup/providers/Microsoft.DocumentDB/databaseAccounts/$CosmosAccount"
$RoleId     = "$Scope/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"

$already = az cosmosdb sql role assignment list --account-name $CosmosAccount --resource-group $ResourceGroup --query "[?principalId=='$MyObjectId'].id" -o tsv
$grantedHere = $false
if (-not $already) {
    az cosmosdb sql role assignment create --account-name $CosmosAccount --resource-group $ResourceGroup --role-definition-id $RoleId --principal-id $MyObjectId --scope $Scope -o none
    $grantedHere = $true
    Write-Host "Waiting 20s for the temporary role to propagate ..."
    Start-Sleep -Seconds 20
}

python "$PSScriptRoot\seed-dk-kardex.py" $CosmosAccount $CosmosDatabase $Container $ClientId @Kardex
$code = $LASTEXITCODE

if ($grantedHere) {
    $id = az cosmosdb sql role assignment list --account-name $CosmosAccount --resource-group $ResourceGroup --query "[?principalId=='$MyObjectId'].id" -o tsv
    if ($id) { az cosmosdb sql role assignment delete --account-name $CosmosAccount --resource-group $ResourceGroup --role-assignment-id $id --yes -o none }
}
exit $code
