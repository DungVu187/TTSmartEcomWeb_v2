[CmdletBinding()]
param([string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS', [switch] $Recreate)

$ErrorActionPreference = 'Stop'
$databaseName = 'TTSmart_Company_V1_Test'
$scriptRoot = $PSScriptRoot
. (Join-Path (Split-Path -Parent $scriptRoot) 'Resolve-MigrationLayout.ps1')
$migrations = @(Resolve-SqlServerV1MigrationLayout -MigrationRoot $scriptRoot)
$sqlCmd = Resolve-SqlServerV1SqlCmd

function Invoke-CompanySql([string] $database, [string] $path, [hashtable] $variables = @{}) {
    $arguments = @('-S',$ServerInstance,'-E','-d',$database,'-b','-I','-f','i:65001,o:65001','-i',$path)
    foreach ($entry in $variables.GetEnumerator()) { $arguments += @('-v', "$($entry.Key)=$($entry.Value)") }
    & $sqlCmd @arguments
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd that bai: $([IO.Path]::GetFileName($path))" }
}

$nameCheck = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'TTSmart_Company_V1_Test') IS NULL OR DB_NAME(DB_ID(N'TTSmart_Company_V1_Test'))=N'TTSmart_Company_V1_Test' THEN N'OK' ELSE N'INVALID' END;"
if ($LASTEXITCODE -ne 0 -or ($nameCheck | Out-String).Trim() -ne 'OK') { throw 'Khong xac minh duoc dung database test Company.' }
if ($Recreate) {
    $safe = & $sqlCmd -S $ServerInstance -E -d master -h -1 -W -Q "SET NOCOUNT ON; IF DB_ID(N'TTSmart_Company_V1_Test') IS NULL SELECT N'OK'; ELSE IF EXISTS(SELECT 1 FROM [TTSmart_Company_V1_Test].sys.dm_db_partition_stats p JOIN [TTSmart_Company_V1_Test].sys.tables t ON t.object_id=p.object_id WHERE p.index_id IN(0,1) AND p.row_count>0 AND t.is_ms_shipped=0 AND t.name NOT IN(N'SchemaVersions',N'DatabaseInfo')) SELECT N'BLOCKED'; ELSE SELECT N'OK';"
    if ($LASTEXITCODE -ne 0 -or ($safe | Out-String).Trim() -ne 'OK') { throw 'Database test co du lieu nghiep vu; dung recreate.' }
    & $sqlCmd -S $ServerInstance -E -d master -b -Q "IF DB_ID(N'TTSmart_Company_V1_Test') IS NOT NULL BEGIN ALTER DATABASE [TTSmart_Company_V1_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TTSmart_Company_V1_Test]; END;"
    if ($LASTEXITCODE -ne 0) { throw 'Khong the recreate Company test database.' }
}
$bootstrap = New-SqlServerV1StagedScript -File (Get-Item -LiteralPath (Join-Path $scriptRoot '000_CreateDatabase.sql'))
try { Invoke-CompanySql 'master' $bootstrap.Path } finally { if (Test-Path $bootstrap.TempRoot) { Remove-Item $bootstrap.TempRoot -Recurse -Force } }
foreach ($migration in $migrations) {
    $stage = New-SqlServerV1StagedScript -File $migration.File
    try { Invoke-CompanySql $databaseName $stage.Path @{ ScriptChecksum=$stage.Checksum } }
    finally { if (Test-Path $stage.TempRoot) { Remove-Item $stage.TempRoot -Recurse -Force } }
}
$metadata = "SET XACT_ABORT ON; BEGIN TRANSACTION; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'TTSmart.Company.V1.Metadata',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000; IF @r<0 THROW 59602,N'Khong lay duoc khoa metadata Company.',1; IF NOT EXISTS(SELECT 1 FROM dbo.DatabaseInfo WHERE SingletonKey=1) INSERT dbo.DatabaseInfo(DatabaseInfoId,SingletonKey,CompanyId,CompanyCode,DatabaseKind,SchemaVersion) VALUES(NEWID(),1,CONVERT(uniqueidentifier,'00000000-0000-0000-0000-000000000000'),N'TTSmartTest',N'Company',N'v1'); IF EXISTS(SELECT 1 FROM dbo.DatabaseInfo WHERE SingletonKey=1 AND (DatabaseKind<>N'Company' OR CompanyCode<>N'TTSmartTest' OR SchemaVersion<>N'v1')) THROW 59603,N'DatabaseInfo Company test khong nhat quan.',1; COMMIT;"
& $sqlCmd -S $ServerInstance -E -d $databaseName -b -Q $metadata
if ($LASTEXITCODE -ne 0) { throw 'Khong the khoi tao hoac xac minh DatabaseInfo Company.' }
