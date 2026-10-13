<#
.SYNOPSIS
  Builds and deploys the Denmark bag-registration TEST STUB API.

.DESCRIPTION
  publish -> zip -> az webapp deploy -> read the live build stamp back, so a silent
  deployment failure cannot masquerade as success.

  The build stamp is read from the /healthz endpoint's X-Build-Stamp response header,
  which is anonymous and therefore reachable without a token. Note the App Service only
  accepts traffic from APIM / Front Door, so this verification step needs the caller's IP
  to be allow-listed (see -SkipVerify).
#>
param(
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Continue'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."
        exit 1
    }
    Write-Host "  -> OK: $what"
}

$ResourceGroup = "rg-nokasapi-cashmgmt"
$AppName       = "api-nokas-bagreg-dk"
$ProjectPath   = Join-Path $PSScriptRoot "..\src\Denmark\Nokas.CashMgmt.Dk.Api"
$PublishDir    = Join-Path $ProjectPath "publish_dk"
$ZipPath       = Join-Path $env:TEMP "nokas-dk-stub.zip"

Write-Host "`n[1/4] Publishing ..."
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
dotnet publish $ProjectPath -c Release -o $PublishDir --nologo
Assert-Success "dotnet publish"

Write-Host "`n[2/4] Zipping ..."
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force
Write-Host "  -> OK: $ZipPath"

Write-Host "`n[3/4] Deploying to $AppName ..."
az webapp deploy --resource-group $ResourceGroup --name $AppName --src-path $ZipPath --type zip
Assert-Success "az webapp deploy"

if ($SkipVerify) {
    Write-Host "`n[4/4] Verification skipped (-SkipVerify)."
    exit 0
}

Write-Host "`n[4/4] Reading the live build stamp back ..."
Start-Sleep -Seconds 20
try {
    $response = Invoke-WebRequest -Uri "https://$AppName.azurewebsites.net/healthz" -Method Get -UseBasicParsing -TimeoutSec 30
    $stamp = $response.Headers["X-Build-Stamp"]
    Write-Host "  -> Live build stamp: $stamp"
} catch {
    Write-Host "  -> Could not read the build stamp: $($_.Exception.Message)"
    Write-Host "     This is expected if your IP is not allow-listed on the App Service."
}
