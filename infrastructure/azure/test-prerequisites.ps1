[CmdletBinding()]
param(
    [ValidateSet("staging", "production")]
    [string]$Environment = "staging",

    [switch]$SkipAzureLogin
)

$ErrorActionPreference = "Stop"
$failureCount = 0

function Write-Check {
    param(
        [string]$Message,
        [bool]$Succeeded
    )

    if ($Succeeded) {
        Write-Host "[OK] $Message" -ForegroundColor Green
        return
    }

    $script:failureCount++
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Test-RequiredFile {
    param([string]$Path)

    $exists = Test-Path -LiteralPath $Path -PathType Leaf
    Write-Check "File exists: $Path" $exists
    return $exists
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$templateFile = Join-Path $PSScriptRoot "main.bicep"
$parameterFile = Join-Path $PSScriptRoot "parameters.$Environment.json"
$environmentFileName = if ($Environment -eq "production") { ".env.production.local" } else { ".env.local" }
$environmentFile = Join-Path $repositoryRoot $environmentFileName
$resourceGroup = if ($Environment -eq "production") { "wtg-prod" } else { "wtg-staging" }

Write-Host "Checking Azure prerequisites for '$Environment'..." -ForegroundColor Cyan

$azCommand = Get-Command az -ErrorAction SilentlyContinue
Write-Check "Azure CLI is installed" ($null -ne $azCommand)

$templateExists = Test-RequiredFile $templateFile
$parameterFileExists = Test-RequiredFile $parameterFile
$environmentFileExists = Test-RequiredFile $environmentFile

if ($parameterFileExists) {
    try {
        $parameterDocument = Get-Content -LiteralPath $parameterFile -Raw | ConvertFrom-Json
        $parameters = $parameterDocument.parameters
        $requiredParameters = @(
            "environmentName",
            "location",
            "apiImage",
            "frontendImage",
            "frontendOrigin",
            "googleClientId",
            "bootstrapAdminEmail",
            "publicBaseUrl"
        )
        $parameterNames = @($parameters.PSObject.Properties.Name)
        $missingParameters = @($requiredParameters | Where-Object { $_ -notin $parameterNames })

        Write-Check "Parameter file contains valid JSON" $true
        Write-Check "Parameter file targets '$Environment'" ($parameters.environmentName.value -eq $Environment)
        Write-Check "Required deployment parameters are present" ($missingParameters.Count -eq 0)

        if ($missingParameters.Count -gt 0) {
            Write-Host "       Missing: $($missingParameters -join ', ')" -ForegroundColor Yellow
        }

        $imageValues = @($parameters.apiImage.value, $parameters.frontendImage.value)
        Write-Check "Container image tags are configured" (-not ($imageValues | Where-Object { $_ -match "COMMIT_SHA|SET_" }))
    }
    catch {
        Write-Check "Parameter file contains valid JSON" $false
        Write-Host "       $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if ($environmentFileExists) {
    $databaseUrlLine = Get-Content -LiteralPath $environmentFile |
        Where-Object { $_ -match "^DATABASE_URL_UNPOOLED=.+" } |
        Select-Object -First 1
    Write-Check "$environmentFileName contains DATABASE_URL_UNPOOLED" ($null -ne $databaseUrlLine)
}

if ($null -ne $azCommand) {
    if ($templateExists) {
        $null = & az bicep build --file $templateFile --stdout 2>&1
        Write-Check "Bicep template compiles" ($LASTEXITCODE -eq 0)
    }

    if (-not $SkipAzureLogin) {
        $subscriptionId = & az account show --query id --output tsv 2>$null
        $isLoggedIn = $LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($subscriptionId)
        Write-Check "Azure CLI is logged in" $isLoggedIn

        if ($isLoggedIn) {
            $groupExists = & az group exists --name $resourceGroup 2>$null
            Write-Check "Resource group '$resourceGroup' exists" ($LASTEXITCODE -eq 0 -and $groupExists.Trim() -eq "true")
        }
    }
}

if ($failureCount -gt 0) {
    Write-Host "`nPreflight failed with $failureCount issue(s)." -ForegroundColor Red
    exit 1
}

Write-Host "`nAll Azure prerequisites passed." -ForegroundColor Green
