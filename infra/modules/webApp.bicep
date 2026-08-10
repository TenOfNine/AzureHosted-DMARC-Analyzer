@description('Azure region for the resource')
param location string

@description('Web App name (globally unique — becomes <name>.azurewebsites.net)')
param webAppName string

param appServicePlanId string
param appInsightsConnectionString string
param keyVaultUri string
param sqlServerFqdn string
param sqlDatabaseName string

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsightsConnectionString
        }
        {
          name: 'KeyVault__Uri'
          value: keyVaultUri
        }
        {
          // Azure AD (managed identity) auth — no password. The Web App's own system-assigned
          // identity must be granted a database user + role via infra/post-deploy-sql-grant.sql
          // after this template deploys (see docs/deployment.md).
          name: 'ConnectionStrings__DmarcAnalyzer'
          value: 'Server=tcp:${sqlServerFqdn},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output webAppPrincipalId string = webApp.identity.principalId
output webAppHostName string = webApp.properties.defaultHostName
