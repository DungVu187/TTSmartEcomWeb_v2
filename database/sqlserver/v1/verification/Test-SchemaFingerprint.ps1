[CmdletBinding()]
param(
    [string] $ServerInstance = 'DESKTOP-5O6VV3J\SQLEXPRESS',
    [ValidateSet('TTSmart_Control_V1_Test', 'TTSmart_Operational_V1_Test')]
    [string] $DatabaseName
)

$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path -Parent $PSScriptRoot) 'Resolve-MigrationLayout.ps1')
$sqlCmd = Resolve-SqlServerV1SqlCmd
if ([string]::IsNullOrWhiteSpace($DatabaseName)) { throw 'Phai chi ro dung ten database test duoc cap phep.' }

$scriptRoot = $PSScriptRoot
$fingerprintScript = Join-Path $scriptRoot 'Get-SchemaFingerprint.ps1'
$temporaryRole = 'FingerprintMutationRole'
$temporaryUser = 'FingerprintMutationUser'
$temporaryProcedure = 'FingerprintMutationProcedure'
$temporaryIndex = 'IX_FingerprintMutation'
$temporaryConstraint = 'CK_FingerprintMutation'

function Invoke-TestSql {
    param([Parameter(Mandatory)][string] $Query, [string] $Database = $DatabaseName)
    $output = & $sqlCmd -S $ServerInstance -E -d $Database -b -I -h -1 -W -Q $Query
    if ($LASTEXITCODE -ne 0) { throw "Lenh SQL kiem thu that bai: $(($output | Out-String).Trim())" }
    return $output
}

function Get-Fingerprint { & $fingerprintScript -ServerInstance $ServerInstance -DatabaseName $DatabaseName }
function Assert-FingerprintChanged([string] $Golden, [string] $CaseName) {
    if ($Golden -eq (Get-Fingerprint)) { throw "Fingerprint khong phat hien thay doi $CaseName." }
}

$golden = Get-Fingerprint
$triggerParts = $null
$autoShrinkWasOn = $null
$queryCaptureMode = $null

try {
    $trigger = Invoke-TestSql "SET NOCOUNT ON; SELECT TOP(1) SCHEMA_NAME(t.schema_id)+N'|'+t.name+N'|'+tr.name FROM sys.triggers AS tr JOIN sys.tables AS t ON t.object_id=tr.parent_id WHERE tr.is_ms_shipped=0 ORDER BY tr.name;"
    $triggerParts = (($trigger | Out-String).Trim() -split '\|')
    if ($triggerParts.Count -ne 3 -or @($triggerParts | Where-Object { $_ -notmatch '^[A-Za-z0-9_]+$' }).Count -ne 0) {
        throw 'Khong tim thay trigger nguoi dung an toan de kiem thu fingerprint.'
    }
    $triggerSchema,$triggerTable,$triggerName = $triggerParts
    Invoke-TestSql "DISABLE TRIGGER [$triggerName] ON [$triggerSchema].[$triggerTable];" | Out-Null
    Assert-FingerprintChanged $golden 'trang thai trigger'
    Invoke-TestSql "ENABLE TRIGGER [$triggerName] ON [$triggerSchema].[$triggerTable];" | Out-Null
    $triggerParts = $null
    if ($golden -ne (Get-Fingerprint)) { throw 'Khong khoi phuc duoc fingerprint sau khi bat lai trigger.' }

    $target = Invoke-TestSql "SET NOCOUNT ON; SELECT TOP(1) SCHEMA_NAME(t.schema_id)+N'|'+t.name+N'|'+c.name FROM sys.tables AS t JOIN sys.columns AS c ON c.object_id=t.object_id WHERE t.is_ms_shipped=0 AND c.is_computed=0 AND c.system_type_id NOT IN(34,35,99,165,167,173,189,231,241) ORDER BY t.name,c.column_id;"
    $targetParts = (($target | Out-String).Trim() -split '\|')
    if ($targetParts.Count -ne 3 -or @($targetParts | Where-Object { $_ -notmatch '^[A-Za-z0-9_]+$' }).Count -ne 0) {
        throw 'Khong tim thay cot nguoi dung an toan de kiem thu fingerprint.'
    }
    $targetSchema,$targetTable,$targetColumn = $targetParts

    Invoke-TestSql "CREATE ROLE [$temporaryRole] AUTHORIZATION dbo; CREATE USER [$temporaryUser] WITHOUT LOGIN;" | Out-Null
    Assert-FingerprintChanged $golden 'role va user baseline'
    Invoke-TestSql "ALTER ROLE [$temporaryRole] ADD MEMBER [$temporaryUser];" | Out-Null
    Assert-FingerprintChanged $golden 'role membership'
    Invoke-TestSql "GRANT SELECT ON [$targetSchema].[$targetTable] TO [$temporaryRole];" | Out-Null
    Assert-FingerprintChanged $golden 'permission'

    Invoke-TestSql "CREATE OR ALTER PROCEDURE dbo.[$temporaryProcedure] AS SELECT 1 AS Marker;" | Out-Null
    $afterCreateProcedure = Get-Fingerprint
    if ($golden -eq $afterCreateProcedure) { throw 'Fingerprint khong phat hien module procedure moi.' }
    Invoke-TestSql "CREATE OR ALTER PROCEDURE dbo.[$temporaryProcedure] AS SELECT 2 AS Marker;" | Out-Null
    if ($afterCreateProcedure -eq (Get-Fingerprint)) { throw 'Fingerprint khong phat hien sua definition procedure.' }

    Invoke-TestSql "CREATE INDEX [$temporaryIndex] ON [$targetSchema].[$targetTable]([$targetColumn]);" | Out-Null
    Assert-FingerprintChanged $golden 'index'
    Invoke-TestSql "ALTER TABLE [$targetSchema].[$targetTable] ADD CONSTRAINT [$temporaryConstraint] CHECK (1=1);" | Out-Null
    Assert-FingerprintChanged $golden 'constraint'

    $autoShrinkWasOn = ((Invoke-TestSql "SET NOCOUNT ON; SELECT CONVERT(int,is_auto_shrink_on) FROM sys.databases WHERE database_id=DB_ID();" | Out-String).Trim() -eq '1')
    $nextAutoShrink = if ($autoShrinkWasOn) { 'OFF' } else { 'ON' }
    Invoke-TestSql "ALTER DATABASE [$DatabaseName] SET AUTO_SHRINK $nextAutoShrink;" 'master' | Out-Null
    Assert-FingerprintChanged $golden 'database option'
    $restoreAutoShrink = if ($autoShrinkWasOn) { 'ON' } else { 'OFF' }
    Invoke-TestSql "ALTER DATABASE [$DatabaseName] SET AUTO_SHRINK $restoreAutoShrink;" 'master' | Out-Null
    $autoShrinkWasOn = $null

    if ($DatabaseName -eq 'TTSmart_Control_V1_Test') {
        $queryCaptureMode = (Invoke-TestSql "SET NOCOUNT ON; SELECT query_capture_mode_desc FROM sys.database_query_store_options;" | Out-String).Trim()
        if ($queryCaptureMode -notin @('ALL','AUTO','NONE','CUSTOM')) { throw 'Khong doc duoc Query Store capture mode de kiem thu.' }
        Invoke-TestSql "ALTER DATABASE [$DatabaseName] SET QUERY_STORE (QUERY_CAPTURE_MODE = NONE);" 'master' | Out-Null
        Assert-FingerprintChanged $golden 'Query Store'
        Invoke-TestSql "ALTER DATABASE [$DatabaseName] SET QUERY_STORE (QUERY_CAPTURE_MODE = $queryCaptureMode);" 'master' | Out-Null
        $queryCaptureMode = $null
    }
}
finally {
    if ($triggerParts) {
        try { Invoke-TestSql "ENABLE TRIGGER [$($triggerParts[2])] ON [$($triggerParts[0])].[$($triggerParts[1])];" | Out-Null } catch { Write-Warning $_ }
    }
    if ($null -ne $autoShrinkWasOn) {
        try { Invoke-TestSql "ALTER DATABASE [$DatabaseName] SET AUTO_SHRINK $(if ($autoShrinkWasOn) { 'ON' } else { 'OFF' });" 'master' | Out-Null } catch { Write-Warning $_ }
    }
    if ($queryCaptureMode) {
        try { Invoke-TestSql "ALTER DATABASE [$DatabaseName] SET QUERY_STORE (QUERY_CAPTURE_MODE = $queryCaptureMode);" 'master' | Out-Null } catch { Write-Warning $_ }
    }
    try { Invoke-TestSql "IF OBJECT_ID(N'dbo.$temporaryProcedure',N'P') IS NOT NULL DROP PROCEDURE dbo.[$temporaryProcedure]; IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'[$targetSchema].[$targetTable]') AND name=N'$temporaryIndex') DROP INDEX [$temporaryIndex] ON [$targetSchema].[$targetTable]; IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'[$targetSchema].[$targetTable]') AND name=N'$temporaryConstraint') ALTER TABLE [$targetSchema].[$targetTable] DROP CONSTRAINT [$temporaryConstraint]; IF EXISTS(SELECT 1 FROM sys.database_role_members WHERE role_principal_id=DATABASE_PRINCIPAL_ID(N'$temporaryRole') AND member_principal_id=DATABASE_PRINCIPAL_ID(N'$temporaryUser')) ALTER ROLE [$temporaryRole] DROP MEMBER [$temporaryUser]; IF DATABASE_PRINCIPAL_ID(N'$temporaryUser') IS NOT NULL DROP USER [$temporaryUser]; IF DATABASE_PRINCIPAL_ID(N'$temporaryRole') IS NOT NULL DROP ROLE [$temporaryRole];" | Out-Null } catch { Write-Warning $_ }
}

$after = Get-Fingerprint
if ($golden -ne $after) { throw 'Fingerprint golden khong tro lai sau khi don mutation test.' }
Write-Output "Fingerprint golden va mutation test passed for $DatabaseName"
