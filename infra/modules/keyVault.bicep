@description('Azure region for the resource')
param location string

@description('Globally-unique Key Vault name (max 24 chars)')
param keyVaultName string

@description('Azure AD tenant ID that owns this vault')
param tenantId string = subscription().tenantId

@description('Resource tags applied for asset inventory/governance (MCSB GS-1).')
param tags object = {}

@description('Log Analytics workspace resource ID to send Key Vault audit logs to (MCSB LT-1/LT-3) — every secret read/write, including the Graph client secret, is auditable.')
param logAnalyticsWorkspaceId string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    // RBAC (not access-policy) authorization: the Web App's managed identity is granted
    // "Key Vault Secrets Officer" via a role assignment in main.bicep. No secrets are created
    // here — the app writes its own Graph client secret at setup-wizard time.
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: 'send-to-log-analytics'
  scope: keyVault
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultResourceId string = keyVault.id
