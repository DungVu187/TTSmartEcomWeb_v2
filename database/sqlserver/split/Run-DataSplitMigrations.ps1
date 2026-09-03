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

function Assert-ControlPlaneBranch {
    $query = @"
SET NOCOUNT ON;
SELECT CONVERT(nvarchar(36), b.BranchId)
FROM dbo.Branches b
INNER JOIN dbo.Companies c ON c.CompanyId=b.CompanyId
WHERE b.BranchId=CONVERT(uniqueidentifier,N'$BranchId')
  AND b.CompanyId=CONVERT(uniqueidentifier,N'$CompanyId')
  AND b.NormalizedBranchCode=UPPER(N'$BranchCode')
  AND c.NormalizedCompanyCode=UPPER(N'$CompanyCode')
  AND b.Status=1 AND b.IsDeleted=0 AND c.Status=1 AND c.IsDeleted=0;
"@
    $resolved = (& $sqlCmd -S $ServerInstance -E -C -b -I -d $ControlDatabase -h -1 -W -Q $query).Trim()
    if ($LASTEXITCODE -ne 0 -or $resolved -ne $BranchId.ToString()) {
        throw 'BranchId không khớp Company/Branch active trong Control Plane.'
    }
}

function Initialize-BranchVariantProjection {
    Add-Type -AssemblyName System.Data
    $companyBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $companyBuilder.DataSource = $ServerInstance
    $companyBuilder.InitialCatalog = $CompanyDatabase
    $companyBuilder.IntegratedSecurity = $true
    $companyBuilder.Encrypt = $true
    $companyBuilder.TrustServerCertificate = $true
    $branchBuilder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($companyBuilder.ConnectionString)
    $branchBuilder.InitialCatalog = $BranchDatabase

    $variants = [System.Collections.Generic.List[object]]::new()
    $companyConnection = [System.Data.SqlClient.SqlConnection]::new($companyBuilder.ConnectionString)
    try {
        $companyConnection.Open()
        $command = $companyConnection.CreateCommand()
        $command.CommandText = @"
SELECT p.ProductId,v.ProductVariantId,v.Price,v.PriceRaw,v.ImportPrice,v.ImportPriceRaw
FROM dbo.Products p
INNER JOIN dbo.ProductVariants v ON v.ProductId=p.ProductId
INNER JOIN dbo.ProductBranchAssignments a ON a.ProductId=p.ProductId
WHERE a.BranchId=@branchId AND a.IsActive=1 AND p.IsDeleted=0;
"@
        [void]$command.Parameters.Add('@branchId',[System.Data.SqlDbType]::UniqueIdentifier)
        $command.Parameters['@branchId'].Value = $BranchId
        $reader = $command.ExecuteReader()
        while ($reader.Read()) {
            $variants.Add([pscustomobject]@{
                ProductId = $reader.GetGuid(0)
                ProductVariantId = $reader.GetGuid(1)
                Price = if ($reader.IsDBNull(2)) { $null } else { $reader.GetDecimal(2) }
                PriceRaw = if ($reader.IsDBNull(3)) { $null } else { $reader.GetString(3) }
                ImportPrice = if ($reader.IsDBNull(4)) { $null } else { $reader.GetDecimal(4) }
                ImportPriceRaw = if ($reader.IsDBNull(5)) { $null } else { $reader.GetString(5) }
            })
        }
        $reader.Close()
    }
    finally { $companyConnection.Dispose() }

    $branchConnection = [System.Data.SqlClient.SqlConnection]::new($branchBuilder.ConnectionString)
    try {
        $branchConnection.Open()
        $transaction = $branchConnection.BeginTransaction()
        try {
            foreach ($variant in $variants) {
                $command = $branchConnection.CreateCommand()
                $command.Transaction = $transaction
                $command.CommandText = @"
UPDATE dbo.BranchProductVariants WITH (UPDLOCK,HOLDLOCK)
SET ProductId=@productId,Price=@price,PriceRaw=@priceRaw,ImportPrice=@importPrice,
    ImportPriceRaw=@importPriceRaw,IsActive=1,UpdatedAtUtc=SYSUTCDATETIME()
WHERE ProductVariantId=@variantId;
IF @@ROWCOUNT=0
    INSERT dbo.BranchProductVariants
        (BranchProductVariantId,ProductId,ProductVariantId,Price,PriceRaw,ImportPrice,ImportPriceRaw,IsActive)
    VALUES (NEWID(),@productId,@variantId,@price,@priceRaw,@importPrice,@importPriceRaw,1);
"@
                [void]$command.Parameters.Add('@productId',[System.Data.SqlDbType]::UniqueIdentifier)
                [void]$command.Parameters.Add('@variantId',[System.Data.SqlDbType]::UniqueIdentifier)
                [void]$command.Parameters.Add('@price',[System.Data.SqlDbType]::Decimal)
                [void]$command.Parameters.Add('@priceRaw',[System.Data.SqlDbType]::NVarChar,100)
                [void]$command.Parameters.Add('@importPrice',[System.Data.SqlDbType]::Decimal)
                [void]$command.Parameters.Add('@importPriceRaw',[System.Data.SqlDbType]::NVarChar,100)
                $command.Parameters['@price'].Precision = 19; $command.Parameters['@price'].Scale = 4
                $command.Parameters['@importPrice'].Precision = 19; $command.Parameters['@importPrice'].Scale = 4
                $command.Parameters['@productId'].Value = $variant.ProductId
                $command.Parameters['@variantId'].Value = $variant.ProductVariantId
                $command.Parameters['@price'].Value = if ($null -eq $variant.Price) { [DBNull]::Value } else { $variant.Price }
                $command.Parameters['@priceRaw'].Value = if ($null -eq $variant.PriceRaw) { [DBNull]::Value } else { $variant.PriceRaw }
                $command.Parameters['@importPrice'].Value = if ($null -eq $variant.ImportPrice) { [DBNull]::Value } else { $variant.ImportPrice }
                $command.Parameters['@importPriceRaw'].Value = if ($null -eq $variant.ImportPriceRaw) { [DBNull]::Value } else { $variant.ImportPriceRaw }
                [void]$command.ExecuteNonQuery()
            }
            $transaction.Commit()
        }
        catch { $transaction.Rollback(); throw }
    }
    finally { $branchConnection.Dispose() }
}

$shared = @{ CompanyId = $CompanyId; CompanyCode = $CompanyCode }
$branch = @{ CompanyId = $CompanyId; BranchId = $BranchId; CompanyCode = $CompanyCode; BranchCode = $BranchCode }

Assert-ControlPlaneBranch

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
Invoke-VersionedMigration -Database $CompanyDatabase -MigrationNumber 10004 -ScriptName '005_CreateProductBranchAssignments.sql' -Variables $branch
Invoke-VersionedMigration -Database $BranchDatabase -MigrationNumber 10005 -ScriptName '006_CreateBranchProductProjection.sql' -Variables $branch
Initialize-BranchVariantProjection
