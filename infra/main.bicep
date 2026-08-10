targetScope = 'resourceGroup'

@description('Environment name, used in resource naming (e.g. prod, dev). Distinguishes multiple deployments of this template into different resource groups for different customers/instances.')
param environmentName string = 'prod'

@description('Azure region for all resources')
param location string = 'germanywestcentral'

@description('Short prefix for resource names, e.g. dmarc-<customer>. Keep it short — it feeds into globally-unique names (Key Vault, SQL Server, Web App).')
@minLength(3)
@maxLength(20)
param namePrefix string

@description('SKU for the App Service Plan. Must support Always On (B1 or higher).')
param appServicePlanSku string = 'B1'

@description('Azure AD object ID (SID) of the user or group to set as the initial Azure-AD-only SQL administrator. This is a deploy-time DBA identity, not the app itself.')
param sqlAdminAadObjectId string

@description('Display name (UPN or group name) of the Azure AD SQL administrator')
param sqlAdminAadLogin string

@allowed(['User', 'Group', 'Application'])
param sqlAdminPrincipalType string = 'User'

var uniqueSuffix = uniqueString(resourceGroup().id, namePrefix, environmentName)
var keyVaultName = take('${namePrefix}kv${uniqueSuffix}', 24)
var sqlServerName = take('${toLower(namePrefix)}-sql-${uniqueSuffix}', 63)
var webAppName = take('${namePrefix}-web-${uniqueSuffix}', 60)
var appServicePlanName = '${namePrefix}-plan-${environmentName}'
var sqlDatabaseName = 'DmarcAnalyzer'

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    namePrefix: namePrefix
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    keyVaultName: keyVaultName
  }
}

module sqlServer 'modules/sqlServer.bicep' = {
  name: 'sqlServer'
  params: {
    location: location
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
    sqlAdminAadObjectId: sqlAdminAadObjectId
    sqlAdminAadLogin: sqlAdminAadLogin
    sqlAdminPrincipalType: sqlAdminPrincipalType
  }
}

module appServicePlan 'modules/appServicePlan.bicep' = {
  name: 'appServicePlan'
  params: {
    location: location
    appServicePlanName: appServicePlanName
    skuName: appServicePlanSku
  }
}

module webApp 'modules/webApp.bicep' = {
  name: 'webApp'
  params: {
    location: location
    webAppName: webAppName
    appServicePlanId: appServicePlan.outputs.appServicePlanId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    keyVaultUri: keyVault.outputs.keyVaultUri
    sqlServerFqdn: sqlServer.outputs.sqlServerFqdn
    sqlDatabaseName: sqlServer.outputs.sqlDatabaseName
  }
}

// Grant the Web App's managed identity permission to read/write secrets in Key Vault — the app
// writes its own Graph client secret here at setup-wizard time, it is never a deploy-time parameter.
var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource existingKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
  dependsOn: [
    keyVault
  ]
}

resource keyVaultSecretsRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(existingKeyVault.id, webAppName, keyVaultSecretsOfficerRoleId)
  scope: existingKeyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRoleId)
    principalId: webApp.outputs.webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output webAppName string = webApp.outputs.webAppName
output webAppHostName string = webApp.outputs.webAppHostName
output webAppPrincipalId string = webApp.outputs.webAppPrincipalId
output sqlServerName string = sqlServer.outputs.sqlServerName
output sqlServerFqdn string = sqlServer.outputs.sqlServerFqdn
output sqlDatabaseName string = sqlServer.outputs.sqlDatabaseName
output keyVaultName string = keyVault.outputs.keyVaultName
