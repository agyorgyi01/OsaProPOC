<#
.SYNOPSIS
    Létrehozza, ellenőrzi vagy lebontja az OsaPro infrastruktúráját.
.DESCRIPTION
    PowerShell 7, Azure CLI és Bicep szükséges. A titokértékek nem kerülnek a kimenetre.
    A parancsok leírása: infra/HASZNALAT.md
.EXAMPLE
    ./Invoke-OsaProInfrastructure.ps1 -Action Create -Environment dev -InputPath input.json -InventoryPath inventory.json
.EXAMPLE
    ./Invoke-OsaProInfrastructure.ps1 -Action Verify -Environment dev -InputPath input.json -InventoryPath inventory.json
.EXAMPLE
    ./Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target OsaPro -Environment dev -InputPath input.json -InventoryPath inventory.json
.EXAMPLE
    ./Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target OsaPro -Environment dev -Confirm -InputPath input.json -InventoryPath inventory.json
.EXAMPLE
    ./Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target Shared -Environment dev -Confirm -InputPath input.json -InventoryPath inventory.json
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Create', 'Verify', 'Destroy')]
    [string] $Action,
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'prod')]
    [string] $Environment,
    [Parameter(Mandatory = $true)]
    [string] $InputPath,
    [Parameter(Mandatory = $true)]
    [string] $InventoryPath,
    [ValidateSet('OsaPro', 'Shared')]
    [string] $Target = 'OsaPro',
    [switch] $Confirm
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'PowerShell 7 szükséges.'
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Import-Module (Join-Path $scriptDir 'Inventory.psm1') -Force
Import-Module (Join-Path $scriptDir 'Provision.psm1') -Force
Import-Module (Join-Path $scriptDir 'AzureGateway.psm1') -Force

function ConvertTo-HashtableRecursive {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [System.Collections.IDictionary]) {
        $converted = @{}
        foreach ($key in @($Value.Keys)) {
            $converted[$key] = ConvertTo-HashtableRecursive $Value[$key]
        }
        return $converted
    }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $converted = @{}
        foreach ($property in $Value.PSObject.Properties) {
            $converted[$property.Name] = ConvertTo-HashtableRecursive $property.Value
        }
        return $converted
    }
    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = @()
        foreach ($item in $Value) {
            $items += ConvertTo-HashtableRecursive $item
        }
        return $items
    }
    return $Value
}

$inputObject = ConvertTo-HashtableRecursive (Get-Content -Raw -Path $InputPath | ConvertFrom-Json)
$inputObject.environment = $Environment
if (Test-Path -LiteralPath $InventoryPath) {
    $inventory = ConvertTo-HashtableRecursive (Get-Content -Raw -Path $InventoryPath | ConvertFrom-Json)
    $inventory.resources = @($inventory.resources)
    $inventory.secretLocations = @($inventory.secretLocations)
    $recorded = ''
    if ($inventory.Contains('environment') -and $null -ne $inventory.environment) {
        $recorded = [string]$inventory.environment
    }
    if ([string]::IsNullOrWhiteSpace($recorded)) {
        $inventory.environment = $Environment
    }
    elseif ($recorded -ne $Environment) {
        throw "A leltár környezete $recorded, a paraméter $Environment."
    }
}
else {
    $inventory = New-OsaProInventory -Environment $Environment
}

$template = Join-Path (Split-Path -Parent $scriptDir) 'bicep\main.bicep'
$gateway = New-OsaProAzureGateway -TemplatePath $template
$variables = @{}
foreach ($entry in [Environment]::GetEnvironmentVariables().GetEnumerator()) {
    $variables[$entry.Key] = [string]$entry.Value
}

$save = {
    $clean = Protect-OsaProInventory -Inventory $inventory
    $json = $clean | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath $InventoryPath -Value $json -Encoding utf8
}

$result = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $inventory -Gateway $gateway -Action $Action -Target $Target -Approved:([bool]$Confirm) -EnvironmentVariables $variables -Save $save
& $save

foreach ($line in @($result.Log)) {
    Write-Output $line
}

if ($result.Status -ne 'complete') {
    exit 1
}
