Set-StrictMode -Version 2.0

$script:GraphAppId = '00000003-0000-0000-c000-000000000000'
$script:SitesReadAllRoleId = '332a536c-c7ef-4017-ab91-336970924f0d'
$script:FilesReadWriteAllRoleId = '75359482-378d-4052-8f01-80520e7db3cd'

function Get-OsaProAccessToken {
    param([Parameter(Mandatory = $true)][string] $Resource)
    $raw = & az account get-access-token --resource $Resource --query accessToken -o tsv
    if ($LASTEXITCODE -ne 0) {
        throw 'Az Azure-token kérése sikertelen.'
    }
    return ([string]$raw).Trim()
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
        throw "HTTP $code"
    }
}

function Invoke-OsaProAzJson {
    param([Parameter(Mandatory = $true)][string[]] $Arguments)
    $raw = & az @Arguments -o json
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
    param([Parameter(Mandatory = $true)][string] $TemplatePath)

    return @{
        FindByName = {
            param($Type, $Name)
            if ($Type -eq 'resourceGroup') {
                $group = Invoke-OsaProAzJson -Arguments @('group', 'show', '-n', $Name)
                if (-not $group) { return $null }
                return @{ id = [string](Get-OsaProProperty $group 'id') }
            }
            $listed = @(Invoke-OsaProAzJson -Arguments @('resource', 'list', '--name', $Name))
            $first = $listed | Select-Object -First 1
            if (-not $first) { return $null }
            return @{ id = [string](Get-OsaProProperty $first 'id') }
        }
        Deploy = {
            param($Environment, $Location)
            $deploymentName = "osp-$Environment"
            $raw = & az deployment sub create --name $deploymentName --location $Location --template-file $TemplatePath --parameters "environment=$Environment" "location=$Location" --query properties.outputs -o json
            if ($LASTEXITCODE -ne 0) {
                throw 'A Bicep-telepítés sikertelen.'
            }
            $outputs = $raw | ConvertFrom-Json
            $mapped = @{}
            foreach ($property in $outputs.PSObject.Properties) {
                if ($property.Name -match '(?i)secret|password|token|connectionString|accountKey') {
                    continue
                }
                $value = Get-OsaProProperty $property.Value 'value'
                if ($value) {
                    $mapped[$property.Name] = [string]$value
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
                        throw 'Admin consent sikertelen.'
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
        EnsureFolder = {
            param($SiteHost, $SitePath, $FolderPath)
            $token = Get-OsaProAccessToken -Resource 'https://graph.microsoft.com'
            if ($SitePath -notlike '/*') { $SitePath = "/$SitePath" }
            $site = Invoke-OsaProRest -Method Get -Uri "https://graph.microsoft.com/v1.0/sites/${SiteHost}:$SitePath" -Token $token
            $siteId = [string](Get-OsaProProperty $site 'id')
            $parent = 'root'
            $lastId = $null
            foreach ($segment in @($FolderPath -split '/' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
                $encoded = [uri]::EscapeDataString($segment)
                $item = $null
                try {
                    $item = Invoke-OsaProRest -Method Get -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/drive/$parent`:/$encoded" -Token $token
                }
                catch {
                    $item = $null
                }
                if (-not $item) {
                    $item = Invoke-OsaProRest -Method Post -Uri "https://graph.microsoft.com/v1.0/sites/$siteId/drive/$parent/children" -Token $token -Body @{
                        name = $segment
                        folder = @{}
                        '@microsoft.graph.conflictBehavior' = 'fail'
                    }
                }
                $lastId = [string](Get-OsaProProperty $item 'id')
                $parent = "items/$lastId"
            }
            return $lastId
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
            $shown = Invoke-OsaProAzJson -Arguments @('resource', 'show', '--ids', [string]$Resource.id)
            if (-not $shown) { return $null }
            return [string](Get-OsaProProperty $shown 'id')
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
                & az group delete --yes --ids ([string]$Resource.id) | Out-Null
                if ($LASTEXITCODE -ne 0) { throw 'Az erőforráscsoport törlése sikertelen.' }
                return
            }
            & az resource delete --ids ([string]$Resource.id) | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Az erőforrás törlése sikertelen.' }
        }
    }
}

Export-ModuleMember -Function 'New-OsaProAzureGateway'
