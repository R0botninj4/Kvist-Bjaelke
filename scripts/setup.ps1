$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $projectRoot '.env'
if (Test-Path -LiteralPath $envPath) { return }
$jwtSecret = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$adminPassword = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))
@"
JWT_SECRET=$jwtSecret
ADMIN_USERNAME=admin
ADMIN_PASSWORD=$adminPassword
"@ | Set-Content -LiteralPath $envPath -Encoding utf8
Write-Host 'Oprettet .env. Find dit admin-login i .env (del ikke filen).'
