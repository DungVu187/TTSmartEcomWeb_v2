[CmdletBinding()]
param(
    [string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS'
)

$ErrorActionPreference = 'Stop'
$databaseName = 'TTSmart_Operational_V1_Test'
$runner = Join-Path $PSScriptRoot 'Run-OperationalBaseline.ps1'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Resolve-MigrationLayout.ps1')
$sqlCmd = Resolve-SqlServerV1SqlCmd

$safe = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; IF DB_ID(N'TTSmart_Operational_V1_Test') IS NULL SELECT N'OK'; ELSE IF EXISTS(SELECT 1 FROM [TTSmart_Operational_V1_Test].sys.dm_db_partition_stats AS p JOIN [TTSmart_Operational_V1_Test].sys.tables AS t ON t.object_id=p.object_id WHERE p.index_id IN(0,1) AND p.row_count>0 AND t.is_ms_shipped=0 AND t.name NOT IN(N'SchemaVersions',N'DatabaseInfo')) SELECT N'BLOCKED'; ELSE SELECT N'OK';"
if ($LASTEXITCODE -ne 0 -or ($safe | Out-String).Trim() -ne 'OK') { throw 'Database test co du lieu nghiep vu; khong the chay concurrent first-run.' }

& $sqlCmd -S $ServerInstance -E -d master -b -Q "IF DB_ID(N'TTSmart_Operational_V1_Test') IS NOT NULL BEGIN ALTER DATABASE [TTSmart_Operational_V1_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TTSmart_Operational_V1_Test]; END;"
if ($LASTEXITCODE -ne 0) { throw 'Khong the xoa database test truoc concurrent first-run.' }

$jobScript = { param($path,$server) $ErrorActionPreference = 'Stop'; & $path -ServerInstance $server }
$jobs = @(
    Start-Job -ScriptBlock $jobScript -ArgumentList $runner,$ServerInstance
    Start-Job -ScriptBlock $jobScript -ArgumentList $runner,$ServerInstance
)
try {
    Wait-Job -Job $jobs | Out-Null
    $receiveErrors = @()
    $output = $jobs | Receive-Job -ErrorAction SilentlyContinue -ErrorVariable +receiveErrors
    $failed = @($jobs | Where-Object { $_.State -ne 'Completed' -or $_.ChildJobs[0].JobStateInfo.State -ne 'Completed' })
    if ($failed.Count -ne 0) { throw "Concurrent first-run Operational that bai: $($output | Out-String) $($receiveErrors | Out-String)" }
}
finally {
    $jobs | Remove-Job -Force -ErrorAction SilentlyContinue
}

$result = & $sqlCmd -S $ServerInstance -E -d $databaseName -h -1 -W -s '|' -Q "SET NOCOUNT ON; SELECT CONVERT(nvarchar(20),COUNT(*))+N'|'+CONVERT(nvarchar(20),(SELECT COUNT(*) FROM dbo.DatabaseInfo)) FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational';"
if ($LASTEXITCODE -ne 0 -or ($result | Out-String).Trim() -ne '11|1') { throw 'Concurrent first-run khong tao dung 11 migration va mot DatabaseInfo Operational.' }
Write-Output 'Operational concurrent first-run passed.'
