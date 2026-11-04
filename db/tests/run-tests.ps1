<#
.SYNOPSIS
    Runs the database constraint tests (V001_constraints_test.sql) on the local PostgreSQL.

.DESCRIPTION
    By default the schema under test is the one produced by the EF Core migrations
    (dotnet ef migrations script). With -Reference the hand-written reference SQL
    (db/migrations/V001__safe_deal_schema.sql) is tested instead.
    Everything runs in a throw-away schema inside a transaction that is rolled back,
    so the real database is never changed. The connection string is read from .env.

.EXAMPLE
    ./db/tests/run-tests.ps1
.EXAMPLE
    ./db/tests/run-tests.ps1 -Reference
#>
param([switch]$Reference, [switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$testFile = Join-Path $PSScriptRoot 'V001_constraints_test.sql'

# --- connection settings from .env -------------------------------------------
$envLine = Get-Content (Join-Path $repoRoot '.env') -Encoding UTF8 |
    Where-Object { $_ -like 'ConnectionStrings__Default=*' } | Select-Object -First 1
if (-not $envLine) { throw 'ConnectionStrings__Default not found in .env' }

$cs = @{}
foreach ($part in $envLine.Substring('ConnectionStrings__Default='.Length) -split ';') {
    if ($part -match '^\s*([^=]+)=(.*)$') { $cs[$matches[1].Trim()] = $matches[2] }
}

$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) { $psql = 'C:\Program Files\PostgreSQL\17\bin\psql.exe' }

# --- schema under test ---------------------------------------------------------
$psqlArgs = @('-h', $cs['Host'], '-p', $cs['Port'], '-U', $cs['Username'], '-d', $cs['Database'],
              '-w', '-X', '-q', '-v', 'ON_ERROR_STOP=1')

$generated = $null
if (-not $Reference) {
    $generated = Join-Path ([IO.Path]::GetTempPath()) "zrobim-ef-schema-$PID.sql"
    Write-Host 'Generating SQL from the EF Core migrations...'
    Push-Location $repoRoot
    try {
        $migrationArgs = @('ef', 'migrations', 'script', '--no-transactions', '--project', 'src/Zrobim.Api', '--output', $generated)
        if ($NoBuild) { $migrationArgs += '--no-build' }
        dotnet @migrationArgs | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'dotnet ef migrations script failed' }
    }
    finally { Pop-Location }
    $psqlArgs += @('-v', "schema=$($generated -replace '\\', '/')")
    Write-Host 'Testing schema: EF Core migrations'
}
else {
    Write-Host 'Testing schema: db/migrations/V001__safe_deal_schema.sql'
}

# --- run ----------------------------------------------------------------------
$env:PGPASSWORD = $cs['Password']
$env:PGCLIENTENCODING = 'UTF8'
Push-Location $PSScriptRoot
try {
    # psql prints the PASS notices to stderr; in Windows PowerShell 5.1 that must not count as an error
    $ErrorActionPreference = 'Continue'
    & $psql @psqlArgs -f $testFile 2>&1 | ForEach-Object { "$_" -replace '^psql:.*?\.sql:\d+:\s*[^:]+:\s+', '' }
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
}
finally {
    Pop-Location
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    if ($generated) { Remove-Item $generated -ErrorAction SilentlyContinue }
}

if ($exitCode -ne 0) { Write-Host 'TESTS FAILED' -ForegroundColor Red; exit $exitCode }
Write-Host 'All database checks passed.' -ForegroundColor Green
