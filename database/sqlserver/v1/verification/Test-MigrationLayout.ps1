[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$resolver = Join-Path (Split-Path -Parent $PSScriptRoot) 'Resolve-MigrationLayout.ps1'
. $resolver
$root = Join-Path ([IO.Path]::GetTempPath()) ("TTSmart-SqlServerV1-Layout-" + [guid]::NewGuid().ToString('N'))

function New-Layout([string[]] $Names) {
    $directory = Join-Path $root ([guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $directory)
    foreach ($name in $Names) { [IO.File]::WriteAllText((Join-Path $directory $name), "-- synthetic test only`n", [Text.UTF8Encoding]::new($false)) }
    $directory
}

function Assert-LayoutFails([string[]] $Names, [string] $ExpectedText) {
    $directory = New-Layout $Names
    try {
        $thrown = $null
        try { Resolve-SqlServerV1MigrationLayout -MigrationRoot $directory | Out-Null } catch { $thrown = $_ }
        if ($null -eq $thrown -or $thrown.Exception.Message -notlike "*$ExpectedText*") {
            throw "Layout am tinh khong dung loi mong doi '$ExpectedText'."
        }
    }
    finally { Remove-Item -LiteralPath $directory -Recurse -Force }
}

try {
    $valid = New-Layout @('000_CreateDatabase.sql','001_One.sql','002_Two.sql')
    try {
        $resolved = @(Resolve-SqlServerV1MigrationLayout -MigrationRoot $valid)
        if ($resolved.Count -ne 2 -or $resolved[0].Number -ne 1 -or $resolved[1].Number -ne 2) { throw 'Layout hop le khong duoc sap xep dung.' }
    }
    finally { Remove-Item -LiteralPath $valid -Recurse -Force }

    Assert-LayoutFails @('001_One.sql') '000_CreateDatabase.sql'
    Assert-LayoutFails @('000_CreateDatabase.sql','001_One.sql','001_Another.sql') 'trung so'
    Assert-LayoutFails @('000_CreateDatabase.sql','001_One.sql','003_Three.sql') 'khong lien tuc'
    Assert-LayoutFails @('000_CreateDatabase.sql','002_Two.sql') 'bat dau tu 001'
    Assert-LayoutFails @('000_CreateDatabase.sql','001_One.sql','notes.sql') 'sai dinh dang'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}

Write-Output 'Migration layout test passed.'
