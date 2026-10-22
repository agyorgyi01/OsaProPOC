targetScope = 'subscription'

@description('dev vagy prod. A nevek ebből és az osp előtagból képződnek.')
@allowed([
  'dev'
  'prod'
])
param environment string

@description('Azure-régió mindkét erőforráscsoportnak.')
param location string

var osaproResourceGroupName = 'osp-${environment}-rg'
var sharedResourceGroupName = 'osp-${environment}-shared-rg'
var osaproFunctionAppName = 'osp-${environment}-func'
var slackFunctionAppName = 'osp-${environment}-slack'
var appInsightsName = 'osp-${environment}-ai'
var workspaceName = 'osp-${environment}-log'
var osaproStorageName = 'osp${environment}st'
var slackStorageName = 'osp${environment}slackst'
var notificationStorageName = 'osp${environment}tbl'

resource osaproRg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: osaproResourceGroupName
  location: location
}

resource sharedRg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: sharedResourceGroupName
  location: location
}

module shared 'shared.bicep' = {
  name: 'shared'
  scope: sharedRg
  params: {
    location: location
    functionAppName: slackFunctionAppName
    storageName: slackStorageName
    appInsightsName: appInsightsName
    workspaceName: workspaceName
  }
}

module osapro 'osapro.bicep' = {
  name: 'osapro'
  scope: osaproRg
  params: {
    location: location
    functionAppName: osaproFunctionAppName
    storageName: osaproStorageName
    notificationStorageName: notificationStorageName
    appInsightsConnectionString: shared.outputs.appInsightsConnectionString
  }
}

output osaproResourceGroupId string = osaproRg.id
output sharedResourceGroupId string = sharedRg.id
output osaproFunctionAppId string = osapro.outputs.functionAppId
output slackFunctionAppId string = shared.outputs.functionAppId
output osaproStorageId string = osapro.outputs.storageId
output slackStorageId string = shared.outputs.storageId
output notificationStorageId string = osapro.outputs.notificationStorageId
output appInsightsId string = shared.outputs.appInsightsId
output workspaceId string = shared.outputs.workspaceId
output osaproPrincipalId string = osapro.outputs.principalId
output slackPrincipalId string = shared.outputs.principalId
