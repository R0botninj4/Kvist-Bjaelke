$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if (-not (Test-Path -LiteralPath '.env')) { & "$PSScriptRoot/setup.ps1" }
    Get-Content -LiteralPath '.env' | ForEach-Object {
        if ($_ -match '^([A-Z_]+)=(.*)$') { Set-Item -LiteralPath "Env:$($Matches[1])" -Value $Matches[2] }
    }
    $env:BootstrapAdmin__Username = $env:ADMIN_USERNAME
    $env:JwtSecret = $env:JWT_SECRET
    $env:BootstrapAdmin__Password = $env:ADMIN_PASSWORD
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = 'http://localhost:3501'
    $env:ConnectionStrings__DefaultConnection = "Data Source=$projectRoot/app.db"
    dotnet run --project server/Security -c Release --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw 'Serveren kunne ikke starte' }
} finally { Pop-Location }
