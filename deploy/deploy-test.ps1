<#
.SYNOPSIS
    Deploys the NOKAS Cash Management components to the TEST environment.

.DESCRIPTION
    Replaces the old Visual Studio publish-profile workflow (those .pubxml files
    are gitignored and no longer exist in the repo). Everything here is Azure CLI,
    so it behaves identically from VS Code, Visual Studio, or a plain terminal.

    TEST resources only. Nothing in this script touches production.

.PARAMETER Target
    api | func | portal | all

.PARAMETER SkipVerify
    Skip the post-deploy build-stamp check.

.EXAMPLE
    .\deploy-test.ps1 -Target api
    .\deploy-test.ps1 -Target all
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('api', 'func', 'portal', 'all')]
    [string]$Target,

    [switch]$SkipVerify
)

# NOTE: deliberately NOT 'Stop'. Native tools (npm, dotnet, az) routinely write
# warnings to stderr, and in Windows PowerShell 5.1 that surfaces as a terminating
# NativeCommandError even on a successful run. Every native call below is checked
# explicitly via $LASTEXITCODE instead, which is the reliable signal.
$ErrorActionPreference = 'Continue'
$env:AZURE_EXTENSION_USE_DYNAMIC_INSTALL = 'no'

# ---- TEST environment constants -------------------------------------
$SubscriptionId = 'd4008c31-6d0b-4eb0-8c95-a17c9ba9fb6c'   # corp-lz-test-dev-001
$ResourceGroup  = 'rg-nokasapi-cashmgmt'
$ApiApp         = 'api-nokas-bagreg'
$FuncApp        = 'func-test-nokas-api-bagreg'
$PortalApp      = 'nokas-integration-portal'
$ApiPublicUrl   = 'https://api-test.nokas.com'
$FuncHost       = 'https://func-test-nokas-api-bagreg-cwe9eddzbmhjhtfe.norwayeast-01.azurewebsites.net'

$RepoRoot   = Split-Path -Parent $PSScriptRoot
$ApiProj    = Join-Path $RepoRoot 'Nokas.CashMgmt.BagRegistration.API\Nokas.CashMgmt.BagRegistration.API\Nokas.CashMgmt.BagRegistration.API.csproj'
$FuncProj   = Join-Path $RepoRoot 'Nokas.CashMgmt.BagReg.Func\Nokas.CashMgmt.BagReg.Func\Nokas.CashMgmt.BagReg.Func.csproj'
$PortalDir  = Join-Path $RepoRoot 'NokasIntegrationPortal'
$StageRoot  = Join-Path $env:TEMP "nokas-deploy-$(Get-Date -Format 'yyyyMMdd-HHmmss')"

function Step($msg)  { Write-Host "`n>>> $msg" -ForegroundColor Cyan }
function Ok($msg)    { Write-Host "    $msg" -ForegroundColor Green }
function Warn($msg)  { Write-Host "    $msg" -ForegroundColor Yellow }

# ---- Pre-flight ------------------------------------------------------
Step 'Pre-flight checks'

az account get-access-token --query expiresOn -o tsv | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Azure CLI is not authenticated. Run: az login --tenant 789b1506-a9ac-4cf0-a02b-d93fc0c889be"
}

$currentSub = az account show --query id -o tsv
if ($currentSub -ne $SubscriptionId) {
    Warn "Switching subscription to $SubscriptionId (was $currentSub)"
    az account set --subscription $SubscriptionId
}
Ok "Subscription: $(az account show --query name -o tsv)"

New-Item -ItemType Directory -Path $StageRoot -Force | Out-Null
Ok "Staging directory: $StageRoot"

# ---------------------------------------------------------------------
function Deploy-Api {
    Step 'API - building'
    $out = Join-Path $StageRoot 'api'
    dotnet publish $ApiProj -c Release -o $out --nologo
    if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }

    $zip = Join-Path $StageRoot 'api.zip'
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force
    Ok "Package: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"

    Step "API - deploying to $ApiApp"
    az webapp deploy --resource-group $ResourceGroup --name $ApiApp --src-path $zip --type zip
    if ($LASTEXITCODE -ne 0) { throw 'API deploy failed.' }
    Ok 'Deployed.'

    if ($SkipVerify) { return }

    Step 'API - verifying build stamp'
    Warn 'Waiting 30s for the app to restart...'
    Start-Sleep -Seconds 30

    # /swagger is the only anonymous path Front Door routes to the App Service.
    # The App Service denies direct traffic, so this must go via api-test.nokas.com.
    # curl.exe output arrives as a string ARRAY; join it before regex matching,
    # because -match on an array filters instead of setting $Matches.
    $headers = (curl.exe -s -D - -o NUL --max-time 60 "$ApiPublicUrl/swagger/v1/swagger.json") -join "`n"
    if ($headers -match '(?im)^x-build-stamp:\s*(.+)$') {
        Ok "Live build stamp -> $($Matches[1].Trim())"
    } else {
        Warn 'X-Build-Stamp header not seen. Check the deployment manually.'
    }
}

# ---------------------------------------------------------------------
function Deploy-Func {
    Step 'FUNCTION - building'
    $out = Join-Path $StageRoot 'func'
    dotnet publish $FuncProj -c Release -o $out --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Function build failed.' }

    $zip = Join-Path $StageRoot 'func.zip'
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -Force
    Ok "Package: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"

    Step "FUNCTION - deploying to $FuncApp"
    az functionapp deployment source config-zip --resource-group $ResourceGroup --name $FuncApp --src $zip
    if ($LASTEXITCODE -ne 0) { throw 'Function deploy failed.' }
    Ok 'Deployed.'

    if ($SkipVerify) { return }

    Step 'FUNCTION - verifying build stamp'
    Warn 'Waiting 30s for the worker to restart...'
    Start-Sleep -Seconds 30

    # Pull the function key from Azure rather than hardcoding it.
    # NOTE: do NOT use --query "'api-call-key'" here. In JMESPath a single-quoted
    # token is a string LITERAL, so that returns the text "api-call-key" instead
    # of the key value, and verification then fails with a bogus 401.
    $keysJson = az functionapp function keys list --resource-group $ResourceGroup --name $FuncApp `
                  --function-name ValidateBagNumber -o json | ConvertFrom-Json
    $key = $keysJson.'api-call-key'
    if ([string]::IsNullOrWhiteSpace($key)) { $key = $keysJson.'default' }
    if ([string]::IsNullOrWhiteSpace($key)) {
        Warn 'Could not read the function key; skipping verification.'
        return
    }

    # Any bag number works - we only care that buildStamp comes back.
    # Join the array first (see the note in Deploy-Api about -match on arrays).
    $resp = (curl.exe -s --max-time 60 "$FuncHost/api/validate/000000?code=$key") -join "`n"
    if ($resp -match '"buildStamp"\s*:\s*"([^"]+)"') {
        Ok "Live build stamp -> $($Matches[1])"
    } else {
        Warn "buildStamp not found in response. Raw response below:"
        Write-Host $resp
        Warn 'A 503 here usually means the VPN path to on-prem SQL is down - the stamp should still be present.'
    }
}

# ---------------------------------------------------------------------
function Deploy-Portal {
    Step 'PORTAL - building'

    if (-not (Test-Path (Join-Path $PortalDir 'node_modules'))) {
        Warn 'node_modules missing - running npm install'
        Push-Location $PortalDir
        npm install --no-audit --no-fund
        Pop-Location
        if ($LASTEXITCODE -ne 0) { throw 'npm install failed.' }
    }

    Push-Location $PortalDir
    npm run build
    $buildExit = $LASTEXITCODE
    Pop-Location
    if ($buildExit -ne 0) { throw 'Portal build failed.' }

    $dist = Join-Path $PortalDir 'dist'
    if (-not (Test-Path (Join-Path $dist 'web.config'))) {
        throw 'dist/web.config is missing - IIS client-side routing would break. Aborting.'
    }

    $zip = Join-Path $StageRoot 'portal.zip'
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip -Force
    Ok "Package: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"

    Step "PORTAL - deploying to $PortalApp"
    # --clean wipes wwwroot first. Without it OneDeploy merges, so files deleted
    # from the build (old logos, renamed bundles) stay served forever. Safe here
    # because dist/ is a complete, self-contained site including web.config.
    az webapp deploy --resource-group $ResourceGroup --name $PortalApp --src-path $zip --type zip --clean true
    if ($LASTEXITCODE -ne 0) { throw 'Portal deploy failed.' }
    Ok 'Deployed (target cleaned).'

    if ($SkipVerify) { return }

    Step 'PORTAL - verifying build stamp'
    Start-Sleep -Seconds 20
    # NOTE: integration.nokas.com currently fails TLS (expired cert binding),
    # so verify against the default hostname instead.
    # Join the array first (see the note in Deploy-Api about -match on arrays).
    $html = (curl.exe -s --max-time 60 "https://$PortalApp-gmeff7aaa3g3bveq.norwayeast-01.azurewebsites.net/") -join "`n"
    if ($html -match 'name="build-stamp"\s+content="([^"]+)"') {
        Ok "Live build stamp -> $($Matches[1])"
    } else {
        Warn 'build-stamp meta tag not found. Check the deployment manually.'
    }
}

# ---------------------------------------------------------------------
switch ($Target) {
    'api'    { Deploy-Api }
    'func'   { Deploy-Func }
    'portal' { Deploy-Portal }
    'all'    { Deploy-Api; Deploy-Func; Deploy-Portal }
}

Step 'Done'
Ok "Staging artifacts left in $StageRoot for inspection."
