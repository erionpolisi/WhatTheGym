[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$environmentFile = Join-Path $repositoryRoot ".env.local"
$parameterFile = Join-Path $PSScriptRoot "parameters.staging.json"
$templateFile = Join-Path $PSScriptRoot "main.bicep"

if (-not (Test-Path $environmentFile)) {
    throw "Missing .env.local. Run 'neon link' before deploying staging."
}

$databaseUrlLine = Get-Content $environmentFile |
    Where-Object { $_ -match "^DATABASE_URL_UNPOOLED=" } |
    Select-Object -First 1

if (-not $databaseUrlLine) {
    throw "DATABASE_URL_UNPOOLED is missing from .env.local."
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
$postgresConnectionString =
    "Host=$($databaseUri.Host);Port=$port;Database=$database;" +
    "Username=$username;Password=$password;SSL Mode=Require"
$publicIpv4 = (Invoke-RestMethod -Uri "https://api.ipify.org?format=json" -TimeoutSec 20).ip

if ($publicIpv4 -notmatch "^(?:\d{1,3}\.){3}\d{1,3}$") {
    throw "Could not determine a valid public IPv4 address."
}

$allowedIngressIpv4Cidr = "$publicIpv4/32"

$googleClientSecretSecure = Read-Host "Google OAuth client secret" -AsSecureString
$secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($googleClientSecretSecure)

try {
    $googleClientSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
    $analyticsBytes = New-Object byte[] 32
    $randomNumberGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
    $randomNumberGenerator.GetBytes($analyticsBytes)
    $randomNumberGenerator.Dispose()
    $analyticsHashSecret = [Convert]::ToBase64String($analyticsBytes)
    $deploymentName = "staging-$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"

    $arguments = @(
        "deployment", "group", "create",
        "--name", $deploymentName,
        "--resource-group", "wtg-staging",
        "--template-file", $templateFile,
        "--parameters", "@$parameterFile",
        "--parameters",
        "externalPostgresConnectionString=$postgresConnectionString",
        "googleClientSecret=$googleClientSecret",
        "analyticsHashSecret=$analyticsHashSecret",
        "allowedIngressIpv4Cidr=$allowedIngressIpv4Cidr",
        "resendApiKey=",
        "--query", "properties.outputs",
        "--output", "json"
    )

    & az @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Azure staging deployment failed."
    }
}
finally {
    if ($secretPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    }

    $googleClientSecret = $null
    $postgresConnectionString = $null
    $password = $null
}
