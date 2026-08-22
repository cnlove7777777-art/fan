[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'tests\Test-Release.ps1')
$stage = Join-Path $env:TEMP ('DellG15FanControl-' + [Guid]::NewGuid().ToString('N'))
$zip = Join-Path $root 'dist\DellG15FanControl.zip'
try {
    [IO.Directory]::CreateDirectory($stage) | Out-Null
    foreach ($name in @('DellG15FanControl.exe','SHA256SUMS.txt')) {
        [IO.File]::Copy((Join-Path $root ('dist\' + $name)), (Join-Path $stage $name), $false)
    }
    foreach ($name in @('README.md','README.zh-CN.md','LICENSE')) {
        [IO.File]::Copy((Join-Path $root $name), (Join-Path $stage $name), $false)
    }
    if (Test-Path -LiteralPath $zip) { [IO.File]::Delete($zip) }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
    $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    Write-Host "Package: $zip"
    Write-Host "SHA256: $zipHash"
}
finally {
    if (Test-Path -LiteralPath $stage) { [IO.Directory]::Delete($stage, $true) }
}
