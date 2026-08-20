[CmdletBinding()]
param(
    [string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS',
    [switch] $Recreate
)

$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0
$databaseName = 'TTSmart_Operational_V1_Test'
$scriptRoot = $PSScriptRoot
. (Join-Path (Split-Path -Parent $scriptRoot) 'Resolve-MigrationLayout.ps1')
$migrations = @(Resolve-SqlServerV1MigrationLayout -MigrationRoot $scriptRoot)
$sqlCmd = Resolve-SqlServerV1SqlCmd

function Invoke-OperationalSql([string] $database, [string] $path, [hashtable] $variables = @{}) {
    $arguments = @('-S', $ServerInstance, '-E', '-d', $database, '-b', '-I', '-f', 'i:65001,o:65001', '-i', $path)
    foreach ($entry in $variables.GetEnumerator()) { $arguments += @('-v', "$($entry.Key)=$($entry.Value)") }
    & $sqlCmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed: $([IO.Path]::GetFileName($path))" }
}

$nameCheck = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'TTSmart_Operational_V1_Test') IS NULL OR DB_NAME(DB_ID(N'TTSmart_Operational_V1_Test')) = N'TTSmart_Operational_V1_Test' THEN N'OK' ELSE N'INVALID' END;"
if ($LASTEXITCODE -ne 0 -or ($nameCheck | Out-String).Trim() -ne 'OK') { throw 'Could not verify the permitted test database name.' }

if ($Recreate) {
    $safeToRecreate = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; IF DB_ID(N'TTSmart_Operational_V1_Test') IS NULL SELECT N'OK'; ELSE IF EXISTS(SELECT 1 FROM [TTSmart_Operational_V1_Test].sys.dm_db_partition_stats p JOIN [TTSmart_Operational_V1_Test].sys.tables t ON t.object_id=p.object_id WHERE p.index_id IN(0,1) AND p.row_count>0 AND t.is_ms_shipped=0 AND t.name NOT IN(N'SchemaVersions',N'DatabaseInfo')) SELECT N'BLOCKED'; ELSE SELECT N'OK';"
    if ($LASTEXITCODE -ne 0 -or ($safeToRecreate | Out-String).Trim() -ne 'OK') { throw 'Database test có dữ liệu hoặc object nghiệp vụ; dừng recreate.' }
    & $sqlCmd -S $ServerInstance -E -d master -b -Q "IF DB_ID(N'TTSmart_Operational_V1_Test') IS NOT NULL BEGIN ALTER DATABASE [TTSmart_Operational_V1_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TTSmart_Operational_V1_Test]; END;"
    if ($LASTEXITCODE -ne 0) { throw 'Could not recreate the Operational test database.' }
}

$bootstrapStage=New-SqlServerV1StagedScript -File (Get-Item -LiteralPath (Join-Path $scriptRoot '000_CreateDatabase.sql'))
try { Invoke-OperationalSql 'master' $bootstrapStage.Path }
finally { if(Test-Path -LiteralPath $bootstrapStage.TempRoot){Remove-Item -LiteralPath $bootstrapStage.TempRoot -Recurse -Force} }
foreach ($migration in $migrations) {
    $file = $migration.File
    $stage=New-SqlServerV1StagedScript -File $file
    try { Invoke-OperationalSql $databaseName $stage.Path @{ ScriptChecksum = $stage.Checksum } }
    finally { if(Test-Path -LiteralPath $stage.TempRoot){Remove-Item -LiteralPath $stage.TempRoot -Recurse -Force} }
    if ($migration.Number -eq 1) {
        $bootstrap = "SET XACT_ABORT ON; DECLARE @r int; BEGIN TRY BEGIN TRANSACTION; EXEC @r=sys.sp_getapplock @Resource=N'TTSmart.Operational.V1.Bootstrap',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000; IF @r<0 THROW 59401,N'Không lấy được khóa bootstrap Operational.',1; IF EXISTS(SELECT 1 FROM dbo.DatabaseInfo WHERE SingletonKey=1 AND (DatabaseKind<>N'TTSmart' OR CompanyId<>CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000000') OR BranchId IS NOT NULL OR DatabaseCode<>N'TTSmart_Operational_V1_Test' OR StorageNamespace<>N'v1-test' OR OperationalRelease<>N'v1')) THROW 59400,N'Operational test DatabaseInfo không nhất quán.',1; IF NOT EXISTS (SELECT 1 FROM dbo.DatabaseInfo WHERE SingletonKey=1) INSERT dbo.DatabaseInfo(DatabaseInfoId,SingletonKey,DatabaseKind,CompanyId,BranchId,DatabaseCode,StorageNamespace,OperationalRelease) VALUES(NEWID(),1,N'TTSmart',CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000000'),NULL,N'TTSmart_Operational_V1_Test',N'v1-test',N'v1'); COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;"
        & $sqlCmd -S $ServerInstance -E -d $databaseName -b -Q $bootstrap
        if ($LASTEXITCODE -ne 0) { throw 'Không thể khởi tạo hoặc xác minh DatabaseInfo Operational.' }
    }
}
