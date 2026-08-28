[CmdletBinding()]
param(
    [string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS',
    [ValidateSet('ControlPlane','Operational','Company')]
    [string] $ModuleCode
)

$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Resolve-MigrationLayout.ps1')
$sqlCmd = Resolve-SqlServerV1SqlCmd
if ([string]::IsNullOrWhiteSpace($ModuleCode)) { throw 'Phai chi ro module kiem thu checksum.' }
$databaseName = switch ($ModuleCode) { 'ControlPlane' { 'TTSmart_Control_V1_Test' } 'Operational' { 'TTSmart_Operational_V1_Test' } 'Company' { 'TTSmart_Company_V1_Test' } }
$migration = switch ($ModuleCode) { 'ControlPlane' { '001_CreateSystemAndCompanyTables.sql' } 'Operational' { '001_CreateSystemAndMigrationTables.sql' } 'Company' { '001_CreateSystemMigrationAndCatalog.sql' } }
$directory = switch ($ModuleCode) { 'ControlPlane' { 'control-plane' } 'Operational' { 'operational' } 'Company' { 'company' } }
$migrationPath = Join-Path (Join-Path (Split-Path -Parent $PSScriptRoot) $directory) $migration
$badChecksum = ('0' * 64)
$source = Get-Content -LiteralPath $migrationPath -Raw
$match = [regex]::Match($source, "(?i)THROW\s+(\d+)\s*,\s*N'[^']*checksum")
if (-not $match.Success) { throw 'Khong tim thay ma loi checksum duoc khai bao trong migration.' }
$expectedError = $match.Groups[1].Value

$output = & $sqlCmd -S $ServerInstance -E -d $databaseName -b -I -f 'i:65001,o:65001' -v "ScriptChecksum=$badChecksum" -i $migrationPath 2>&1
if ($LASTEXITCODE -eq 0) { throw 'Checksum sai da khong lam migration dung lai.' }
$text = $output | Out-String
if ($text -notmatch "(?i)Msg\s+$expectedError\b") { throw "Checksum sai khong tra ve dung ma loi ${expectedError}: $text" }
Write-Output "Checksum mismatch test passed for $ModuleCode."
