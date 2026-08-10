@description('Azure region for the resource')
param location string

@description('Globally-unique Key Vault name (max 24 chars)')
param keyVaultName string

@description('Azure AD tenant ID that owns this vault')
param tenantId string = subscription().tenantId

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
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

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultResourceId string = keyVault.id
