Set-StrictMode -Version 2.0

$script:Catalog = @(
    @{ Key = 'osapro-rg'; Ownership = 'environment'; Type = 'resourceGroup'; NameProperty = 'OsaProResourceGroup'; Target = 'OsaPro' }
    @{ Key = 'osapro-func'; Ownership = 'environment'; Type = 'functionApp'; NameProperty = 'OsaProFunctionApp'; Target = 'OsaPro' }
    @{ Key = 'osapro-storage'; Ownership = 'environment'; Type = 'storageAccount'; NameProperty = 'OsaProStorage'; Target = 'OsaPro' }
    @{ Key = 'notification-storage'; Ownership = 'business'; Type = 'storageAccount'; NameProperty = 'NotificationStorage'; Target = 'OsaPro' }
    @{ Key = 'excel-app'; Ownership = 'environment'; Type = 'application'; NameProperty = 'ExcelApp'; Target = 'OsaPro' }
    @{ Key = 'excel-sp'; Ownership = 'environment'; Type = 'servicePrincipal'; NameProperty = 'ExcelApp'; Target = 'OsaPro' }
    @{ Key = 'shared-rg'; Ownership = 'shared'; Type = 'resourceGroup'; NameProperty = 'SharedResourceGroup'; Target = 'Shared' }
    @{ Key = 'slack-func'; Ownership = 'shared'; Type = 'functionApp'; NameProperty = 'SlackFunctionApp'; Target = 'Shared' }
    @{ Key = 'slack-storage'; Ownership = 'shared'; Type = 'storageAccount'; NameProperty = 'SlackStorage'; Target = 'Shared' }
    @{ Key = 'app-insights'; Ownership = 'shared'; Type = 'applicationInsights'; NameProperty = 'AppInsights'; Target = 'Shared' }
    @{ Key = 'workspace'; Ownership = 'shared'; Type = 'workspace'; NameProperty = 'LogAnalytics'; Target = 'Shared' }
    @{ Key = 'sharepoint-folder'; Ownership = 'existing'; Type = 'sharePointFolder'; NameProperty = $null; Target = 'None' }
)

function Get-OsaProCatalog {
    return ,$script:Catalog
}

function Get-OsaProResourceNames {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Environment,
        [Parameter(Mandatory = $true)]
        [string] $Prefix
    )

    if ($Environment -ne 'dev' -and $Environment -ne 'prod') {
        throw "A környezet csak dev vagy prod lehet: $Environment"
    }
    if (-not [regex]::IsMatch($Prefix, '^[a-z][a-z0-9]{0,12}$')) {
        throw 'Az előtag kisbetűvel kezdődik, csak kisbetű és szám lehet, legfeljebb 13 karakter.'
    }

    $hyphen = "$Prefix-$Environment"
    $compact = "$Prefix$Environment"
    return @{
        Environment = $Environment
        OsaProResourceGroup = "$hyphen-rg"
        SharedResourceGroup = "$hyphen-shared-rg"
        OsaProFunctionApp = "$hyphen-func"
        SlackFunctionApp = "$hyphen-slack"
        AppInsights = "$hyphen-ai"
        LogAnalytics = "$hyphen-log"
        ExcelApp = "$hyphen-excel"
        OsaProStorage = "${compact}st"
        SlackStorage = "${compact}slackst"
        NotificationStorage = "${compact}tbl"
    }
}

function New-OsaProInventory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Environment,
        [string] $Prefix = ''
    )

    return @{
        status = 'partial'
        environment = $Environment
        prefix = $Prefix
        resources = @()
        secretLocations = @()
    }
}

function Get-OsaProResource {
    param(
        [Parameter(Mandatory = $true)]
        $Inventory,
        [Parameter(Mandatory = $true)]
        [string] $Key
    )

    return @($Inventory.resources) | Where-Object { $_.key -eq $Key } | Select-Object -First 1
}

function Add-OsaProInventoryResource {
    param(
        [Parameter(Mandatory = $true)]
        $Inventory,
        [Parameter(Mandatory = $true)]
        [string] $Key,
        [Parameter(Mandatory = $true)]
        [string] $Id
    )

    $definition = $script:Catalog | Where-Object { $_.Key -eq $Key } | Select-Object -First 1
    if (-not $definition) {
        throw "Ismeretlen leltárkulcs: $Key"
    }

    $name = $Id
    if ($definition.NameProperty) {
        $prefix = ''
        if ($Inventory -is [System.Collections.IDictionary] -and $Inventory.Contains('prefix') -and $null -ne $Inventory['prefix']) {
            $prefix = [string]$Inventory['prefix']
        }
        $names = Get-OsaProResourceNames -Environment ([string]$Inventory.environment) -Prefix $prefix
        $name = [string]$names[$definition.NameProperty]
    }

    $existing = Get-OsaProResource -Inventory $Inventory -Key $Key
    if ($existing) {
        if ($existing.id -ne $Id) {
            throw "A leltár $Key kulcsa másik azonosítót tartalmaz."
        }
        return $existing
    }

    $resource = @{
        key = $Key
        type = [string]$definition.Type
        id = $Id
        name = $name
        ownership = [string]$definition.Ownership
    }
    $Inventory.resources = @($Inventory.resources) + $resource
    return $resource
}

function Test-OsaProNameClaim {
    param(
        $Inventory,
        [string] $Name,
        [string] $LiveId,
        [string] $SubscriptionId = '',
        [string] $ExpectedResourceGroup = ''
    )

    if ([string]::IsNullOrWhiteSpace($LiveId)) {
        return 'absent'
    }

    $owned = @(@($Inventory.resources) | Where-Object { $_.id -eq $LiveId })
    if ($owned.Count -gt 0) {
        return 'owned'
    }

    if (-not [string]::IsNullOrWhiteSpace($SubscriptionId) -and -not [string]::IsNullOrWhiteSpace($ExpectedResourceGroup)) {
        $prefix = "/subscriptions/$SubscriptionId/resourceGroups/$ExpectedResourceGroup"
        if ($LiveId -eq $prefix -or $LiveId.StartsWith("$prefix/")) {
            return 'owned'
        }
    }

    return 'foreign'
}

function Protect-OsaProText {
    param(
        [AllowNull()]
        [string] $Text,
        [string[]] $SecretValues = @()
    )

    if ([string]::IsNullOrEmpty($Text)) {
        return ''
    }

    $redacted = $Text
    foreach ($secret in @($SecretValues)) {
        if (-not [string]::IsNullOrEmpty($secret)) {
            $redacted = $redacted.Replace($secret, '***')
        }
    }

    $redacted = [regex]::Replace($redacted, '(?i)(AccountKey=)[^;\s]+', '$1***')
    $redacted = [regex]::Replace($redacted, '(?i)(sig=)[^&\s]+', '$1***')
    $redacted = [regex]::Replace($redacted, '(?i)Bearer\s+\S+', 'Bearer ***')
    $redacted = [regex]::Replace($redacted, '(?i)(client_secret["\s:=]+)[^"&\s]+', '$1***')
    return $redacted
}

function Protect-OsaProInventory {
    param(
        [Parameter(Mandatory = $true)]
        $Inventory
    )

    $resources = @()
    foreach ($resource in @($Inventory.resources)) {
        $clean = @{
            key = [string]$resource.key
            type = [string]$resource.type
            id = [string]$resource.id
            name = [string]$resource.name
            ownership = [string]$resource.ownership
        }
        if ($resource.ContainsKey('permissionsGranted')) {
            $clean.permissionsGranted = [bool]$resource.permissionsGranted
        }
        if ($resource.ContainsKey('consentGranted')) {
            $clean.consentGranted = [bool]$resource.consentGranted
        }
        $resources += $clean
    }

    $locations = @()
    foreach ($location in @($Inventory.secretLocations)) {
        $locations += @{
            key = [string]$location.key
            location = [string]$location.location
        }
    }

    $prefix = ''
    if ($Inventory -is [System.Collections.IDictionary] -and $Inventory.Contains('prefix') -and $null -ne $Inventory['prefix']) {
        $prefix = [string]$Inventory['prefix']
    }

    return @{
        status = [string]$Inventory.status
        environment = [string]$Inventory.environment
        prefix = $prefix
        resources = $resources
        secretLocations = $locations
    }
}

function Get-OsaProDestroyPreview {
    param(
        [Parameter(Mandatory = $true)]
        $Inventory,
        [Parameter(Mandatory = $true)]
        [ValidateSet('OsaPro', 'Shared')]
        [string] $Target
    )

    $selected = @()
    foreach ($resource in @($Inventory.resources)) {
        if ([string]$resource.type -like 'sharePoint*') {
            continue
        }

        $definition = $script:Catalog | Where-Object { $_.Key -eq $resource.key } | Select-Object -First 1
        if (-not $definition -or $definition.Target -ne $Target) {
            continue
        }

        $selected += $resource
    }

    return $selected
}

function Get-OsaProDestroySet {
    param(
        [Parameter(Mandatory = $true)]
        $Inventory,
        [Parameter(Mandatory = $true)]
        [ValidateSet('OsaPro', 'Shared')]
        [string] $Target,
        [switch] $Confirm
    )

    if (-not $Confirm) {
        return
    }

    return @(Get-OsaProDestroyPreview -Inventory $Inventory -Target $Target)
}

function Test-OsaProDestroyIds {
    param(
        [Parameter(Mandatory = $true)]
        $Planned,
        [Parameter(Mandatory = $true)]
        $LiveIdsByKey
    )

    $blocked = @()
    foreach ($resource in @($Planned)) {
        if (-not $LiveIdsByKey.ContainsKey($resource.key)) {
            continue
        }

        $liveId = $LiveIdsByKey[$resource.key]
        if (-not [string]::IsNullOrWhiteSpace($liveId) -and $liveId -ne $resource.id) {
            $blocked += [string]$resource.key
        }
    }

    return $blocked
}

Export-ModuleMember -Function @(
    'Get-OsaProCatalog'
    'Get-OsaProResourceNames'
    'New-OsaProInventory'
    'Get-OsaProResource'
    'Add-OsaProInventoryResource'
    'Test-OsaProNameClaim'
    'Protect-OsaProText'
    'Protect-OsaProInventory'
    'Get-OsaProDestroyPreview'
    'Get-OsaProDestroySet'
    'Test-OsaProDestroyIds'
)
