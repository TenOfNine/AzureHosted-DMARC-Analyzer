@description('Azure region for the resource')
param location string

@description('App Service Plan name')
param appServicePlanName string

@description('SKU name. Must support Always On (B1 or higher) since the app relies on it for both web responsiveness and the in-process background ingestion/retention services.')
param skuName string = 'B1'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: skuName
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

output appServicePlanId string = appServicePlan.id
