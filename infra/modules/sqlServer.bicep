@description('Azure region for the resources')
param location string

@description('Globally-unique SQL logical server name')
param sqlServerName string

@description('Database name')
param sqlDatabaseName string = 'DmarcAnalyzer'

@description('Azure AD object ID (SID) of the user or group to set as the Azure-AD-only SQL administrator')
param sqlAdminAadObjectId string

@description('Display name (UPN or group name) of the Azure AD SQL administrator')
param sqlAdminAadLogin string

@allowed(['User', 'Group', 'Application'])
param sqlAdminPrincipalType string = 'User'

@description('Resource tags applied for asset inventory/governance (MCSB GS-1).')
param tags object = {}

@description('Log Analytics workspace resource ID to send SQL audit logs to (MCSB LT-1/LT-3) — enables server-level auditing across all databases.')
param logAnalyticsWorkspaceId string = ''

// Azure-AD-only authentication: no SQL password is ever created or stored. The Web App connects
// using its own managed identity (granted DB access via a post-deploy SQL script — see
// infra/post-deploy-sql-grant.sql — since Bicep cannot create SQL database users).
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: sqlAdminPrincipalType
      login: sqlAdminAadLogin
      sid: sqlAdminAadObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: 'GP_S_Gen5'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 1
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: json('0.5')
    zoneRedundant: false
    // Zone- (not geo-) redundant backups (MCSB BR-1): keeps automatic PITR backups resilient to a
    // zone failure without replicating this data outside the deployment region — relevant given the
    // project defaults to Germany West Central specifically for data-residency reasons.
    requestedBackupStorageRedundancy: 'Zone'
  }
}

// Allows Azure-hosted resources (the App Service) to reach the server; no VNet integration in this MVP.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Server-level SQL auditing (MCSB LT-1/LT-3), covering every database under this server. This turns
// on the audit event stream; the diagnosticSettings resource below routes it to Log Analytics.
resource auditingSettings 'Microsoft.Sql/servers/auditingSettings@2023-08-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: 'send-to-log-analytics'
  scope: sqlDatabase
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLSecurityAuditEvents'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Basic'
        enabled: true
      }
    ]
  }
  dependsOn: [
    auditingSettings
  ]
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
