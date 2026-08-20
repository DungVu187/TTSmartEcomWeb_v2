[CmdletBinding()]
param(
    [string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS',
    [switch] $Recreate
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0
$databaseName = 'TTSmart_Control_V1_Test'
$scriptRoot = $PSScriptRoot
. (Join-Path (Split-Path -Parent $scriptRoot) 'Resolve-MigrationLayout.ps1')
$migrations = @(Resolve-SqlServerV1MigrationLayout -MigrationRoot $scriptRoot)
$sqlCmd = Resolve-SqlServerV1SqlCmd

function Invoke-ControlPlaneSql([string] $database, [string] $path, [hashtable] $variables = @{}) {
    $arguments = @('-S', $ServerInstance, '-E', '-d', $database, '-b', '-I', '-f', 'i:65001,o:65001', '-i', $path)
    foreach ($entry in $variables.GetEnumerator()) { $arguments += @('-v', "$($entry.Key)=$($entry.Value)") }
    & $sqlCmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd that bai: $([IO.Path]::GetFileName($path))" }
}

$exactName = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'TTSmart_Control_V1_Test') IS NULL OR DB_NAME(DB_ID(N'TTSmart_Control_V1_Test')) = N'TTSmart_Control_V1_Test' THEN N'OK' ELSE N'INVALID' END;"
if ($LASTEXITCODE -ne 0 -or ($exactName | Out-String).Trim() -ne 'OK') { throw 'Could not verify the permitted test database name.' }

if ($Recreate) {
    $safeToRecreate = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; IF DB_ID(N'TTSmart_Control_V1_Test') IS NULL SELECT N'OK'; ELSE IF EXISTS(SELECT 1 FROM [TTSmart_Control_V1_Test].sys.dm_db_partition_stats p JOIN [TTSmart_Control_V1_Test].sys.tables t ON t.object_id=p.object_id WHERE p.index_id IN(0,1) AND p.row_count>0 AND t.is_ms_shipped=0 AND t.name NOT IN(N'SchemaVersions',N'DatabaseInfo')) SELECT N'BLOCKED'; ELSE SELECT N'OK';"
    if ($LASTEXITCODE -ne 0 -or ($safeToRecreate | Out-String).Trim() -ne 'OK') { throw 'Database test contains business data or objects; recreate stopped.' }
    & $sqlCmd -S $ServerInstance -E -d master -b -Q "IF DB_ID(N'TTSmart_Control_V1_Test') IS NOT NULL BEGIN ALTER DATABASE [TTSmart_Control_V1_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TTSmart_Control_V1_Test]; END;"
    if ($LASTEXITCODE -ne 0) { throw 'Could not recreate the ControlPlane test database.' }
}

$bootstrapStage=New-SqlServerV1StagedScript -File (Get-Item -LiteralPath (Join-Path $scriptRoot '000_CreateDatabase.sql'))
try { Invoke-ControlPlaneSql 'master' $bootstrapStage.Path }
finally { if(Test-Path -LiteralPath $bootstrapStage.TempRoot){Remove-Item -LiteralPath $bootstrapStage.TempRoot -Recurse -Force} }
foreach ($migration in $migrations) {
    $file = $migration.File
    $stage=New-SqlServerV1StagedScript -File $file
    try { Invoke-ControlPlaneSql $databaseName $stage.Path @{ ScriptChecksum = $stage.Checksum } }
    finally { if(Test-Path -LiteralPath $stage.TempRoot){Remove-Item -LiteralPath $stage.TempRoot -Recurse -Force} }
}
