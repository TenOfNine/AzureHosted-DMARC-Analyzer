@description('Azure region for the resource')
param location string

@description('Web App name (globally unique — becomes <name>.azurewebsites.net)')
param webAppName string

param appServicePlanId string
param appInsightsConnectionString string
param keyVaultUri string
param sqlServerFqdn string
param sqlDatabaseName string

@description('Gate every request behind Microsoft Entra ID sign-in (Azure App Service "Easy Auth"). Leave true unless the app sits behind some other equivalent access control (private network, gateway auth) — without this, the setup wizard and dashboard are reachable by anyone who can resolve the URL, since the app has no login of its own by design. See NFR-SEC-5/7 in the technical specification.')
param enableEntraIdAuth bool = true

@description('Client ID of the App Registration used for interactive sign-in. Can be the same App Registration used for Graph mailbox access (step 2 of docs/deployment.md) as long as it also has a Web platform redirect URI https://<webAppName>.azurewebsites.net/.auth/login/aad/callback and its own client secret. Required when enableEntraIdAuth is true.')
param authAadClientId string = ''

@description('Tenant ID that owns the sign-in App Registration. Defaults to the deploying subscription\'s tenant, which is correct for the overwhelmingly common case of signing in against the same tenant the app is deployed into.')
param authAadTenantId string = subscription().tenantId

@secure()
@description('Client secret for the sign-in App Registration — a different secret value than the Graph app\'s own (that one is entered later, in-app, via the setup wizard). Passed at deploy time only (GitHub Actions secret), never committed to source control. Required when enableEntraIdAuth is true.')
param authAadClientSecret string = ''

@description('Resource tags applied for asset inventory/governance (MCSB GS-1).')
param tags object = {}

@description('Log Analytics workspace resource ID to send App Service HTTP/console/platform logs to (MCSB LT-1/LT-3).')
param logAnalyticsWorkspaceId string = ''

var baseAppSettings = [
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

// Easy Auth resolves its client secret from an app setting named by clientSecretSettingName below —
// only created when auth is enabled, so a deploy with it disabled doesn't leave an empty secret setting.
var authAppSettings = enableEntraIdAuth ? [
  {
    name: 'MICROSOFT_PROVIDER_AUTHENTICATION_SECRET'
    value: authAadClientSecret
  }
] : []

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  tags: tags
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
      appSettings: concat(baseAppSettings, authAppSettings)
    }
  }
}

// Azure App Service Authentication ("Easy Auth") v2 — enforced at the platform level, in front of
// the whole app (setup wizard included), so no application code has to implement login itself. Every
// unauthenticated request is redirected to Entra ID sign-in before Kestrel ever sees it. This closes
// the gap previously documented as an accepted limitation (NFR-SEC-5 in the technical specification):
// without it, the setup wizard and dashboard — including the ability to rotate the Graph client
// secret and add/remove monitored mailboxes — are open to anyone who can reach the URL.
resource authSettings 'Microsoft.Web/sites/config@2023-12-01' = if (enableEntraIdAuth) {
  parent: webApp
  name: 'authsettingsV2'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'RedirectToLoginPage'
      redirectToProvider: 'azureactivedirectory'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: authAadClientId
          clientSecretSettingName: 'MICROSOFT_PROVIDER_AUTHENTICATION_SECRET'
          openIdIssuer: '${environment().authentication.loginEndpoint}${authAadTenantId}/v2.0'
        }
        validation: {
          defaultAuthorizationPolicy: {
            allowedApplications: [
              authAadClientId
            ]
          }
        }
      }
    }
    login: {
      tokenStore: {
        enabled: true
      }
    }
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(logAnalyticsWorkspaceId)) {
  name: 'send-to-log-analytics'
  scope: webApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
      }
      {
        category: 'AppServicePlatformLogs'
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

output webAppName string = webApp.name
output webAppPrincipalId string = webApp.identity.principalId
output webAppHostName string = webApp.properties.defaultHostName
