// WhatTheGym Azure infrastructure for the deployed Staging and Production environments.
// Cost-optimized around the <= 10 EUR/month target; see docs/adr/0008-azure-cost-plan.md
// for the tradeoffs (managed PostgreSQL alone exceeds the cap).
//
// Deploy/reconcile with the environment parameter file and secure runtime parameters.

@allowed(['staging', 'production'])
param environmentName string

param location string = resourceGroup().location

@description('Container image for the API, e.g. ghcr.io/<owner>/whatthegym-api:<tag> (ADR 0008 addendum: ghcr.io, no ACR)')
param apiImage string

@description('Container image for the Next.js frontend, e.g. ghcr.io/<owner>/whatthegym-web:<tag>.')
param frontendImage string

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

@description('Optional staging-only IPv4 CIDR allowed to access both public apps. Production always remains public.')
param allowedIngressIpv4Cidr string = ''

@description('Existing Container Apps environment name to reuse. Empty creates an environment for this deployment.')
param existingContainerAppsEnvironmentName string = ''

@description('Resource group of the existing Container Apps environment. Required when its name is set.')
param existingContainerAppsEnvironmentResourceGroup string = ''

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

var prefix = environmentName == 'production' ? 'wtg-prod' : 'wtg-staging'
var tags = {
  project: 'whatthegym'
  environment: environmentName
}
var dataProtectionStorageName = '${environmentName == 'production' ? 'wtgp' : 'wtgs'}${uniqueString(subscription().subscriptionId, resourceGroup().id)}dp'
var reuseContainerAppsEnvironment = !empty(existingContainerAppsEnvironmentName)
var applyIngressIpRestriction = environmentName == 'staging' && !empty(allowedIngressIpv4Cidr)
var containerAppsEnvironmentId = reuseContainerAppsEnvironment
  ? resourceId(
      existingContainerAppsEnvironmentResourceGroup,
      'Microsoft.App/managedEnvironments',
      existingContainerAppsEnvironmentName
    )
  : containerAppsEnvironment.id

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

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-api-identity'
  location: location
  tags: tags
}

resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: 'data-protection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'encrypt'
      'decrypt'
      'wrapKey'
      'unwrapKey'
    ]
  }
}

resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiIdentity.id, 'kv-secrets-user')
  scope: keyVault
  properties: {
    principalId: apiIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
    )
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultCryptoUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiIdentity.id, 'kv-crypto-user')
  scope: keyVault
  properties: {
    principalId: apiIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '12338af0-0e69-4776-bea7-57ae8d297424' // Key Vault Crypto User
    )
    principalType: 'ServicePrincipal'
  }
}

resource dataProtectionStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: dataProtectionStorageName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource dataProtectionBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: dataProtectionStorage
  name: 'default'
}

resource dataProtectionContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: dataProtectionBlobService
  name: 'data-protection'
  properties: {
    publicAccess: 'None'
  }
}

resource storageBlobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(dataProtectionStorage.id, apiIdentity.id, 'blob-data-contributor')
  scope: dataProtectionStorage
  properties: {
    principalId: apiIdentity.properties.principalId
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor
    )
    principalType: 'ServicePrincipal'
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

resource googleClientSecretResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'google-client-secret'
  properties: {
    value: googleClientSecret
  }
}

resource analyticsHashSecretResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'analytics-hash-secret'
  properties: {
    value: analyticsHashSecret
  }
}

resource resendApiKeySecretResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(resendApiKey)) {
  parent: keyVault
  name: 'resend-api-key'
  properties: {
    value: resendApiKey
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
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2026-07-01' = if (!reuseContainerAppsEnvironment) {
  name: '${prefix}-cae'
  location: location
  tags: tags
  properties: {
    environmentMode: 'WorkloadProfiles'
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-api'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        ipSecurityRestrictions: applyIngressIpRestriction
          ? [
              {
                name: 'staging-owner'
                description: 'Current staging owner public IPv4'
                ipAddressRange: allowedIngressIpv4Cidr
                action: 'Allow'
              }
            ]
          : []
      }
      secrets: concat(
        [
          {
            name: 'postgres-connection'
            keyVaultUrl: '${keyVault.properties.vaultUri}secrets/postgres-connection-string'
            identity: apiIdentity.id
          }
          {
            name: 'google-client-secret'
            keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${googleClientSecretResource.name}'
            identity: apiIdentity.id
          }
          {
            name: 'analytics-hash-secret'
            keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${analyticsHashSecretResource.name}'
            identity: apiIdentity.id
          }
        ],
        empty(resendApiKey)
          ? []
          : [
              {
                name: 'resend-api-key'
                #disable-next-line BCP318
                keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${resendApiKeySecretResource.name}'
                identity: apiIdentity.id
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
              {
                name: 'DataProtection__BlobUri'
                value: 'https://${dataProtectionStorage.name}.blob.${environment().suffixes.storage}/data-protection/keys.xml'
              }
              {
                name: 'DataProtection__KeyIdentifier'
                value: '${keyVault.properties.vaultUri}keys/${dataProtectionKey.name}'
              }
              { name: 'DataProtection__ManagedIdentityClientId', value: apiIdentity.properties.clientId }
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
  dependsOn: [
    keyVaultSecretsUser
    keyVaultCryptoUser
    storageBlobDataContributor
    dataProtectionContainer
  ]
}

resource frontendApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${prefix}-web'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: containerAppsEnvironmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 3000
        transport: 'http'
        ipSecurityRestrictions: applyIngressIpRestriction
          ? [
              {
                name: 'staging-owner'
                description: 'Current staging owner public IPv4'
                ipAddressRange: allowedIngressIpv4Cidr
                action: 'Allow'
              }
            ]
          : []
      }
    }
    template: {
      containers: [
        {
          name: 'web'
          image: frontendImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            // Same-environment service discovery bypasses the public API IP allowlist for SSR.
            { name: 'API_BASE_URL', value: 'http://${apiApp.name}' }
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

output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output frontendUrl string = 'https://${frontendApp.properties.configuration.ingress.fqdn}'
output keyVaultName string = keyVault.name
