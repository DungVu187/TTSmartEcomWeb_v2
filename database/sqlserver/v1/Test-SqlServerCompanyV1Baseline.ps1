[CmdletBinding()]
param([string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'Resolve-MigrationLayout.ps1')
$sqlCmd = Resolve-SqlServerV1SqlCmd
$database = 'TTSmart_Company_V1_Test'
$runner = Join-Path $root 'company\Run-CompanyBaseline.ps1'
$verification = Join-Path $root 'verification'
function Invoke-CompanySqlFile([string] $path) { & $sqlCmd -S $ServerInstance -E -d $database -b -I -f 'i:65001,o:65001' -i $path; if ($LASTEXITCODE -ne 0) { throw "Kiem thu SQL that bai: $([IO.Path]::GetFileName($path))" } }
function Assert-Checksums {
    foreach ($migration in @(Resolve-SqlServerV1MigrationLayout -MigrationRoot (Join-Path $root 'company'))) {
        $checksum=(Get-FileHash -LiteralPath $migration.File.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        $result=& $sqlCmd -S $ServerInstance -E -d $database -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Company' AND MigrationNumber=$($migration.Number) AND ScriptChecksum=N'$checksum') THEN N'OK' ELSE N'MISMATCH' END;"
        if ($LASTEXITCODE -ne 0 -or ($result | Out-String).Trim() -ne 'OK') { throw "Checksum SchemaVersions khong khop Company/$($migration.Number.ToString('D3'))." }
    }
}
& $runner -ServerInstance $ServerInstance -Recreate
& (Join-Path $root 'company\Test-CompanyConcurrentFirstRun.ps1') -ServerInstance $ServerInstance
& $runner -ServerInstance $ServerInstance
& (Join-Path $verification 'Test-MigrationLayout.ps1')
& (Join-Path $verification 'Test-MigrationChecksumMismatch.ps1') -ServerInstance $ServerInstance -ModuleCode Company
Invoke-CompanySqlFile (Join-Path $verification 'TestCompanyConstraints.sql')
& (Join-Path $verification 'Test-SchemaFingerprint.ps1') -ServerInstance $ServerInstance -DatabaseName $database
Assert-Checksums
Invoke-CompanySqlFile (Join-Path $verification 'VerifyCompany.sql')
$rows=& $sqlCmd -S $ServerInstance -E -d $database -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.dm_db_partition_stats p JOIN sys.tables t ON t.object_id=p.object_id WHERE p.index_id IN(0,1) AND p.row_count>0 AND t.is_ms_shipped=0 AND t.name NOT IN(N'SchemaVersions',N'DatabaseInfo')) THEN N'ROWS' ELSE N'OK' END;"
if ($LASTEXITCODE -ne 0 -or ($rows | Out-String).Trim() -ne 'OK') { throw 'Company test con du lieu ngoai metadata sau rollback.' }
Write-Output 'SQL Server Company v1 baseline orchestration test passed.'
