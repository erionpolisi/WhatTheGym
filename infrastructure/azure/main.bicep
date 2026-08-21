// WhatTheGym Azure infrastructure (prepared, not yet deployed).
// Cost-optimized around the <= 10 EUR/month target; see docs/adr/0008-azure-cost-plan.md
// for the tradeoffs (managed PostgreSQL alone exceeds the cap).
//
// Deploy (when deliberately going live):
//   az deployment group create -g <rg> -f main.bicep -p @parameters.staging.json

@allowed(['staging', 'production'])
param environmentName string

param location string = resourceGroup().location

@description('Container image for the API, e.g. <registry>.azurecr.io/whatthegym-api:tag')
param apiImage string

@description('Deploy Azure Database for PostgreSQL Flexible Server. When false, an external PostgreSQL (e.g. free tier provider) is used via the connection string secret.')
param deployPostgres bool = false

@secure()
@description('PostgreSQL admin password (only used when deployPostgres = true).')
param postgresAdminPassword string = ''

@secure()
@description('Full PostgreSQL connection string stored in Key Vault (used when deployPostgres = false).')
param externalPostgresConnectionString string = ''

@description('Allowed CORS origin of the frontend, e.g. https://staging.whatthegym.at')
param frontendOrigin string

var prefix = 'wtg-${environmentName}'
var tags = {
  project: 'whatthegym'
  environment: environmentName
}

// ---------- Observability (ingestion capped to stay inside the budget) ----------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
    workspaceCapping: { dailyQuotaGb: json('0.1') }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-insights'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ---------- Key Vault ----------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${prefix}-kv'
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
  }
}

resource postgresConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!deployPostgres) {
  parent: keyVault
  name: 'postgres-connection-string'
  properties: {
    value: externalPostgresConnectionString
  }
}

// ---------- Optional managed PostgreSQL (exceeds the 10 EUR cap on its own) ----------
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = if (deployPostgres) {
  name: '${prefix}-pg'
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: 'wtgadmin'
    administratorLoginPassword: postgresAdminPassword
    storage: { storageSizeGB: 32 }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: { mode: 'Disabled' }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = if (deployPostgres) {
  parent: postgres
  name: 'whatthegym'
}

// ---------- Container Apps (consumption, scale to zero) ----------
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-cae'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-api'
  location: location
  tags: tags
  identity: { type: 'SystemAssigned' }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      secrets: [
        {
          name: 'postgres-connection'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/postgres-connection-string'
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: environmentName == 'production' ? 'Production' : 'Staging' }
            { name: 'ConnectionStrings__Postgres', secretRef: 'postgres-connection' }
            { name: 'Database__MigrateOnStartup', value: 'true' }
            { name: 'Seed__SeedCatalog', value: 'true' }
            { name: 'Seed__SeedDemoData', value: 'false' }
            { name: 'Auth__EnableDevLogin', value: 'false' }
            { name: 'Cors__AllowedOrigins__0', value: frontendOrigin }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

// Grant the API's managed identity read access to Key Vault secrets.
resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiApp.id, 'kv-secrets-user')
  scope: keyVault
  properties: {
    principalId: apiApp.identity.principalId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
    )
    principalType: 'ServicePrincipal'
  }
}

// ---------- Frontend: Static Web App (Free tier) ----------
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: '${prefix}-web'
  location: 'westeurope'
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    stagingEnvironmentPolicy: 'Disabled'
    allowConfigFileUpdates: true
  }
}

output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
output keyVaultName string = keyVault.name
