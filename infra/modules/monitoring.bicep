@description('Azure region for the resources')
param location string

@description('Short prefix for resource names')
param namePrefix string

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: take('${namePrefix}-law', 63)
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: take('${namePrefix}-appi', 260)
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

output appInsightsConnectionString string = appInsights.properties.ConnectionString
output logAnalyticsWorkspaceId string = logAnalytics.id
