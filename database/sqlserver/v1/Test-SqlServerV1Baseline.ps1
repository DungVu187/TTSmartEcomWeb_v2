[CmdletBinding()]
param(
    [string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'Resolve-MigrationLayout.ps1')
$sqlCmd = Resolve-SqlServerV1SqlCmd
$root = $PSScriptRoot
$controlDatabase = 'TTSmart_Control_V1_Test'
$operationalDatabase = 'TTSmart_Operational_V1_Test'
. (Join-Path $root 'Resolve-MigrationLayout.ps1')

function Invoke-SqlFileTest([string] $Database, [string] $Path) {
    & $sqlCmd -S $ServerInstance -E -d $Database -b -I -f 'i:65001,o:65001' -i $Path
    if ($LASTEXITCODE -ne 0) { throw "Kiem thu SQL that bai: $([IO.Path]::GetFileName($Path))" }
}

function Assert-SchemaVersionChecksums([string] $Database, [string] $ModuleCode, [string] $MigrationRoot) {
    foreach ($migration in @(Resolve-SqlServerV1MigrationLayout -MigrationRoot $MigrationRoot)) {
        $checksum = (Get-FileHash -LiteralPath $migration.File.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        $result = & $sqlCmd -S $ServerInstance -E -d $Database -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'$ModuleCode' AND MigrationNumber=$($migration.Number) AND ScriptChecksum=N'$checksum') THEN N'OK' ELSE N'MISMATCH' END;"
        if ($LASTEXITCODE -ne 0 -or ($result | Out-String).Trim() -ne 'OK') { throw "Checksum SchemaVersions khong khop $ModuleCode/$($migration.Number.ToString('D3'))." }
    }
}

function Assert-OnlyMetadata([string] $Database) {
    $result = & $sqlCmd -S $ServerInstance -E -d $Database -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.dm_db_partition_stats AS p JOIN sys.tables AS t ON t.object_id=p.object_id WHERE p.index_id IN(0,1) AND p.row_count>0 AND t.is_ms_shipped=0 AND t.name NOT IN(N'SchemaVersions',N'DatabaseInfo')) THEN N'BUSINESS_ROWS' ELSE N'OK' END;"
    if ($LASTEXITCODE -ne 0 -or ($result | Out-String).Trim() -ne 'OK') { throw "Database $Database con du lieu ngoai metadata sau kiem thu." }
}

foreach ($database in @($controlDatabase,$operationalDatabase)) {
    $check = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$database') IS NULL OR DB_NAME(DB_ID(N'$database'))=N'$database' THEN N'OK' ELSE N'INVALID' END;"
    if ($LASTEXITCODE -ne 0 -or ($check | Out-String).Trim() -ne 'OK') { throw "Khong xac minh duoc database test duoc cap phep: $database" }
}

$controlRunner = Join-Path $root 'control-plane\Run-ControlPlaneBaseline.ps1'
$operationalRunner = Join-Path $root 'operational\Run-OperationalBaseline.ps1'
$verification = Join-Path $root 'verification'

# Recreate co preflight trong tung runner; chi hai database test literal o tren duoc phep DDL/DML.
& $controlRunner -ServerInstance $ServerInstance -Recreate
& $operationalRunner -ServerInstance $ServerInstance -Recreate

& (Join-Path $root 'control-plane\Test-ControlPlaneConcurrentFirstRun.ps1') -ServerInstance $ServerInstance
& (Join-Path $root 'operational\Test-OperationalConcurrentFirstRun.ps1') -ServerInstance $ServerInstance

# Chay lai sau first-run dong thoi de chung minh idempotent.
& $controlRunner -ServerInstance $ServerInstance
& $operationalRunner -ServerInstance $ServerInstance

& (Join-Path $verification 'Test-MigrationLayout.ps1')
& (Join-Path $verification 'Test-MigrationChecksumMismatch.ps1') -ServerInstance $ServerInstance -ModuleCode ControlPlane
& (Join-Path $verification 'Test-MigrationChecksumMismatch.ps1') -ServerInstance $ServerInstance -ModuleCode Operational

Invoke-SqlFileTest $controlDatabase (Join-Path $verification 'TestControlPlaneConstraints.sql')
Invoke-SqlFileTest $operationalDatabase (Join-Path $verification 'TestOperationalConstraints.sql')
& (Join-Path $verification 'Test-SchemaFingerprint.ps1') -ServerInstance $ServerInstance -DatabaseName $controlDatabase
& (Join-Path $verification 'Test-SchemaFingerprint.ps1') -ServerInstance $ServerInstance -DatabaseName $operationalDatabase

Assert-SchemaVersionChecksums $controlDatabase 'ControlPlane' (Join-Path $root 'control-plane')
Assert-SchemaVersionChecksums $operationalDatabase 'Operational' (Join-Path $root 'operational')
Invoke-SqlFileTest $controlDatabase (Join-Path $verification 'VerifyControlPlane.sql')
Invoke-SqlFileTest $operationalDatabase (Join-Path $verification 'VerifyOperational.sql')
Assert-OnlyMetadata $controlDatabase
Assert-OnlyMetadata $operationalDatabase

Write-Output 'SQL Server baseline v1 orchestration test passed.'
