// WhatTheGym Azure infrastructure (prepared, not yet deployed).
// Cost-optimized around the <= 10 EUR/month target; see docs/adr/0008-azure-cost-plan.md
// for the tradeoffs (managed PostgreSQL alone exceeds the cap).
//
// Deploy (when deliberately going live):
//   az deployment group create -g <rg> -f main.bicep -p @parameters.staging.json

@allowed(['staging', 'production'])
param environmentName string

param location string = resourceGroup().location

@description('Container image for the API, e.g. ghcr.io/<owner>/whatthegym-api:<tag> (ADR 0008 addendum: ghcr.io, no ACR)')
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

@description('Google OAuth client id of the BFF login.')
param googleClientId string

@secure()
@description('Google OAuth client secret of the BFF login.')
param googleClientSecret string

@description('Verified Google email that becomes the first Admin while no Admin exists.')
param bootstrapAdminEmail string

@description('Public frontend base URL used in mail links (case status, appeals), e.g. https://whatthegym.at')
param publicBaseUrl string

@secure()
@description('Secret for the daily-rotating analytics session-bucket HMAC.')
param analyticsHashSecret string

@secure()
@description('Resend API key for transactional mail. Empty means mails are only logged - do not run staging/production without it.')
param resendApiKey string = ''

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

// Connection secret is always present: either the managed flexible server (deployPostgres = true)
// or the externally provided connection string. The container app references it via Key Vault.
resource postgresConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'postgres-connection-string'
  properties: {
    // ARM's if() only evaluates the selected branch, so the reference is safe when deployPostgres = false.
    value: deployPostgres
      #disable-next-line BCP318
      ? 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=whatthegym;Username=wtgadmin;Password=${postgresAdminPassword};Ssl Mode=Require'
      : externalPostgresConnectionString
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
      secrets: concat(
        [
          {
            name: 'postgres-connection'
            keyVaultUrl: '${keyVault.properties.vaultUri}secrets/postgres-connection-string'
            identity: 'system'
          }
          {
            name: 'google-client-secret'
            value: googleClientSecret
          }
          {
            name: 'analytics-hash-secret'
            value: analyticsHashSecret
          }
        ],
        empty(resendApiKey)
          ? []
          : [
              {
                name: 'resend-api-key'
                value: resendApiKey
              }
            ]
      )
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
          env: concat(
            [
              { name: 'ASPNETCORE_ENVIRONMENT', value: environmentName == 'production' ? 'Production' : 'Staging' }
              { name: 'ConnectionStrings__Postgres', secretRef: 'postgres-connection' }
              { name: 'Database__MigrateOnStartup', value: 'true' }
              { name: 'Seed__SeedCatalog', value: 'true' }
              { name: 'Seed__SeedDemoData', value: 'false' }
              { name: 'Auth__EnableDevLogin', value: 'false' }
              { name: 'Auth__GoogleClientId', value: googleClientId }
              { name: 'Auth__GoogleClientSecret', secretRef: 'google-client-secret' }
              { name: 'Auth__BootstrapAdminEmail', value: bootstrapAdminEmail }
              { name: 'Mail__PublicBaseUrl', value: publicBaseUrl }
              { name: 'Analytics__HashSecret', secretRef: 'analytics-hash-secret' }
              // Ingress terminates TLS; the app must honor X-Forwarded-For/Proto for
              // rate limiting per client IP and correct OIDC redirect URIs.
              { name: 'ForwardedHeaders__Enabled', value: 'true' }
              { name: 'Cors__AllowedOrigins__0', value: frontendOrigin }
              { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
            ],
            empty(resendApiKey)
              ? []
              : [
                  { name: 'Mail__ResendApiKey', secretRef: 'resend-api-key' }
                ]
          )
        }
      ]
      scale: {
        // Cost decision (ADR 0008/0012): scale to zero stays. Tradeoff: hosted background
        // services (email outbox, retention sweeper) only run while an instance is warm;
        // pending work is picked up on the next request-triggered start.
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
