Set-StrictMode -Version Latest

function Resolve-SqlServerV1MigrationLayout {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $MigrationRoot
    )

    if (-not (Test-Path -LiteralPath $MigrationRoot -PathType Container)) {
        throw "Khong tim thay thu muc migration: $MigrationRoot"
    }
    $root = Get-Item -LiteralPath $MigrationRoot -Force
    if (($root.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Thu muc migration khong duoc la reparse point.' }
    $canonicalRoot = [IO.Path]::GetFullPath($root.FullName).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
    $nestedSql = @(Get-ChildItem -LiteralPath $canonicalRoot -Recurse -File -Filter '*.sql' | Where-Object { $_.DirectoryName -ne $canonicalRoot })
    if ($nestedSql.Count -gt 0) { throw "Khong cho phep SQL trong thu muc con: $($nestedSql.FullName -join ', ')" }

    $sqlFiles = @(Get-ChildItem -LiteralPath $MigrationRoot -File -Filter '*.sql')
    $suspiciousSql = @(Get-ChildItem -LiteralPath $MigrationRoot -File | Where-Object { $_.Name -match '\.sql\.' })
    if ($suspiciousSql.Count -gt 0) { throw "Phat hien file SQL dang ngo: $($suspiciousSql.Name -join ', ')" }
    foreach ($sqlFile in $sqlFiles) {
        if (($sqlFile.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Migration khong duoc la reparse point: $($sqlFile.Name)" }
        if (-not ([IO.Path]::GetFullPath($sqlFile.FullName).StartsWith($canonicalRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))) { throw "Migration nam ngoai root: $($sqlFile.FullName)" }
    }
    $bootstrapFiles = @($sqlFiles | Where-Object { $_.Name -ceq '000_CreateDatabase.sql' })
    if ($bootstrapFiles.Count -ne 1) {
        throw "Thu muc migration phai co dung mot 000_CreateDatabase.sql; hien co $($bootstrapFiles.Count)."
    }

    $invalidNames = @($sqlFiles | Where-Object { $_.Name -cne '000_CreateDatabase.sql' -and $_.Name -cnotmatch '^[0-9]{3}_.+\.sql$' })
    if ($invalidNames.Count -gt 0) {
        throw "Ten file migration sai dinh dang: $($invalidNames.Name -join ', ')"
    }

    $migrations = @(
        $sqlFiles |
            Where-Object { $_.Name -cne '000_CreateDatabase.sql' } |
            ForEach-Object {
                [pscustomobject]@{
                    Number = [int]$_.BaseName.Substring(0, 3)
                    File = $_
                }
            }
    )

    if ($migrations.Count -eq 0) { throw 'Khong tim thay migration nao bat dau tu 001.' }
    $duplicateNumbers = @($migrations | Group-Object Number | Where-Object Count -ne 1)
    if ($duplicateNumbers.Count -gt 0) {
        throw "Moi so migration phai co dung mot file; trung so: $(($duplicateNumbers.Name) -join ', ')."
    }

    $ordered = @($migrations | Sort-Object Number)
    if ($ordered[0].Number -ne 1) { throw 'Migration phai bat dau tu 001.' }
    for ($index = 0; $index -lt $ordered.Count; $index++) {
        $expected = $index + 1
        if ($ordered[$index].Number -ne $expected) {
            throw "Day migration khong lien tuc; can $('{0:D3}' -f $expected) nhung tim thay $('{0:D3}' -f $ordered[$index].Number)."
        }
    }

    return $ordered
}

function New-SqlServerV1StagedScript {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.IO.FileInfo] $File)

    $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $checksum = ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '') }
    finally { $sha.Dispose() }
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('TTSmartSqlV1_' + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($tempRoot) | Out-Null
    $stagedPath = Join-Path $tempRoot $File.Name
    [System.IO.File]::WriteAllBytes($stagedPath,$bytes)
    [pscustomobject]@{ Path=$stagedPath; Checksum=$checksum; TempRoot=$tempRoot }
}

function Resolve-SqlServerV1SqlCmd {
    [CmdletBinding()]
    param()

    if ($env:SQLCMD_PATH -and (Test-Path -LiteralPath $env:SQLCMD_PATH -PathType Leaf) -and (Get-Item -LiteralPath $env:SQLCMD_PATH).Length -gt 0) { return $env:SQLCMD_PATH }
    $command = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if ($command -and $command.CommandType -eq 'Application' -and (Test-Path -LiteralPath $command.Source) -and (Get-Item -LiteralPath $command.Source).Length -gt 0) { return $command.Source }
    $candidate = Get-ChildItem -LiteralPath $env:ProgramFiles -Recurse -Filter SQLCMD.EXE -ErrorAction SilentlyContinue | Where-Object Length -gt 0 | Select-Object -First 1
    if ($candidate) { return $candidate.FullName }
    throw 'Khong tim thay sqlcmd executable hop le. Dat bien moi truong SQLCMD_PATH neu can.'
}
