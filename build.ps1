[CmdletBinding()]
param([string]$Configuration = 'Release')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'dist'
$compiler = Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw "C# compiler not found: $compiler" }
if (-not (Test-Path -LiteralPath $out)) { [IO.Directory]::CreateDirectory($out) | Out-Null }
$target = Join-Path $out 'DellG15FanControl.exe'
$sources = [IO.Directory]::GetFiles((Join-Path $root 'src'), '*.cs')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /win32manifest:"$(Join-Path $root 'app.manifest')" /out:"$target" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Windows.Forms.dll $sources
if ($LASTEXITCODE -ne 0) { throw "csc.exe failed with exit code $LASTEXITCODE" }
$hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Value "$hash  DellG15FanControl.exe" -Encoding ascii
Write-Host "Built $target"
Write-Host "SHA256 $hash"
