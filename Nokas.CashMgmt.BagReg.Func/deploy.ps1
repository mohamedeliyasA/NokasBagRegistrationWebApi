<#
.SYNOPSIS
    Deploys the NOKAS Bag Registration Function App to TEST.

.DESCRIPTION
    Thin wrapper. All logic and environment constants live in the shared
    orchestrator at deploy/deploy-test.ps1 in the repo root, so the
    subscription / resource group / app names are defined in exactly one place.

.EXAMPLE
    .\deploy.ps1
    .\deploy.ps1 -SkipVerify
#>
[CmdletBinding()]
param([switch]$SkipVerify)

& (Join-Path (Split-Path -Parent $PSScriptRoot) 'deploy\deploy-test.ps1') `
    -Target func @PSBoundParameters
