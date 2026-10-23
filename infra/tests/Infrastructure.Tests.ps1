Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $root 'infra\scripts\Inventory.psm1'))) {
    $root = Split-Path -Parent $PSScriptRoot
    $root = Split-Path -Parent $root
}
Import-Module (Join-Path $PSScriptRoot '..\scripts\Provision.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '..\scripts\Inventory.psm1') -Force

$script:Failed = 0

function Assert-True {
    param([bool] $Condition, [string] $Name)
    if ($Condition) {
        Write-Output "OK $Name"
    }
    else {
        Write-Output "FAIL $Name"
        $script:Failed++
    }
}

$dev = Get-OsaProResourceNames -Environment 'dev' -Prefix 'osp'
$prod = Get-OsaProResourceNames -Environment 'prod' -Prefix 'osp'
Assert-True ($dev.OsaProResourceGroup -eq 'osp-dev-rg' -and $dev.OsaProStorage -eq 'ospdevst') 'dev nevek'
Assert-True ($prod.OsaProResourceGroup -eq 'osp-prod-rg' -and $prod.OsaProStorage -eq 'ospprodst') 'prod nevek'
$custom = Get-OsaProResourceNames -Environment 'dev' -Prefix 'ab'
Assert-True ($custom.OsaProResourceGroup -eq 'ab-dev-rg' -and $custom.OsaProStorage -eq 'abdevst' -and $custom.SlackStorage -eq 'abdevslackst') 'elotag a nevben'
$invalidStopped = $false
try { Get-OsaProResourceNames -Environment 'test' -Prefix 'osp' | Out-Null } catch { $invalidStopped = $true }
Assert-True $invalidStopped 'ervenytelen kornyezet megall'
$invalidPrefix = $false
try { Get-OsaProResourceNames -Environment 'dev' -Prefix 'OSP' | Out-Null } catch { $invalidPrefix = $true }
Assert-True $invalidPrefix 'ervenytelen elotag megall'

$inventory = New-OsaProInventory -Environment 'dev' -Prefix 'osp'
$first = Add-OsaProInventoryResource -Inventory $inventory -Key 'osapro-rg' -Id '/id/osapro-rg'
$second = Add-OsaProInventoryResource -Inventory $inventory -Key 'osapro-rg' -Id '/id/osapro-rg'
Assert-True (@($inventory.resources).Count -eq 1 -and $first.id -eq $second.id) 'masodik futas ugyanaz az azonosito'

$foreignInventory = New-OsaProInventory -Environment 'dev'
$claim = Test-OsaProNameClaim -Inventory $foreignInventory -Name 'osp-dev-rg' -LiveId '/subscriptions/other/resourceGroups/osp-dev-rg'
Assert-True ($claim -eq 'foreign' -and @($foreignInventory.resources).Count -eq 0) 'idegen nev nincs a leltarban'
$sameSubscription = '11111111-2222-3333-4444-555555555555'
$inPlace = Test-OsaProNameClaim -Inventory (New-OsaProInventory -Environment 'dev') -Name 'ospdevtbl' -LiveId "/subscriptions/$sameSubscription/resourceGroups/osp-dev-rg/providers/Microsoft.Storage/storageAccounts/ospdevtbl" -SubscriptionId $sameSubscription -ExpectedResourceGroup 'osp-dev-rg'
Assert-True ($inPlace -eq 'owned') 'meglevo eroforras nem idegen'

$secretValue = 'super-secret-value'
$dirty = New-OsaProInventory -Environment 'dev' -Prefix 'osp'
Add-OsaProInventoryResource -Inventory $dirty -Key 'osapro-func' -Id '/id/osapro-func' | Out-Null
$dirty.resources[0].clientSecret = $secretValue
$dirty.secretLocations = @(@{ key = 'Secrets:ExcelClientSecret'; location = 'Secrets__ExcelClientSecret'; value = $secretValue })
$clean = Protect-OsaProInventory -Inventory $dirty
$cleanJson = $clean | ConvertTo-Json -Depth 6
$redacted = Protect-OsaProText -Text "Bearer eyJ.token client_secret=$secretValue AccountKey=storagekey sig=sassig" -SecretValues @($secretValue)
Assert-True ($cleanJson -notlike "*$secretValue*" -and $redacted -notlike "*$secretValue*" -and $redacted -notlike '*AccountKey=storagekey*' -and $redacted -notlike '*sig=sassig*' -and $redacted -notlike '*eyJ.token*') 'titok kikerul a leltarbol es a naplobol'

foreach ($key in @('osapro-func', 'osapro-storage', 'notification-storage', 'excel-app', 'excel-sp', 'shared-rg', 'slack-func', 'slack-storage', 'app-insights', 'workspace', 'sharepoint-folder')) {
    Add-OsaProInventoryResource -Inventory $inventory -Key $key -Id "/id/$key" | Out-Null
}
$emptySet = @(Get-OsaProDestroySet -Inventory $inventory -Target 'OsaPro')
$confirmed = @(Get-OsaProDestroySet -Inventory $inventory -Target 'OsaPro' -Confirm)
$confirmedKeys = @($confirmed | ForEach-Object { $_.key })
Assert-True ($emptySet.Count -eq 0) 'megerosites nelkul ures a halmaz'
Assert-True (($confirmedKeys -contains 'osapro-rg') -and ($confirmedKeys -contains 'osapro-func') -and ($confirmedKeys -contains 'osapro-storage') -and ($confirmedKeys -contains 'notification-storage') -and ($confirmedKeys -contains 'excel-app')) 'OsaPro halmaz'
Assert-True (($confirmedKeys -notcontains 'shared-rg') -and ($confirmedKeys -notcontains 'slack-func') -and ($confirmedKeys -notcontains 'app-insights') -and ($confirmedKeys -notcontains 'workspace') -and ($confirmedKeys -notcontains 'sharepoint-folder')) 'megosztott es SharePoint kimarad'

$bicep = Get-Content -Raw (Join-Path $PSScriptRoot '..\bicep\main.bicep')
$osaproBicep = Get-Content -Raw (Join-Path $PSScriptRoot '..\bicep\osapro.bicep')
$sharedBicep = Get-Content -Raw (Join-Path $PSScriptRoot '..\bicep\shared.bicep')
$allBicep = $bicep + $osaproBicep + $sharedBicep
Assert-True ($bicep -like '*${prefix}-${environment}-rg*' -and $bicep -like '*${prefix}${environment}st*' -and $bicep -match 'param prefix string') 'bicep nevkepzes'
Assert-True ($allBicep -notmatch 'alwaysReady') 'nincs always-ready'
Assert-True (@([regex]::Matches($bicep, "Microsoft.Resources/resourceGroups")).Count -eq 2) 'ket eroforrascsoport'
$outputNames = @([regex]::Matches($bicep, '(?m)^output\s+(\S+)')) | ForEach-Object { $_.Groups[1].Value }
$secretOutputs = @($outputNames | Where-Object { $_ -match '(?i)secret|password|token|connectionString|accountKey' })
Assert-True ($secretOutputs.Count -eq 0) 'gyoker kimenet nem titok'

function New-TestGateway {
    $state = @{
        PasswordCalls = 0
        PermissionCalls = 0
        Deletes = New-Object System.Collections.Generic.List[string]
        Names = New-Object System.Collections.Generic.List[string]
        Settings = @()
        Foreign = $false
        FailConsent = $false
        Hits = 0
        DeployCalls = 0
        StorageNameTaken = $false
        ForeignDuring = $false
        Live = @{}
    }
    $ids = @{
        osaproResourceGroupId = '/id/osapro-rg'
        osaproFunctionAppId = '/id/osapro-func'
        osaproStorageId = '/id/osapro-storage'
        notificationStorageId = '/id/notification-storage'
        sharedResourceGroupId = '/id/shared-rg'
        slackFunctionAppId = '/id/slack-func'
        slackStorageId = '/id/slack-storage'
        appInsightsId = '/id/app-insights'
        workspaceId = '/id/workspace'
    }
    $gateway = @{
        State = $state
        FindByName = {
            param($Type, $Name)
            $state.ForeignDuring = [bool]$state.Foreign
            if ($state.Foreign) {
                $state.Hits = 1
                return @{ id = "/foreign/$Name" }
            }
            return $null
        }.GetNewClosure()
        Deploy = {
            param($Environment, $Location, $Prefix)
            $state.DeployCalls++
            return $ids
        }.GetNewClosure()
        CheckStorageName = {
            param($Name)
            return -not [bool]$state.StorageNameTaken
        }.GetNewClosure()
        FindApplication = { param($DisplayName) return $null }.GetNewClosure()
        AddApplication = {
            param($DisplayName)
            return @{ appObjectId = '/id/excel-app'; appId = 'app-id'; servicePrincipalId = '/id/excel-sp' }
        }.GetNewClosure()
        AddPermissions = { param($AppObjectId) $state.PermissionCalls++ }.GetNewClosure()
        Consent = { param($ServicePrincipalId) if ($state.FailConsent) { throw 'consent hiba' } }.GetNewClosure()
        AddPassword = { param($AppObjectId) $state.PasswordCalls++; return $secretValue }.GetNewClosure()
        SetAppSettings = { param($FunctionAppId, $Settings) $state.Settings += ,$Settings }.GetNewClosure()
        GetLiveId = {
            param($Resource)
            if ($state.Live.ContainsKey($Resource.key)) { return $state.Live[$Resource.key] }
            return $Resource.id
        }.GetNewClosure()
        Delete = { param($Resource) [void]$state.Deletes.Add([string]$Resource.key) }.GetNewClosure()
    }
    return $gateway
}

$inputObject = @{
    tenantId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    subscriptionId = '11111111-2222-3333-4444-555555555555'
    location = 'westeurope'
    prefix = 'osp'
    environment = 'dev'
    excel = @{ retryCount = 3; retryDelaySeconds = 5 }
    nav = @{ baseUrl = 'https://api.onlineszamla.nav.gov.hu'; retryCount = 3; retryDelaySeconds = 5; software = @{ softwareId = 'HU-OSP-00000000001' } }
    secretVariables = @{ NavPassword = 'OSP_NAV_PASSWORD' }
}

$guardInventory = New-OsaProInventory -Environment 'dev'
$guardGateway = New-TestGateway
$guardStopped = $false
try {
    Invoke-OsaProInfrastructure -InputObject @{ environment = 'dev'; location = 'westeurope'; tenantId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa' } -Inventory $guardInventory -Gateway $guardGateway -Action 'Create' | Out-Null
}
catch {
    $guardStopped = $true
}
Assert-True ($guardStopped -and @($guardInventory.resources).Count -eq 0 -and $guardGateway.State.PasswordCalls -eq 0) 'hianyzo elofizetesnel nincs Azure-hivas'

$badPrefixGateway = New-TestGateway
$badPrefixStopped = $false
try {
    Invoke-OsaProInfrastructure -InputObject @{ environment = 'dev'; location = 'westeurope'; tenantId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'; subscriptionId = '11111111-2222-3333-4444-555555555555'; prefix = 'OSP' } -Inventory (New-OsaProInventory -Environment 'dev') -Gateway $badPrefixGateway -Action 'Create' | Out-Null
}
catch {
    $badPrefixStopped = $true
}
Assert-True ($badPrefixStopped -and $badPrefixGateway.State.DeployCalls -eq 0) 'ervenytelen elotag nem hiv Azure-t'

$prefixMismatchGateway = New-TestGateway
$prefixMismatch = $false
try {
    Invoke-OsaProInfrastructure -InputObject @{ environment = 'dev'; location = 'westeurope'; tenantId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'; subscriptionId = '11111111-2222-3333-4444-555555555555'; prefix = 'ab' } -Inventory (New-OsaProInventory -Environment 'dev' -Prefix 'osp') -Gateway $prefixMismatchGateway -Action 'Create' | Out-Null
}
catch {
    $prefixMismatch = $true
}
Assert-True ($prefixMismatch -and $prefixMismatchGateway.State.DeployCalls -eq 0) 'eltero leltarelotag megall'

Import-Module (Join-Path $PSScriptRoot '..\scripts\AzureGateway.psm1') -Force
$header = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"alg":"none"}')).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$ownPayload = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"tid":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}')).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$otherPayload = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"tid":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"}')).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$ownTokenAccepted = $false
try {
    Assert-OsaProTokenTenant -Token "$header.$ownPayload.sig" -TenantId 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    $ownTokenAccepted = $true
}
catch { $ownTokenAccepted = $false }
Assert-True $ownTokenAccepted 'a megadott berlo tokenje jo'
$foreignTokenStopped = $false
$foreignTokenMessage = ''
try {
    Assert-OsaProTokenTenant -Token "$header.$otherPayload.sig" -TenantId 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
}
catch {
    $foreignTokenStopped = $true
    $foreignTokenMessage = [string]$_.Exception.Message
}
Assert-True ($foreignTokenStopped -and $foreignTokenMessage -notlike '*bbbbbbbb*') 'az idegen berlo nem kerul ki'
$ownArm = $false
try {
    Assert-OsaProArmSubscription -ResourceId '/subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/osp-dev-rg' -SubscriptionId '11111111-2222-3333-4444-555555555555'
    $ownArm = $true
}
catch { $ownArm = $false }
Assert-True $ownArm 'sajat elofizetes azonositoja jo'
$foreignArm = $false
try {
    Assert-OsaProArmSubscription -ResourceId '/subscriptions/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/resourceGroups/osp-dev-rg' -SubscriptionId '11111111-2222-3333-4444-555555555555'
}
catch { $foreignArm = $true }
Assert-True $foreignArm 'idegen elofizetes azonositoja tilos'
$gatewayText = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '..\scripts\AzureGateway.psm1')
Assert-True ($gatewayText -notmatch 'account show' -and $gatewayText -notmatch 'account list' -and $gatewayText -match 'az login' -and $gatewayText -match '--tenant' -and $gatewayText -match '--subscription' -and $gatewayText -notmatch 'conflictBehavior') 'a cel nem a bejelentkezett fiokbol jon'
Assert-True ($gatewayText -match 'get-access-token[\s\S]{0,220}-SkipSubscription' -or $gatewayText -match '-SkipSubscription[\s\S]{0,80}get-access-token') 'a token csak a berlőt kapja'


$createdInventory = New-OsaProInventory -Environment 'dev'
$gateway = New-TestGateway
$firstRun = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $createdInventory -Gateway $gateway -Action 'Create'
$secondRun = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $createdInventory -Gateway $gateway -Action 'Create'
$settingsJson = ($gateway.State.Settings | ConvertTo-Json -Depth 6)
Assert-True ($firstRun.Status -eq 'complete' -and $secondRun.Status -eq 'complete') 'hianyzo NAV titok mellett teljes'
Assert-True ($gateway.State.PasswordCalls -eq 1 -and $gateway.State.PermissionCalls -eq 1) 'masodik futas nem ker uj titkot es engedelyt'
Assert-True (($settingsJson -notlike '*OSP_NAV_PASSWORD*') -and ($settingsJson -notlike '*Secrets__NavPassword*') -and ($settingsJson -notlike '*Nav__*') -and ($settingsJson -notlike '*Excel__*')) 'NAV es Excel beallitas nem masolodik'
$stored = Protect-OsaProInventory -Inventory $createdInventory | ConvertTo-Json -Depth 6
$logText = $firstRun.Log -join ' '
Assert-True (($stored -notlike "*$secretValue*") -and ($logText -notlike "*$secretValue*")) 'excel titok nincs a leltarban es a naploban'
Assert-True (@($createdInventory.resources | Where-Object { $_.key -eq 'sharepoint-folder' }).Count -eq 0) 'mappa nem keszul'

$partialInventory = New-OsaProInventory -Environment 'dev'
$partialGateway = New-TestGateway
$partialGateway.State.FailConsent = $true
$partial = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $partialInventory -Gateway $partialGateway -Action 'Create'
Assert-True ($partial.Status -eq 'partial' -and $partialInventory.status -eq 'partial') 'reszleges futas nem teljes siker'
$partialGateway.State.FailConsent = $false
Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $partialInventory -Gateway $partialGateway -Action 'Create' | Out-Null
Assert-True ($partialGateway.State.PasswordCalls -eq 1 -and (@($partialInventory.resources | Where-Object { $_.key -eq 'osapro-func' })).Count -eq 1) 'folytatas nem keszit masodik hosztot vagy titkot'

$foreignRunInventory = New-OsaProInventory -Environment 'dev'
$foreignGateway = New-TestGateway
$foreignGateway.State.Foreign = $true
$foreignRun = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $foreignRunInventory -Gateway $foreignGateway -Action 'Create'
Assert-True ($foreignRun.Status -eq 'partial' -and @($foreignRunInventory.resources).Count -eq 0) 'idegen nev nem kerul a leltarba'

$resumeInventory = New-OsaProInventory -Environment 'dev'
$resumeGateway = New-TestGateway
$resumeGateway.FindByName = {
    param($Type, $Name)
    $group = 'osp-dev-rg'
    if ($Name -eq 'osp-dev-shared-rg' -or $Name -eq 'osp-dev-slack' -or $Name -eq 'ospdevslackst' -or $Name -eq 'osp-dev-ai' -or $Name -eq 'osp-dev-log') {
        $group = 'osp-dev-shared-rg'
    }
    $subscription = '11111111-2222-3333-4444-555555555555'
    if ($Type -eq 'resourceGroup') {
        return @{ id = "/subscriptions/$subscription/resourceGroups/$Name" }
    }
    return @{ id = "/subscriptions/$subscription/resourceGroups/$group/providers/Microsoft.Resources/$Name" }
}.GetNewClosure()
$resumed = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $resumeInventory -Gateway $resumeGateway -Action 'Create'
Assert-True ($resumed.Status -eq 'complete' -and $resumeGateway.State.DeployCalls -eq 1 -and @($resumeInventory.resources | Where-Object { $_.key -eq 'osapro-func' }).Count -eq 1) 'meglevo eroforras nem keszul ujra'

$takenInventory = New-OsaProInventory -Environment 'dev'
$takenGateway = New-TestGateway
$takenGateway.State.StorageNameTaken = $true
$taken = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $takenInventory -Gateway $takenGateway -Action 'Create'
$takenLog = $taken.Log -join ' '
Assert-True ($taken.Status -eq 'partial' -and $takenGateway.State.DeployCalls -eq 0 -and $takenLog -like '*ospdevst*' -and $takenLog -like '*foglalt*') 'elofizetesen kivul foglalt storage nem települ'

$destroyGateway = New-TestGateway
$preview = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $inventory -Gateway $destroyGateway -Action 'Destroy' -Target 'OsaPro'
Assert-True ($preview.DestroySet.Count -eq 0 -and $destroyGateway.State.Deletes.Count -eq 0 -and $preview.DestroyPreview.Count -gt 0) 'elonezet torles nelkul'

$mismatchGateway = New-TestGateway
$mismatchGateway.State.Live['osapro-rg'] = '/other-id'
$mismatch = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $inventory -Gateway $mismatchGateway -Action 'Destroy' -Target 'OsaPro' -Approved
Assert-True ($mismatch.Status -eq 'partial' -and $mismatchGateway.State.Deletes.Count -eq 0) 'azonosito elteresnel nincs torles'

$deleteGateway = New-TestGateway
$deleted = Invoke-OsaProInfrastructure -InputObject $inputObject -Inventory $inventory -Gateway $deleteGateway -Action 'Destroy' -Target 'OsaPro' -Approved
$deletedKeys = @($deleteGateway.State.Deletes)
Assert-True (($deletedKeys -contains 'notification-storage') -and ($deletedKeys -contains 'excel-app') -and ($deletedKeys -notcontains 'shared-rg') -and ($deletedKeys -notcontains 'slack-func') -and ($deletedKeys -notcontains 'app-insights') -and ($deletedKeys -notcontains 'sharepoint-folder')) 'OsaPro torles vedett celok nelkul'

$scriptPath = (Resolve-Path (Join-Path $PSScriptRoot '..\scripts\Invoke-OsaProInfrastructure.ps1')).Path
$scriptText = Get-Content -Raw -Encoding UTF8 $scriptPath
Assert-True ($scriptText -match 'Connect-OsaProAccount') 'a script a megadott berlovel jelentkezik be'
$helpText = $scriptText
$helper = [powershell]::Create()
[void]$helper.AddScript("Get-Help -Name '$scriptPath' -Examples | Out-String")
$handle = $helper.BeginInvoke()
if ($handle.AsyncWaitHandle.WaitOne(15000)) {
    $received = $helper.EndInvoke($handle)
    if ($received) { $helpText = [string]$received }
}
else {
    $helper.Stop()
}
$helper.Dispose()
$docText = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot '..\HASZNALAT.md')
$commands = @(
    './Invoke-OsaProInfrastructure.ps1 -Action Create -Environment dev -InputPath input.json -InventoryPath inventory.json'
    './Invoke-OsaProInfrastructure.ps1 -Action Verify -Environment dev -InputPath input.json -InventoryPath inventory.json'
    './Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target OsaPro -Environment dev -InputPath input.json -InventoryPath inventory.json'
    './Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target OsaPro -Environment dev -Confirm -InputPath input.json -InventoryPath inventory.json'
    './Invoke-OsaProInfrastructure.ps1 -Action Destroy -Target Shared -Environment dev -Confirm -InputPath input.json -InventoryPath inventory.json'
)
$commandsFound = $true
foreach ($command in $commands) {
    $inScript = $scriptText -like "*$command*"
    $inHelp = ($helpText -replace '\s', '') -like ("*" + ($command -replace '\s', '') + "*")
    $inDoc = ($docText -replace '\s+', ' ') -like ("*" + ($command -replace '\s+', ' ') + "*")
    if (-not $inScript -or -not $inHelp -or -not $inDoc) {
        $commandsFound = $false
        Write-Output ("MISSING script=$inScript help=$inHelp doc=$inDoc :: $command")
    }
}
Assert-True $commandsFound 'a parancsok a sugoban es a leirasban vannak'
Assert-True ($docText -like '*küldési állapot elvész*' -and $docText -like '*telemetria elvész*' -and $docText -notmatch '(?i)AccountKey=|client_secret|Bearer |sig=') 'a leiras jeloli az adatvesztest es nincs benne titok'

$bicepExe = $env:BICEP
if (-not $bicepExe) {
    $command = Get-Command bicep -ErrorAction SilentlyContinue
    if ($command) { $bicepExe = $command.Source }
}
if (-not $bicepExe) {
    $candidate = Join-Path $env:TEMP 'bicep.exe'
    if (Test-Path $candidate) { $bicepExe = $candidate }
}
if (-not $bicepExe) {
    Assert-True $false 'bicep nem erheto el'
}
else {
    & $bicepExe build (Join-Path $PSScriptRoot '..\bicep\main.bicep') --outfile (Join-Path $env:TEMP 'osp-main.json')
    Assert-True ($LASTEXITCODE -eq 0) 'bicep build'
    if (Test-Path (Join-Path $env:TEMP 'osp-main.json')) {
        Remove-Item (Join-Path $env:TEMP 'osp-main.json') -Force
    }
}

if ($script:Failed -gt 0) {
    Write-Output "$script:Failed teszt sikertelen"
    exit 1
}

Write-Output 'Minden infrastruktura-teszt sikeres.'
exit 0
