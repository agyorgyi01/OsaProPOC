Set-StrictMode -Version 2.0

$script:GraphAppId = '00000003-0000-0000-c000-000000000000'
$script:SitesReadAllRoleId = '332a536c-c7ef-4017-ab91-336970924f0d'
$script:FilesReadWriteAllRoleId = '75359482-378d-4052-8f01-80520e7db3cd'
$script:OsaProTenantId = ''
$script:OsaProSubscriptionId = ''
$script:OsaProTemplatePath = ''

function Test-OsaProTargetGuid {
    param([string] $Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }
    return $Value -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
}

function Assert-OsaProBoundTarget {
    $tenantId = [string]$script:OsaProTenantId
    $subscriptionId = [string]$script:OsaProSubscriptionId
    if (-not (Test-OsaProTargetGuid $tenantId) -or -not (Test-OsaProTargetGuid $subscriptionId)) {
        throw 'A bérlő és az előfizetés azonosítója kötelező, a szkript nem olvassa a bejelentkezett fiókot.'
    }
}

function Get-OsaProTokenTenant {
    param([Parameter(Mandatory = $true)][string] $Token)
    $parts = $Token.Split('.')
    if ($parts.Length -lt 2) {
        throw 'A token nem a megadott bérlőé.'
    }
    $payload = $parts[1].Replace('-', '+').Replace('_', '/')
    switch ($payload.Length % 4) {
        2 { $payload += '==' }
        3 { $payload += '=' }
    }
    $json = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($payload))
    $claims = $json | ConvertFrom-Json
    $tid = ''
    $property = $claims.PSObject.Properties['tid']
    if ($property) {
        $tid = [string]$property.Value
    }
    if ([string]::IsNullOrWhiteSpace($tid)) {
        throw 'A token nem a megadott bérlőé.'
    }
    return $tid
}

function Assert-OsaProTokenTenant {
    param(
        [Parameter(Mandatory = $true)][string] $Token,
        [Parameter(Mandatory = $true)][string] $TenantId
    )
    $tid = Get-OsaProTokenTenant -Token $Token
    if (-not $tid.Equals($TenantId, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'A token nem a megadott bérlőé.'
    }
}

function Assert-OsaProArmSubscription {
    param(
        [Parameter(Mandatory = $true)][string] $ResourceId,
        [Parameter(Mandatory = $true)][string] $SubscriptionId
    )
    $prefix = "/subscriptions/$SubscriptionId/"
    if ($ResourceId -notlike "$prefix*") {
        throw 'Az erőforrás nincs a megadott előfizetésben.'
    }
}

function Get-OsaProAzErrorText {
    param([string] $Text)
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ''
    }
    $redacted = [regex]::Replace($Text, '(?i)(AccountKey=)[^;\s]+', '$1***')
    $redacted = [regex]::Replace($redacted, '(?i)(sig=)[^&\s]+', '$1***')
    $redacted = [regex]::Replace($redacted, '(?i)Bearer\s+\S+', 'Bearer ***')
    $redacted = [regex]::Replace($redacted, '(?i)(client_secret["\s:=]+)[^"&\s]+', '$1***')
    $redacted = [regex]::Replace($redacted, 'eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+', '***')
    $lines = @($redacted -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_ -notmatch '^(WARNING|WARNING:)' })
    return (($lines -join ' ').Trim())
}

function Add-OsaProFailureLine {
    param($Lines, $Operation, [int] $Depth)
    if ($null -eq $Operation -or $Depth -gt 4) {
        return
    }
    $properties = Get-OsaProProperty $Operation 'properties'
    $state = [string](Get-OsaProProperty $properties 'provisioningState')
    if ($state -ne 'Failed') {
        return
    }
    $target = Get-OsaProProperty $properties 'targetResource'
    $name = [string](Get-OsaProProperty $target 'resourceName')
    $type = [string](Get-OsaProProperty $target 'resourceType')
    $status = Get-OsaProProperty $properties 'statusMessage'
    $error = Get-OsaProProperty $status 'error'
    $code = [string](Get-OsaProProperty $error 'code')
    $message = [string](Get-OsaProProperty $error 'message')
    if (-not [string]::IsNullOrWhiteSpace($message)) {
        [void]$Lines.Add("$name ${code}: $message")
    }
    if ($type -ne 'Microsoft.Resources/deployments') {
        return
    }
    $id = [string](Get-OsaProProperty $target 'id')
    $group = ''
    $deployment = $name
    if ($id -match '/resourceGroups/([^/]+)/providers/Microsoft.Resources/deployments/([^/]+)$') {
        $group = $Matches[1]
        $deployment = $Matches[2]
    }
    if ([string]::IsNullOrWhiteSpace($group)) {
        return
    }
    $children = @(Invoke-OsaProAzJson -Arguments @('deployment', 'operation', 'group', 'list', '--resource-group', $group, '--name', $deployment))
    foreach ($child in $children) {
        Add-OsaProFailureLine -Lines $Lines -Operation $child -Depth ($Depth + 1)
    }
}

function Get-OsaProDeploymentFailureText {
    param(
        [Parameter(Mandatory = $true)][string] $Environment,
        [Parameter(Mandatory = $true)][string] $Prefix
    )
    $lines = New-Object System.Collections.Generic.List[string]
    $operations = @(Invoke-OsaProAzJson -Arguments @('deployment', 'operation', 'sub', 'list', '--name', "$Prefix-$Environment"))
    foreach ($operation in $operations) {
        Add-OsaProFailureLine -Lines $lines -Operation $operation -Depth 0
    }
    return (($lines -join ' ').Trim())
}

function Invoke-OsaProAzRaw {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [switch] $SkipSubscription
    )
    Assert-OsaProBoundTarget
    $subscriptionId = [string]$script:OsaProSubscriptionId
    $azArguments = $Arguments
    if (-not $SkipSubscription) {
        $azArguments = @($Arguments + @('--subscription', $subscriptionId))
    }
    $errorFile = [System.IO.Path]::GetTempFileName()
    try {
        $output = & az @azArguments 2>$errorFile
        $script:OsaProLastAzError = Get-OsaProAzErrorText (Get-Content -Raw -LiteralPath $errorFile -ErrorAction SilentlyContinue)
        return $output
    }
    finally {
        Remove-Item -LiteralPath $errorFile -Force -ErrorAction SilentlyContinue
    }
}

function Connect-OsaProAccount {
    Assert-OsaProBoundTarget
    $tenantId = [string]$script:OsaProTenantId
    $subscriptionId = [string]$script:OsaProSubscriptionId
    $previousErrorAction = $ErrorActionPreference
    $nativePreference = Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue
    $previousNative = $null
    if ($nativePreference) {
        $previousNative = $nativePreference.Value
        $PSNativeCommandUseErrorActionPreference = $false
    }
    $ErrorActionPreference = 'Continue'
    try {
        & az login --tenant $tenantId --subscription $subscriptionId
        if ($LASTEXITCODE -ne 0) {
            throw 'Az Azure-bejelentkezés sikertelen.'
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
        if ($nativePreference) {
            $PSNativeCommandUseErrorActionPreference = $previousNative
        }
    }
}

function Get-OsaProAccessToken {
    param([Parameter(Mandatory = $true)][string] $Resource)
    Assert-OsaProBoundTarget
    $tenantId = [string]$script:OsaProTenantId
    $raw = Invoke-OsaProAzRaw -SkipSubscription -Arguments @('account', 'get-access-token', '--resource', $Resource, '--tenant', $tenantId, '--query', 'accessToken', '-o', 'tsv')
    if ($LASTEXITCODE -ne 0) {
        $detail = [string]$script:OsaProLastAzError
        if ([string]::IsNullOrWhiteSpace($detail)) {
            throw 'Az Azure-token kérése sikertelen.'
        }
        throw "Az Azure-token kérése sikertelen. $detail"
    }
    $token = ([string]$raw).Trim()
    Assert-OsaProTokenTenant -Token $token -TenantId $tenantId
    return $token
}

function Invoke-OsaProRest {
    param(
        [Parameter(Mandatory = $true)][string] $Method,
        [Parameter(Mandatory = $true)][string] $Uri,
        [Parameter(Mandatory = $true)][string] $Token,
        $Body
    )

    $headers = @{ Authorization = "Bearer $Token" }
    try {
        $params = @{
            Method = $Method
            Uri = $Uri
            Headers = $headers
        }
        if ($null -ne $Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
            $params.ContentType = 'application/json'
        }
        return Invoke-RestMethod @params
    }
    catch {
        $code = 0
        if ($_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
        }
        $detail = ''
        if ($_.ErrorDetails) {
            $detail = Get-OsaProAzErrorText ([string]$_.ErrorDetails.Message)
        }
        if ([string]::IsNullOrWhiteSpace($detail)) {
            throw "HTTP $code"
        }
        throw "HTTP $code. $detail"
    }
}

function Invoke-OsaProAzJson {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)
    $raw = Invoke-OsaProAzRaw -Arguments ($Arguments + @('-o', 'json'))
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    if ([string]::IsNullOrWhiteSpace([string]$raw)) {
        return $null
    }
    return ($raw | ConvertFrom-Json)
}

function Get-OsaProProperty {
    param($Object, [string] $Name)
    if ($null -eq $Object) {
        return $null
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($property) {
        return $property.Value
    }
    return $null
}

function New-OsaProAzureGateway {
    param(
        [Parameter(Mandatory = $true)][string] $TemplatePath,
        [Parameter(Mandatory = $true)][string] $TenantId,
        [Parameter(Mandatory = $true)][string] $SubscriptionId
    )
    if (-not (Test-OsaProTargetGuid $TenantId) -or -not (Test-OsaProTargetGuid $SubscriptionId)) {
        throw 'A bérlő és az előfizetés azonosítója kötelező, a szkript nem olvassa a bejelentkezett fiókot.'
    }
    $script:OsaProTenantId = $TenantId
    $script:OsaProSubscriptionId = $SubscriptionId
    $script:OsaProTemplatePath = $TemplatePath

    return @{
        FindByName = {
            param($Type, $Name)
            if ($Type -eq 'resourceGroup') {
                $group = Invoke-OsaProAzJson -Arguments @('group', 'show', '-n', $Name)
                if (-not $group) { return $null }
                $groupId = [string](Get-OsaProProperty $group 'id')
                Assert-OsaProArmSubscription -ResourceId $groupId -SubscriptionId $script:OsaProSubscriptionId
                return @{ id = $groupId }
            }
            $listed = @(Invoke-OsaProAzJson -Arguments @('resource', 'list', '--name', $Name))
            $first = $listed | Select-Object -First 1
            if (-not $first) { return $null }
            $foundId = [string](Get-OsaProProperty $first 'id')
            Assert-OsaProArmSubscription -ResourceId $foundId -SubscriptionId $script:OsaProSubscriptionId
            return @{ id = $foundId }
        }
        CheckStorageName = {
            param($Name)
            $raw = Invoke-OsaProAzRaw -Arguments @('storage', 'account', 'check-name', '--name', $Name, '-o', 'json')
            if ($LASTEXITCODE -ne 0) {
                $detail = [string]$script:OsaProLastAzError
                if ([string]::IsNullOrWhiteSpace($detail)) {
                    throw 'A storage-név ellenőrzése sikertelen.'
                }
                throw "A storage-név ellenőrzése sikertelen. $detail"
            }
            $parsed = $raw | ConvertFrom-Json
            $available = Get-OsaProProperty $parsed 'nameAvailable'
            if ($null -eq $available) {
                throw 'A storage-név ellenőrzése sikertelen.'
            }
            return [System.Convert]::ToBoolean($available)
        }
        Deploy = {
            param($Environment, $Location, $Prefix)
            $deploymentName = "$Prefix-$Environment"
            $raw = Invoke-OsaProAzRaw -Arguments @(
                'deployment', 'sub', 'create',
                '--name', $deploymentName,
                '--location', $Location,
                '--template-file', $script:OsaProTemplatePath,
                '--parameters', "environment=$Environment", "location=$Location", "prefix=$Prefix",
                '--query', 'properties.outputs',
                '-o', 'json'
            )
            if ($LASTEXITCODE -ne 0) {
                $detail = Get-OsaProAzErrorText ((@(
                    [string]$script:OsaProLastAzError
                    (Get-OsaProDeploymentFailureText -Environment $Environment -Prefix $Prefix)
                ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ' ')
                if ([string]::IsNullOrWhiteSpace($detail)) {
                    throw 'A Bicep-telepítés sikertelen.'
                }
                throw "A Bicep-telepítés sikertelen. $detail"
            }
            $outputs = $raw | ConvertFrom-Json
            $mapped = @{}
            foreach ($property in $outputs.PSObject.Properties) {
                if ($property.Name -match '(?i)secret|password|token|connectionString|accountKey') {
                    continue
                }
                $value = Get-OsaProProperty $property.Value 'value'
                if ($value) {
                    $text = [string]$value
                    $mapped[$property.Name] = $text
                    if ($text -like '/subscriptions/*') {
                        Assert-OsaProArmSubscription -ResourceId $text -SubscriptionId $script:OsaProSubscriptionId
                    }
                }
            }
            return $mapped
        }
        FindApplication = {
            param($DisplayName)
            $safeName = $DisplayName.Replace("'", "''")
            $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
            $filter = [uri]::EscapeDataString("displayName eq '$safeName'")
            try {
                $page = Invoke-OsaProRest -Method Get -Uri "https://graph.microsoft.com/v1.0/applications?`$filter=$filter" -Token $token
            }
            catch {
                return $null
            }
            $app = @($page.value) | Select-Object -First 1
            if (-not $app) { return $null }
            $appId = [string](Get-OsaProProperty $app 'appId')
            $spFilter = [uri]::EscapeDataString("appId eq '$appId'")
            $spPage = Invoke-OsaProRest -Method Get -Uri "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=$spFilter" -Token $token
            $sp = @($spPage.value) | Select-Object -First 1
            return @{
                appObjectId = [string](Get-OsaProProperty $app 'id')
                appId = $appId
                servicePrincipalId = [string](Get-OsaProProperty $sp 'id')
            }
        }
        AddApplication = {
            param($DisplayName)
            $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
            $app = Invoke-OsaProRest -Method Post -Uri 'https://graph.microsoft.com/v1.0/applications' -Token $token -Body @{
                displayName = $DisplayName
                signInAudience = 'AzureADMyOrg'
            }
            $appId = [string](Get-OsaProProperty $app 'appId')
            $sp = Invoke-OsaProRest -Method Post -Uri 'https://graph.microsoft.com/v1.0/servicePrincipals' -Token $token -Body @{ appId = $appId }
            return @{
                appObjectId = [string](Get-OsaProProperty $app 'id')
                appId = $appId
                servicePrincipalId = [string](Get-OsaProProperty $sp 'id')
            }
        }
        AddPermissions = {
            param($AppObjectId)
            $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
            Invoke-OsaProRest -Method Patch -Uri "https://graph.microsoft.com/v1.0/applications/$AppObjectId" -Token $token -Body @{
                requiredResourceAccess = @(
                    @{
                        resourceAppId = $script:GraphAppId
                        resourceAccess = @(
                            @{ id = $script:SitesReadAllRoleId; type = 'Role' }
                            @{ id = $script:FilesReadWriteAllRoleId; type = 'Role' }
                        )
                    }
                )
            } | Out-Null
        }
        Consent = {
            param($ServicePrincipalId)
            $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
            $filter = [uri]::EscapeDataString("appId eq '$script:GraphAppId'")
            $graphSp = @( (Invoke-OsaProRest -Method Get -Uri "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=$filter" -Token $token).value ) | Select-Object -First 1
            $graphSpId = [string](Get-OsaProProperty $graphSp 'id')
            foreach ($roleId in @($script:SitesReadAllRoleId, $script:FilesReadWriteAllRoleId)) {
                try {
                    Invoke-OsaProRest -Method Post -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$graphSpId/appRoleAssignedTo" -Token $token -Body @{
                        principalId = $ServicePrincipalId
                        resourceId = $graphSpId
                        appRoleId = $roleId
                    } | Out-Null
                }
                catch {
                    if ($_.Exception.Message -notlike 'HTTP 409*') {
                        throw "Admin consent sikertelen. $($_.Exception.Message)"
                    }
                }
            }
        }
        AddPassword = {
            param($AppObjectId)
            $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
            $created = Invoke-OsaProRest -Method Post -Uri "https://graph.microsoft.com/v1.0/applications/$AppObjectId/addPassword" -Token $token -Body @{
                passwordCredential = @{ displayName = 'osp-excel' }
            }
            return [string](Get-OsaProProperty $created 'secretText')
        }
        SetAppSettings = {
            param($FunctionAppId, $Settings)
            Assert-OsaProArmSubscription -ResourceId ([string]$FunctionAppId) -SubscriptionId $script:OsaProSubscriptionId
            $token = Get-OsaProAccessToken -Resource 'https://management.azure.com/'
            $uri = "https://management.azure.com$FunctionAppId/config/appsettings?api-version=2024-04-01"
            $current = @{}
            try {
                $existing = Invoke-OsaProRest -Method Get -Uri $uri -Token $token
                foreach ($property in $existing.properties.PSObject.Properties) {
                    $current[$property.Name] = [string]$property.Value
                }
            }
            catch {
                $current = @{}
            }
            foreach ($key in @($Settings.Keys)) {
                $current[$key] = [string]$Settings[$key]
            }
            Invoke-OsaProRest -Method Put -Uri $uri -Token $token -Body @{ properties = $current } | Out-Null
        }
        GetLiveId = {
            param($Resource)
            if ($Resource.type -eq 'application') {
                $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
                try {
                    $app = Invoke-OsaProRest -Method Get -Uri "https://graph.microsoft.com/v1.0/applications/$($Resource.id)" -Token $token
                    return [string](Get-OsaProProperty $app 'id')
                }
                catch { return $null }
            }
            if ($Resource.type -eq 'servicePrincipal') {
                $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
                try {
                    $sp = Invoke-OsaProRest -Method Get -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$($Resource.id)" -Token $token
                    return [string](Get-OsaProProperty $sp 'id')
                }
                catch { return $null }
            }
            if ($Resource.type -eq 'sharePointFolder') {
                return [string]$Resource.id
            }
            Assert-OsaProArmSubscription -ResourceId ([string]$Resource.id) -SubscriptionId $script:OsaProSubscriptionId
            $shown = Invoke-OsaProAzJson -Arguments @('resource', 'show', '--ids', [string]$Resource.id)
            if (-not $shown) { return $null }
            $liveId = [string](Get-OsaProProperty $shown 'id')
            Assert-OsaProArmSubscription -ResourceId $liveId -SubscriptionId $script:OsaProSubscriptionId
            return $liveId
        }
        Delete = {
            param($Resource)
            if ($Resource.type -eq 'sharePointFolder') {
                throw 'A SharePoint-mappa nem törölhető.'
            }
            if ($Resource.type -eq 'application') {
                $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
                try {
                    Invoke-OsaProRest -Method Delete -Uri "https://graph.microsoft.com/v1.0/applications/$($Resource.id)" -Token $token | Out-Null
                }
                catch { }
                return
            }
            if ($Resource.type -eq 'servicePrincipal') {
                $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
                try {
                    Invoke-OsaProRest -Method Delete -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$($Resource.id)" -Token $token | Out-Null
                }
                catch { }
                return
            }
            if ($Resource.type -eq 'resourceGroup') {
                Assert-OsaProArmSubscription -ResourceId ([string]$Resource.id) -SubscriptionId $script:OsaProSubscriptionId
                Invoke-OsaProAzRaw -Arguments @('group', 'delete', '--yes', '--ids', [string]$Resource.id) | Out-Null
                if ($LASTEXITCODE -ne 0) {
                    $detail = [string]$script:OsaProLastAzError
                    if ([string]::IsNullOrWhiteSpace($detail)) { throw 'Az erőforráscsoport törlése sikertelen.' }
                    throw "Az erőforráscsoport törlése sikertelen. $detail"
                }
                return
            }
            Assert-OsaProArmSubscription -ResourceId ([string]$Resource.id) -SubscriptionId $script:OsaProSubscriptionId
            Invoke-OsaProAzRaw -Arguments @('resource', 'delete', '--ids', [string]$Resource.id) | Out-Null
            if ($LASTEXITCODE -ne 0) {
                $detail = [string]$script:OsaProLastAzError
                if ([string]::IsNullOrWhiteSpace($detail)) { throw 'Az erőforrás törlése sikertelen.' }
                throw "Az erőforrás törlése sikertelen. $detail"
            }
        }
    }
}

Export-ModuleMember -Function 'New-OsaProAzureGateway', 'Connect-OsaProAccount', 'Assert-OsaProTokenTenant', 'Assert-OsaProArmSubscription', 'Get-OsaProTokenTenant'
