[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^[0-9a-f]{40}$")]
    [string]$SourceSha
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$environmentFile = Join-Path $repositoryRoot ".env.production.local"
$parameterFile = Join-Path $PSScriptRoot "parameters.production.json"
$templateFile = Join-Path $PSScriptRoot "main.bicep"

if (-not (Test-Path $environmentFile)) {
    throw "Missing .env.production.local. Pull the production Neon environment before deploying."
}

$databaseUrlLine = Get-Content $environmentFile |
    Where-Object { $_ -match "^DATABASE_URL_UNPOOLED=" } |
    Select-Object -First 1

if (-not $databaseUrlLine) {
    throw "DATABASE_URL_UNPOOLED is missing from .env.production.local."
}

$databaseUrl = $databaseUrlLine.Substring($databaseUrlLine.IndexOf("=") + 1).Trim().Trim('"')
$databaseUri = [Uri]$databaseUrl
$userInfo = $databaseUri.UserInfo -split ":", 2

if ($userInfo.Count -ne 2) {
    throw "The Neon connection string has an invalid user-info section."
}

$username = [Uri]::UnescapeDataString($userInfo[0])
$password = [Uri]::UnescapeDataString($userInfo[1])
$database = [Uri]::UnescapeDataString($databaseUri.AbsolutePath.TrimStart("/"))
$port = if ($databaseUri.Port -gt 0) { $databaseUri.Port } else { 5432 }
$escapedUsername = $username.Replace('"', '""')
$escapedCredential = $password.Replace('"', '""')
$escapedDatabase = $database.Replace('"', '""')
$postgresConnectionString =
    "Host=$($databaseUri.Host);Port=$port;Database=`"$escapedDatabase`";" +
    "Username=`"$escapedUsername`";Pwd=`"$escapedCredential`";SSL Mode=Require"

$googleClientSecretSecure = Read-Host "Production Google OAuth client secret" -AsSecureString
$secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($googleClientSecretSecure)

try {
    $googleClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
    if ([string]::IsNullOrWhiteSpace($googleClientSecret)) {
        throw "The production Google OAuth client secret is required."
    }

    $analyticsBytes = New-Object byte[] 32
    $randomNumberGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
    $randomNumberGenerator.GetBytes($analyticsBytes)
    $randomNumberGenerator.Dispose()
    $analyticsHashSecret = [Convert]::ToBase64String($analyticsBytes)
    $deploymentName = "production-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"

    $arguments = @(
        "deployment", "group", "create",
        "--name", $deploymentName,
        "--resource-group", "wtg-prod",
        "--template-file", $templateFile,
        "--parameters", "@$parameterFile",
        "--parameters",
        "externalPostgresConnectionString=$postgresConnectionString",
        "googleClientSecret=$googleClientSecret",
        "analyticsHashSecret=$analyticsHashSecret",
        "apiImage=ghcr.io/erionpolisi/whatthegym-api:$SourceSha",
        "frontendImage=ghcr.io/erionpolisi/whatthegym-web:$SourceSha-production",
        "allowedIngressIpv4Cidr=",
        "resendApiKey=",
        "--query", "properties.outputs",
        "--output", "json"
    )

    & az @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Azure production deployment failed."
    }
}
finally {
    if ($secretPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    }

    $googleClientSecret = $null
    $postgresConnectionString = $null
    $escapedCredential = $null
    $password = $null
}
