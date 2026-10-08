[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$clientId = "505244859145-clbp2iurv5qucqaqcq6t0v4eeeed1g23.apps.googleusercontent.com"
$clientSecretSecure = Read-Host "New Google Web OAuth client secret" -AsSecureString
$secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($clientSecretSecure)

try {
    $clientSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)

    & az containerapp secret set `
        --resource-group wtg-staging `
        --name wtg-staging-api `
        --secrets "google-client-secret=$clientSecret" `
        --only-show-errors `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Updating the Google OAuth secret failed."
    }

    & az containerapp update `
        --resource-group wtg-staging `
        --name wtg-staging-api `
        --set-env-vars `
        "Auth__GoogleClientId=$clientId" `
        "Auth__GoogleClientSecret=secretref:google-client-secret" `
        --only-show-errors `
        --output none
    if ($LASTEXITCODE -ne 0) {
        throw "Updating the Google OAuth configuration failed."
    }
}
finally {
    if ($secretPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    }

    $clientSecret = $null
}
