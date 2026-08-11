@description('Azure region for the resource')
param location string

@description('App Service Plan name')
param appServicePlanName string

@description('SKU name. Must support Always On (B1 or higher) since the app relies on it for both web responsiveness and the in-process background ingestion/retention services.')
param skuName string = 'B1'

@description('Resource tags applied for asset inventory/governance (MCSB GS-1).')
param tags object = {}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: skuName
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

output appServicePlanId string = appServicePlan.id
