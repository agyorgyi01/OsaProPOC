Set-StrictMode -Version 2.0

Import-Module (Join-Path $PSScriptRoot 'Inventory.psm1') -Force -Global

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

function Get-OsaProDefinition {
    param([string] $Key)
    foreach ($item in (Get-OsaProCatalog)) {
        if ([string]$item.Key -eq $Key) {
            return $item
        }
    }
    return $null
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

function Test-OsaProGuid {
    param([string] $Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }
    return $Value -match '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
}

function Assert-OsaProTargetInput {
    param($InputObject)
    $tenantId = [string](Get-OsaProInputValue $InputObject 'tenantId')
    $subscriptionId = [string](Get-OsaProInputValue $InputObject 'subscriptionId')
    if (-not (Test-OsaProGuid $tenantId) -or -not (Test-OsaProGuid $subscriptionId)) {
        throw 'A bérlő és az előfizetés azonosítója kötelező, a szkript nem olvassa a bejelentkezett fiókot.'
    }
    $prefix = [string](Get-OsaProInputValue $InputObject 'prefix')
    if (-not [regex]::IsMatch($prefix, '^[a-z][a-z0-9]{0,12}$')) {
        throw 'Az előtag kisbetűvel kezdődik, csak kisbetű és szám lehet, legfeljebb 13 karakter.'
    }
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

function Get-OsaProStoredSecret {
    param($Inventory, [string] $Key)
    $entry = @($Inventory.secretLocations) | Where-Object { $_.key -eq $Key } | Select-Object -First 1
    if (-not $entry) {
        return ''
    }
    if ($entry -is [System.Collections.IDictionary] -and $entry.Contains('value') -and $null -ne $entry['value']) {
        return [string]$entry['value']
    }
    return ''
}

function Get-OsaProAppSettings {
    param($ExcelAppId, [string] $ExcelSecret, [bool] $IncludeExcelSecret)
    $settings = @{}
    if ($IncludeExcelSecret) {
        Add-SettingIfPresent $settings 'Secrets__ExcelClientId' $ExcelAppId
        Add-SettingIfPresent $settings 'Secrets__ExcelClientSecret' $ExcelSecret
    }

    return $settings
}

function Invoke-OsaProCreate {
    param($InputObject, $Inventory, $Gateway, $Result, $Save)

    $environment = [string](Get-OsaProInputValue $InputObject 'environment')
    $prefix = [string](Get-OsaProInputValue $InputObject 'prefix')
    $recordedPrefix = ''
    if ($Inventory -is [System.Collections.IDictionary] -and $Inventory.Contains('prefix') -and $null -ne $Inventory['prefix']) {
        $recordedPrefix = [string]$Inventory['prefix']
    }
    if ([string]::IsNullOrWhiteSpace($recordedPrefix)) {
        $Inventory.prefix = $prefix
    }
    elseif ($recordedPrefix -ne $prefix) {
        throw "A leltár előtagja $recordedPrefix, a bemenet $prefix."
    }
    try {
        $names = Get-OsaProResourceNames -Environment $environment -Prefix $prefix
    }
    catch {
        Add-OsaProLog $Result $_.Exception.Message
        $Result.Status = 'partial'
        return
    }

    if ([string]::IsNullOrWhiteSpace([string]$Inventory.environment)) {
        $Inventory.environment = $environment
    }

    $subscriptionId = [string](Get-OsaProInputValue $InputObject 'subscriptionId')
    foreach ($key in $script:AzureKeys) {
        $definition = Get-OsaProDefinition $key
        $name = [string]$names[$definition.NameProperty]
        $expectedGroup = $names.OsaProResourceGroup
        if ($key -in @('shared-rg', 'slack-func', 'slack-storage', 'app-insights', 'workspace')) {
            $expectedGroup = $names.SharedResourceGroup
        }
        $live = & $Gateway.FindByName $definition.Type $name
        $liveId = ''
        if ($live) {
            $liveId = [string]$live.id
        }
        $claim = Test-OsaProNameClaim -Inventory $Inventory -Name $name -LiveId $liveId -SubscriptionId $subscriptionId -ExpectedResourceGroup $expectedGroup
        if ($claim -eq 'foreign') {
            Add-OsaProLog $Result "Foglalt idegen név, a lépés megáll: $name"
            $Result.Status = 'partial'
            return
        }
        if ($claim -eq 'owned' -and -not (Get-OsaProResource -Inventory $Inventory -Key $key)) {
            Add-OsaProLog $Result "Meglévő erőforrás, nem jön létre újra: $name"
        }
    }

    try {
        foreach ($storageKey in @('osapro-storage', 'slack-storage', 'notification-storage')) {
            $storageDefinition = Get-OsaProDefinition $storageKey
            $storageName = [string]$names[$storageDefinition.NameProperty]
            $storageLive = & $Gateway.FindByName $storageDefinition.Type $storageName
            if ($storageLive) {
                continue
            }
            $available = & $Gateway.CheckStorageName $storageName
            if (-not $available) {
                throw "A $storageName storage-név foglalt. A telepítés nem indul. A meglévő erőforrás nem jön létre újra."
            }
        }
        $deployed = & $Gateway.Deploy $environment ([string](Get-OsaProInputValue $InputObject 'location')) $prefix
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
            $settings = Get-OsaProAppSettings -ExcelAppId $appId -ExcelSecret $secret -IncludeExcelSecret $true
            $functionApp = Get-OsaProResource -Inventory $Inventory -Key 'osapro-func'
            & $Gateway.SetAppSettings $functionApp.id $settings
            foreach ($pair in @(
                @{ key = 'Secrets:ExcelClientId'; location = 'Secrets__ExcelClientId'; value = $appId }
                @{ key = 'Secrets:ExcelClientSecret'; location = 'Secrets__ExcelClientSecret'; value = $secret }
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
            $storedSecret = Get-OsaProStoredSecret $Inventory 'Secrets:ExcelClientSecret'
            $storedClient = Get-OsaProStoredSecret $Inventory 'Secrets:ExcelClientId'
            if ([string]::IsNullOrWhiteSpace($storedSecret) -or [string]::IsNullOrWhiteSpace($storedClient)) {
                throw 'A leltárban lévő Excel-titok értéke hiányzik. A Function App beállítása nem írható vissza, és új titok nem készül.'
            }
            $settings = Get-OsaProAppSettings -ExcelAppId $storedClient -ExcelSecret $storedSecret -IncludeExcelSecret $true
            $functionApp = Get-OsaProResource -Inventory $Inventory -Key 'osapro-func'
            & $Gateway.SetAppSettings $functionApp.id $settings
            Add-OsaProLog $Result 'A meglévő Excel-titok visszakerült a Function App környezetébe.' $storedSecret
        }
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
        $Save
    )

    Assert-OsaProTargetInput $InputObject
    $result = New-OsaProResult -Status 'partial'
    if ($Action -eq 'Create') {
        Invoke-OsaProCreate -InputObject $InputObject -Inventory $Inventory -Gateway $Gateway -Result $result -Save $Save
    }
    elseif ($Action -eq 'Verify') {
        Invoke-OsaProVerify -Inventory $Inventory -Gateway $Gateway -Result $result
    }
    else {
        Invoke-OsaProDestroy -Inventory $Inventory -Gateway $Gateway -Result $result -Target $Target -Confirm:$Approved
    }

    return $result
}

Export-ModuleMember -Function 'Invoke-OsaProInfrastructure', 'Get-OsaProAppSettings', 'Get-OsaProInputValue', 'Test-OsaProGuid', 'Assert-OsaProTargetInput'
