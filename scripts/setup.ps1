$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'
if (Test-Path -LiteralPath $envPath) {
    Write-Host '.env findes allerede. Eksisterende adgangskoder bevares.'
    exit 0
}
$jwt = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$adminPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
@"
JWT_SECRET=$jwt
ADMIN_USERNAME=admin
ADMIN_PASSWORD=$adminPassword
"@ | Set-Content -LiteralPath $envPath -Encoding utf8
Write-Host 'Oprettet .env med tilfældige nøgler. Find dit admin-login i .env (del ikke filen).'
