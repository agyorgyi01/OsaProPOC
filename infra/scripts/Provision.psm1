Set-StrictMode -Version 2.0

Import-Module (Join-Path $PSScriptRoot 'Inventory.psm1') -Force

$script:AzureKeys = @(
    'osapro-rg', 'osapro-func', 'osapro-storage', 'notification-storage',
    'shared-rg', 'slack-func', 'slack-storage', 'app-insights', 'workspace'
)

$script:OutputMap = @{
    osaproResourceGroupId = 'osapro-rg'
    osaproFunctionAppId = 'osapro-func'
    osaproStorageId = 'osapro-storage'
    notificationStorageId = 'notification-storage'
    sharedResourceGroupId = 'shared-rg'
    slackFunctionAppId = 'slack-func'
    slackStorageId = 'slack-storage'
    appInsightsId = 'app-insights'
    workspaceId = 'workspace'
}

function New-OsaProResult {
    param([string] $Status)
    return @{
        Status = $Status
        Log = New-Object System.Collections.Generic.List[string]
        DestroyPreview = @()
        DestroySet = @()
        Deleted = @()
    }
}

function Add-OsaProLog {
    param($Result, [string] $Message, [string[]] $SecretValues = @())
    $Result.Log.Add((Protect-OsaProText -Text $Message -SecretValues $SecretValues))
}

function Get-OsaProInputValue {
    param($InputObject, [string] $Name, [string] $Default = '')
    if ($null -eq $InputObject) {
        return $Default
    }
    if ($InputObject -is [System.Collections.IDictionary]) {
        if ($InputObject.Contains($Name) -and $null -ne $InputObject[$Name]) {
            return $InputObject[$Name]
        }
        return $Default
    }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($property -and $null -ne $property.Value) {
        return $property.Value
    }
    return $Default
}

function Add-SettingIfPresent {
    param($Settings, [string] $Name, $Value)
    if ($null -eq $Value) {
        return
    }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return
    }
    $Settings[$Name] = $text
}

function Get-OsaProAppSettings {
    param($InputObject, $Names, $ExcelAppId, [string] $ExcelSecret, $EnvironmentVariables, [bool] $IncludeExcelSecret)
    $settings = @{}
    $sharePoint = Get-OsaProInputValue -InputObject $InputObject -Name 'sharePoint'
    $nav = Get-OsaProInputValue -InputObject $InputObject -Name 'nav'
    $excel = Get-OsaProInputValue -InputObject $InputObject -Name 'excel'
    $software = Get-OsaProInputValue -InputObject $nav -Name 'software'

    Add-SettingIfPresent $settings 'Excel__SiteHost' (Get-OsaProInputValue $sharePoint 'siteHost')
    Add-SettingIfPresent $settings 'Excel__SitePath' (Get-OsaProInputValue $sharePoint 'sitePath')
    Add-SettingIfPresent $settings 'Excel__FolderPath' (Get-OsaProInputValue $sharePoint 'folderPath')
    Add-SettingIfPresent $settings 'Excel__RetryCount' (Get-OsaProInputValue $excel 'retryCount')
    Add-SettingIfPresent $settings 'Excel__RetryDelaySeconds' (Get-OsaProInputValue $excel 'retryDelaySeconds')

    Add-SettingIfPresent $settings 'Nav__BaseUrl' (Get-OsaProInputValue $nav 'baseUrl')
    Add-SettingIfPresent $settings 'Nav__RetryCount' (Get-OsaProInputValue $nav 'retryCount')
    Add-SettingIfPresent $settings 'Nav__RetryDelaySeconds' (Get-OsaProInputValue $nav 'retryDelaySeconds')
    foreach ($softwareKey in @('softwareId', 'softwareName', 'softwareOperation', 'softwareMainVersion', 'softwareDevName', 'softwareDevContact')) {
        $suffix = $softwareKey.Substring(0, 1).ToUpperInvariant() + $softwareKey.Substring(1)
        Add-SettingIfPresent $settings "Nav__Software__$suffix" (Get-OsaProInputValue $software $softwareKey)
    }

    if ($IncludeExcelSecret) {
        Add-SettingIfPresent $settings 'Secrets__ExcelTenantId' (Get-OsaProInputValue $InputObject 'tenantId')
        Add-SettingIfPresent $settings 'Secrets__ExcelClientId' $ExcelAppId
        Add-SettingIfPresent $settings 'Secrets__ExcelClientSecret' $ExcelSecret
    }

    $variables = Get-OsaProInputValue -InputObject $InputObject -Name 'secretVariables'
    foreach ($secretKey in @('NavLogin', 'NavTaxNumber', 'NavPassword', 'NavSigningKey')) {
        $variableName = [string](Get-OsaProInputValue $variables $secretKey)
        if ([string]::IsNullOrWhiteSpace($variableName)) {
            continue
        }
        $value = $null
        if ($EnvironmentVariables -and $EnvironmentVariables.Contains($variableName)) {
            $value = $EnvironmentVariables[$variableName]
        }
        Add-SettingIfPresent $settings "Secrets__$secretKey" $value
    }

    return $settings
}

function Invoke-OsaProCreate {
    param($InputObject, $Inventory, $Gateway, $Result, $EnvironmentVariables, $Save)

    $environment = [string](Get-OsaProInputValue $InputObject 'environment')
    try {
        $names = Get-OsaProResourceNames -Environment $environment
    }
    catch {
        Add-OsaProLog $Result $_.Exception.Message
        $Result.Status = 'partial'
        return
    }

    if ([string]::IsNullOrWhiteSpace([string]$Inventory.environment)) {
        $Inventory.environment = $environment
    }

    foreach ($key in $script:AzureKeys) {
        $definition = Get-OsaProCatalog | Where-Object { $_.Key -eq $key } | Select-Object -First 1
        $name = [string]$names[$definition.NameProperty]
        $live = & $Gateway.FindByName $definition.Type $name
        $liveId = ''
        if ($live) {
            $liveId = [string]$live.id
        }
        $claim = Test-OsaProNameClaim -Inventory $Inventory -Name $name -LiveId $liveId
        if ($claim -eq 'foreign') {
            Add-OsaProLog $Result "Foglalt idegen név, a lépés megáll: $name"
            $Result.Status = 'partial'
            return
        }
    }

    try {
        $deployed = & $Gateway.Deploy $environment ([string](Get-OsaProInputValue $InputObject 'location'))
        foreach ($outputName in $script:OutputMap.Keys) {
            if (-not $deployed.Contains($outputName) -or [string]::IsNullOrWhiteSpace([string]$deployed[$outputName])) {
                throw "A telepítés nem adott azonosítót: $outputName"
            }
            Add-OsaProInventoryResource -Inventory $Inventory -Key $script:OutputMap[$outputName] -Id ([string]$deployed[$outputName]) | Out-Null
        }
        if ($Save) { & $Save }
        Add-OsaProLog $Result 'Azure-erőforrások azonosítói a leltárban vannak.'
    }
    catch {
        Add-OsaProLog $Result $_.Exception.Message
        $Result.Status = 'partial'
        return
    }

    $excel = Get-OsaProResource -Inventory $Inventory -Key 'excel-app'
    $found = & $Gateway.FindApplication $names.ExcelApp
    if ($found -and -not $excel) {
        $foundId = [string]$found.appObjectId
        $claim = Test-OsaProNameClaim -Inventory $Inventory -Name $names.ExcelApp -LiveId $foundId
        if ($claim -eq 'foreign') {
            Add-OsaProLog $Result "Foglalt idegen alkalmazásnév, a lépés megáll: $($names.ExcelApp)"
            $Result.Status = 'partial'
            return
        }
    }

    try {
        if (-not $excel) {
            if (-not $found) {
                $found = & $Gateway.AddApplication $names.ExcelApp
            }
            $excel = Add-OsaProInventoryResource -Inventory $Inventory -Key 'excel-app' -Id ([string]$found.appObjectId)
            $excel.appId = [string]$found.appId
            Add-OsaProInventoryResource -Inventory $Inventory -Key 'excel-sp' -Id ([string]$found.servicePrincipalId) | Out-Null
            if ($Save) { & $Save }
        }

        $excel = Get-OsaProResource -Inventory $Inventory -Key 'excel-app'
        $servicePrincipal = Get-OsaProResource -Inventory $Inventory -Key 'excel-sp'
        if (-not $excel.ContainsKey('permissionsGranted') -or -not $excel.permissionsGranted) {
            & $Gateway.AddPermissions $excel.id
            $excel.permissionsGranted = $true
            if ($Save) { & $Save }
        }
        if (-not $excel.ContainsKey('consentGranted') -or -not $excel.consentGranted) {
            & $Gateway.Consent $servicePrincipal.id
            $excel.consentGranted = $true
            if ($Save) { & $Save }
        }

        $hasSecret = @($Inventory.secretLocations) | Where-Object { $_.key -eq 'Secrets:ExcelClientSecret' } | Select-Object -First 1
        $appId = ''
        if ($excel.ContainsKey('appId')) {
            $appId = [string]$excel.appId
        }
        elseif ($found -and $found.ContainsKey('appId')) {
            $appId = [string]$found.appId
        }
        if (-not $hasSecret) {
            $secret = [string](& $Gateway.AddPassword $excel.id)
            $settings = Get-OsaProAppSettings -InputObject $InputObject -Names $names -ExcelAppId $appId -ExcelSecret $secret -EnvironmentVariables $EnvironmentVariables -IncludeExcelSecret $true
            $functionApp = Get-OsaProResource -Inventory $Inventory -Key 'osapro-func'
            & $Gateway.SetAppSettings $functionApp.id $settings
            foreach ($pair in @(
                @{ key = 'Secrets:ExcelTenantId'; location = 'Secrets__ExcelTenantId' }
                @{ key = 'Secrets:ExcelClientId'; location = 'Secrets__ExcelClientId' }
                @{ key = 'Secrets:ExcelClientSecret'; location = 'Secrets__ExcelClientSecret' }
            )) {
                $already = @(@($Inventory.secretLocations) | Where-Object { $_.key -eq $pair.key })
                if ($already.Count -eq 0) {
                    $Inventory.secretLocations = @($Inventory.secretLocations) + $pair
                }
            }
            if ($Save) { & $Save }
            Add-OsaProLog $Result 'Az Excel-titok a Function App környezetbe került.' $secret
        }
        else {
            $settings = Get-OsaProAppSettings -InputObject $InputObject -Names $names -ExcelAppId $appId -ExcelSecret '' -EnvironmentVariables $EnvironmentVariables -IncludeExcelSecret $false
            $functionApp = Get-OsaProResource -Inventory $Inventory -Key 'osapro-func'
            & $Gateway.SetAppSettings $functionApp.id $settings
            Add-OsaProLog $Result 'A meglévő Excel-titok nem cserélődik.'
        }
    }
    catch {
        Add-OsaProLog $Result $_.Exception.Message
        $Result.Status = 'partial'
        return
    }

    $sharePoint = Get-OsaProInputValue $InputObject 'sharePoint'
    try {
        $folderId = [string](& $Gateway.EnsureFolder (Get-OsaProInputValue $sharePoint 'siteHost') (Get-OsaProInputValue $sharePoint 'sitePath') (Get-OsaProInputValue $sharePoint 'folderPath'))
        $folder = Get-OsaProResource -Inventory $Inventory -Key 'sharepoint-folder'
        if (-not $folder) {
            $created = Add-OsaProInventoryResource -Inventory $Inventory -Key 'sharepoint-folder' -Id $folderId
            $created.name = [string](Get-OsaProInputValue $sharePoint 'folderPath')
        }
        if ($Save) { & $Save }
        Add-OsaProLog $Result 'A konfigurált SharePoint-mappa megvan.'
    }
    catch {
        Add-OsaProLog $Result $_.Exception.Message
        $Result.Status = 'partial'
        return
    }

    $Inventory.status = 'complete'
    $Result.Status = 'complete'
    if ($Save) { & $Save }
}

function Invoke-OsaProVerify {
    param($Inventory, $Gateway, $Result)

    $failed = $false
    foreach ($resource in @($Inventory.resources)) {
        $liveId = & $Gateway.GetLiveId $resource
        if ([string]::IsNullOrWhiteSpace($liveId)) {
            Add-OsaProLog $Result "Hiányzik: $($resource.name)"
            $failed = $true
            continue
        }
        if ($liveId -ne $resource.id) {
            Add-OsaProLog $Result "Azonosító eltér: $($resource.name)"
            $failed = $true
        }
    }

    if ($failed -or [string]$Inventory.status -ne 'complete') {
        $Result.Status = 'partial'
        return
    }

    $Result.Status = 'complete'
}

function Invoke-OsaProDestroy {
    param($Inventory, $Gateway, $Result, [string] $Target, [bool] $Confirm)

    $preview = @(Get-OsaProDestroyPreview -Inventory $Inventory -Target $Target)
    $Result.DestroyPreview = $preview
    foreach ($resource in $preview) {
        Add-OsaProLog $Result "Előnézet: $($resource.type) $($resource.name) $($resource.id)"
    }

    $live = @{}
    foreach ($resource in $preview) {
        $live[$resource.key] = & $Gateway.GetLiveId $resource
    }
    $blocked = @(Test-OsaProDestroyIds -Planned $preview -LiveIdsByKey $live)
    if ($blocked.Count -gt 0) {
        Add-OsaProLog $Result 'Azonosító eltérés, törlés nem indul.'
        $Result.Status = 'partial'
        $Result.DestroySet = @()
        return
    }

    $set = @(Get-OsaProDestroySet -Inventory $Inventory -Target $Target -Confirm:$Confirm)
    $Result.DestroySet = $set
    if (-not $Confirm) {
        Add-OsaProLog $Result 'Megerősítés nélkül törlés nem történik.'
        $Result.Status = 'complete'
        return
    }

    $groups = @($set | Where-Object { $_.type -eq 'resourceGroup' })
    $others = @($set | Where-Object { $_.type -ne 'resourceGroup' })
    foreach ($resource in ($others + $groups)) {
        $liveId = $live[$resource.key]
        if ([string]::IsNullOrWhiteSpace($liveId)) {
            continue
        }
        & $Gateway.Delete $resource
        $Result.Deleted += $resource.key
        Add-OsaProLog $Result "Törölve: $($resource.name)"
    }

    $deletedKeys = @($Result.Deleted)
    $remaining = @()
    foreach ($resource in @($Inventory.resources)) {
        if ($deletedKeys -notcontains $resource.key) {
            $remaining += $resource
        }
    }
    $Inventory.resources = $remaining
    $Inventory.status = 'partial'
    $Result.Status = 'complete'
}

function Invoke-OsaProInfrastructure {
    param(
        [Parameter(Mandatory = $true)]
        $InputObject,
        [Parameter(Mandatory = $true)]
        $Inventory,
        [Parameter(Mandatory = $true)]
        $Gateway,
        [Parameter(Mandatory = $true)]
        [ValidateSet('Create', 'Verify', 'Destroy')]
        [string] $Action,
        [ValidateSet('OsaPro', 'Shared')]
        [string] $Target = 'OsaPro',
        [switch] $Approved,
        $EnvironmentVariables = @{},
        $Save
    )

    $result = New-OsaProResult -Status 'partial'
    if ($Action -eq 'Create') {
        Invoke-OsaProCreate -InputObject $InputObject -Inventory $Inventory -Gateway $Gateway -Result $result -EnvironmentVariables $EnvironmentVariables -Save $Save
    }
    elseif ($Action -eq 'Verify') {
        Invoke-OsaProVerify -Inventory $Inventory -Gateway $Gateway -Result $result
    }
    else {
        Invoke-OsaProDestroy -Inventory $Inventory -Gateway $Gateway -Result $result -Target $Target -Confirm:$Approved
    }

    return $result
}

Export-ModuleMember -Function 'Invoke-OsaProInfrastructure', 'Get-OsaProAppSettings'
