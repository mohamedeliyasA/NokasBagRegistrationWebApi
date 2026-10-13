<#
.SYNOPSIS
  Builds and deploys the Denmark counting-result sync Function.

.DESCRIPTION
  publish -> zip -> az functionapp deployment source config-zip -> list the functions back, so a
  deploy that lands but fails to register the trigger cannot pass unnoticed.

  Isolated-worker functions need a restart to re-index after a zip deploy in this environment, so
  the restart below is deliberate rather than defensive.
#>

$ErrorActionPreference = 'Continue'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) { Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."; exit 1 }
    Write-Host "  -> OK: $what"
}

$ResourceGroup = "rg-nokasapi-cashmgmt"
$FuncName      = "func-test-nokas-bagreg-dk"
$ProjectPath   = Join-Path $PSScriptRoot "..\src\Denmark\Nokas.CashMgmt.Dk.Func"
$PublishDir    = Join-Path $ProjectPath "publish_dk_func"
$ZipPath       = Join-Path $env:TEMP "nokas-dk-sync-func.zip"

Write-Host "`n[1/5] Publishing ..."
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish $ProjectPath -c Release -o $PublishDir --nologo
Assert-Success "dotnet publish"

Write-Host "`n[2/5] Zipping ..."
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force
Write-Host "  -> OK: $ZipPath"

Write-Host "`n[3/5] Deploying ..."
az functionapp deployment source config-zip --resource-group $ResourceGroup --name $FuncName --src $ZipPath -o none
Assert-Success "zip deploy"

Write-Host "`n[4/5] Restarting so the isolated worker re-indexes its triggers ..."
az functionapp restart --name $FuncName --resource-group $ResourceGroup -o none
Assert-Success "restart"
Start-Sleep -Seconds 20

Write-Host "`n[5/5] Functions registered:"
az functionapp function list --name $FuncName --resource-group $ResourceGroup --query "[].name" -o tsv
