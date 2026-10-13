<#
.SYNOPSIS
  Publishes the Denmark bag-registration TEST STUB through APIM.

.DESCRIPTION
  Creates a dedicated APIM API rather than adding operations to the Norwegian one, so the
  Danish surface has its own policy and rate limit and can be deleted later without touching
  Norway.

  Path model: the API path is 'api/cash-mgmt/dk' and the operation templates are relative to
  it, so the backend URL must already include that prefix - APIM strips the API path before
  forwarding. The Norwegian API instead sits at the root path with full-path operations;
  both end up serving the same public URL shape.

  Side effect worth knowing: the existing Front Door route 'apim-bagreg' already matches
  /api/cash-mgmt/* on api-test.nokas.com and forwards to this APIM without rewriting the
  path, so this API becomes reachable on the production TEST domain with no Front Door
  change at all.

  Authorization is deliberately NOT enforced here. APIM validates the token (authentication)
  and the API validates the app role (authorization), which keeps the role name defined in
  exactly one place and lets the API answer with its own ErrorResponse shape.

  Idempotent: safe to re-run.
#>

$ErrorActionPreference = 'Continue'

function Assert-Success($what) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  -> FAILED: $what (exit code $LASTEXITCODE). Stopping."
        exit 1
    }
    Write-Host "  -> OK: $what"
}

$ApimName     = "apim-avarn-dev-test"
$ApimRg       = "rg-common-test-dev"
$ApiId        = "dk-bag-registration-api"
$ApiPath      = "api/cash-mgmt/dk"
$DisplayName  = "NOKAS Bag Registration API - Denmark (TEST STUB)"
$BackendBase  = "https://api-nokas-bagreg-dk.azurewebsites.net/api/cash-mgmt/dk"
$TenantId     = "24aa7821-e848-457d-99f0-3b40c5519ad6"
$Audience     = "https://nokasb2ctest.onmicrosoft.com/api/nokas"

$SubId = az account show --query id -o tsv
Assert-Success "Resolved subscription id"

# ---------------------------------------------------------------------------
Write-Host "`n[1/4] Creating APIM API '$ApiId' (path '$ApiPath') ..."
$existing = az apim api show --service-name $ApimName --resource-group $ApimRg --api-id $ApiId --query id -o tsv 2>$null
if ($existing) {
    Write-Host "  -> SKIP: API already exists"
} else {
    az apim api create --service-name $ApimName --resource-group $ApimRg --api-id $ApiId --display-name $DisplayName --path $ApiPath --protocols https --service-url $BackendBase --subscription-required false
    Assert-Success "APIM API created"
}

# ---------------------------------------------------------------------------
Write-Host "`n[2/4] Creating operations ..."

function New-Operation($opId, $method, $template, $display, $templateParam) {
    $exists = az apim api operation show --service-name $ApimName --resource-group $ApimRg --api-id $ApiId --operation-id $opId --query id -o tsv 2>$null
    if ($exists) {
        Write-Host "  -> SKIP: operation '$opId' already exists"
        return
    }
    # Every {placeholder} in the URL template must also be declared as a template parameter,
    # or APIM rejects the operation outright.
    if ($templateParam) {
        az apim api operation create --service-name $ApimName --resource-group $ApimRg --api-id $ApiId --operation-id $opId --display-name $display --method $method --url-template $template --template-parameters "name=$templateParam" "type=string" "required=true"
    } else {
        az apim api operation create --service-name $ApimName --resource-group $ApimRg --api-id $ApiId --operation-id $opId --display-name $display --method $method --url-template $template
    }
    Assert-Success "operation '$opId' created"
}

New-Operation "post-bag-registration" "POST" "/bag-registration" "Register a bag" $null
New-Operation "get-bag-registration"  "GET"  "/bag-registration/{bagNumber}" "Retrieve a bag registration" "bagNumber"
New-Operation "get-bag-counting-results" "GET" "/bag-counting-results" "List counting results" $null
New-Operation "get-bag-counting-result"  "GET" "/bag-counting-results/{bagNumber}" "Retrieve the counting result for a bag" "bagNumber"

# ---------------------------------------------------------------------------
Write-Host "`n[3/4] Applying API-level policy (validate-jwt + rate limit + backend) ..."

$policyXml = @"
<policies>
    <inbound>
        <base />
        <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Unauthorized" output-token-variable-name="jwtToken">
            <openid-config url="https://login.microsoftonline.com/$TenantId/v2.0/.well-known/openid-configuration" />
            <audiences>
                <audience>$Audience</audience>
            </audiences>
            <issuers>
                <issuer>https://sts.windows.net/$TenantId/</issuer>
            </issuers>
        </validate-jwt>
        <set-variable name="rateLimitKey" value="@{
  var jwt = context.Variables.GetValueOrDefault(&quot;jwtToken&quot;) as Jwt;
  var appId = jwt?.Claims.GetValueOrDefault(&quot;appid&quot;,&quot;unknown-app&quot;);
  var iss   = jwt?.Claims.GetValueOrDefault(&quot;iss&quot;,&quot;unknown-iss&quot;);
  return iss + &quot;|&quot; + appId;
}" />
        <rate-limit-by-key calls="100" renewal-period="60" counter-key="@((string)context.Variables[&quot;rateLimitKey&quot;])" />
        <set-backend-service base-url="$BackendBase" />
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
"@

# Invoke-RestMethod rather than 'az rest': APIM returns the policy document with a UTF-8 BOM,
# which az cannot print - it raises a UnicodeEncodeError and exits non-zero even though the
# call itself succeeded. Going direct also sidesteps cmd.exe re-parsing the parentheses and
# ampersands this policy contains.
$bodyJson = @{ properties = @{ format = "rawxml"; value = $policyXml } } | ConvertTo-Json -Depth 5
$mgmtToken = az account get-access-token --resource https://management.azure.com --query accessToken -o tsv
Assert-Success "Acquired ARM access token"

$policyUrl = "https://management.azure.com/subscriptions/$SubId/resourceGroups/$ApimRg/providers/Microsoft.ApiManagement/service/$ApimName/apis/$ApiId/policies/policy?api-version=2022-08-01"
try {
    Invoke-RestMethod -Uri $policyUrl -Method Put -Headers @{ Authorization = "Bearer $mgmtToken" } -ContentType "application/json" -Body $bodyJson | Out-Null
    Write-Host "  -> OK: API policy applied"
} catch {
    Write-Host "  -> FAILED: API policy not applied: $($_.Exception.Message)"
    exit 1
}

# ---------------------------------------------------------------------------
Write-Host "`n[4/4] Done."
Write-Host ""
Write-Host "  APIM gateway : https://$ApimName.azure-api.net/$ApiPath/bag-registration"
Write-Host "  Via TEST domain (existing /api/cash-mgmt/* Front Door route):"
Write-Host "                 https://api-test.nokas.com/$ApiPath/bag-registration"
