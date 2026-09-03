[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ServerInstance,
    [ValidateSet('TTSmart')]
    [string] $CompanyDatabase = 'TTSmart',
    [ValidateSet('TTSmart_MAIN_online')]
    [string] $BranchDatabase = 'TTSmart_MAIN_online',
    [ValidateSet('ttsmart.com.vn')]
    [string] $ControlDatabase = 'ttsmart.com.vn',
    [Parameter(Mandatory)]
    [guid] $CompanyId,
    [Parameter(Mandatory)]
    [guid] $BranchId,
    [ValidatePattern('^[A-Za-z0-9]+$')]
    [string] $CompanyCode = 'TTSmart',
    [ValidatePattern('^[A-Za-z0-9]+$')]
    [string] $BranchCode = 'MAIN'
)

$ErrorActionPreference = 'Stop'
$sqlCmd = Get-Command sqlcmd -All -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandType -eq 'Application' -and (Get-Item -LiteralPath $_.Source).Length -gt 0 } |
    Sort-Object { if ($_.Source -like '*ODBC\170*') { 0 } else { 1 } } |
    Select-Object -First 1 -ExpandProperty Source
if (-not $sqlCmd) { throw 'Không tìm thấy sqlcmd.exe hợp lệ.' }

$root = $PSScriptRoot

function Invoke-SqlCmdChecked {
    param(
        [Parameter(Mandatory)][string] $Database,
        [Parameter(Mandatory)][string[]] $Arguments
    )

    & $sqlCmd -S $ServerInstance -E -C -b -I -f 65001 -d $Database @Arguments
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd thất bại trên database [$Database]." }
}

function Invoke-VersionedMigration {
    param(
        [Parameter(Mandatory)][string] $Database,
        [Parameter(Mandatory)][int] $MigrationNumber,
        [Parameter(Mandatory)][string] $ScriptName,
        [Parameter(Mandatory)][hashtable] $Variables
    )

    $script = Join-Path $root $ScriptName
    $checksum = (Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash
    $existing = (& $sqlCmd -S $ServerInstance -E -C -b -I -d $Database -h -1 -W -Q "SET NOCOUNT ON; SELECT ScriptChecksum FROM dbo.SchemaVersions WHERE MigrationNumber=$MigrationNumber;").Trim()
    if ($LASTEXITCODE -ne 0) { throw "Không đọc được SchemaVersions trên [$Database]." }
    if ($existing) {
        if ($existing -ne $checksum) { throw "Checksum drift migration $MigrationNumber trên [$Database]." }
        Write-Output "SKIP [$Database] migration ${MigrationNumber}: checksum khớp."
        return
    }

    $args = @('-i', $script, '-v', "ExpectedDatabaseName=$Database", "ScriptChecksum=$checksum")
    foreach ($entry in $Variables.GetEnumerator()) { $args += "$($entry.Key)=$($entry.Value)" }
    Invoke-SqlCmdChecked -Database $Database -Arguments $args
    Write-Output "APPLIED [$Database] migration $MigrationNumber."
}

$shared = @{ CompanyId = $CompanyId; CompanyCode = $CompanyCode }
$branch = @{ CompanyId = $CompanyId; BranchId = $BranchId; CompanyCode = $CompanyCode; BranchCode = $BranchCode }

Invoke-VersionedMigration -Database $BranchDatabase -MigrationNumber 10002 -ScriptName '002_PrepareBranchDatabase.sql' -Variables $branch
Invoke-VersionedMigration -Database $CompanyDatabase -MigrationNumber 10001 -ScriptName '001_PrepareCompanyDatabase.sql' -Variables $shared

Invoke-SqlCmdChecked -Database $ControlDatabase -Arguments @(
    '-i', (Join-Path $root '003_RegisterSingleCompanyBranch.sql'),
    '-v',
    "ExpectedDatabaseName=$ControlDatabase",
    "CompanyId=$CompanyId",
    "BranchId=$BranchId",
    "CompanyCode=$CompanyCode",
    "BranchCode=$BranchCode"
)

Invoke-VersionedMigration -Database $CompanyDatabase -MigrationNumber 10003 -ScriptName '004_RemoveDanglingLegacyRecordMappings.sql' -Variables @{}
Invoke-VersionedMigration -Database $BranchDatabase -MigrationNumber 10003 -ScriptName '004_RemoveDanglingLegacyRecordMappings.sql' -Variables @{}
