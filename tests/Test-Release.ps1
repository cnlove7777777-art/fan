[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $root 'build.ps1')
$exe = Join-Path $root 'dist\DellG15FanControl.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'EXE was not built.' }
if ((Get-Item -LiteralPath $exe).Length -lt 20000) { throw 'EXE is unexpectedly small.' }

$source = [IO.File]::ReadAllText((Join-Path $root 'src\Firmware.cs'))
$required = @('0xFEA3','0x00A3','0x01A3','0x02A3','0x000004A3','0x10A3','0x000011A3',
    '0x44494147','0x44454C4C','Dell G15 5515','1.30.0')
foreach ($needle in $required) {
    if (-not $source.Contains($needle)) { throw "Required protocol constant missing: $needle" }
}
if ($source -match 'WinRing|PawnIO|WriteProcessMemory|CreateFile\(') {
    throw 'An unexpected raw-driver or process-memory API appeared.'
}
$uiSource = [IO.File]::ReadAllText((Join-Path $root 'src\MainForm.cs'))
foreach ($needle in @('FanState.Off','FanState.Low','FanState.High','int limit = emergencyThreshold','value.CpuC >= limit','value.GpuC.Value >= limit')) {
    if (-not $uiSource.Contains($needle)) { throw "Required compact-controller behavior missing: $needle" }
}
if ($uiSource -notmatch 'value\.CpuC >= limit && value\.GpuC\.Value >= limit') {
    throw 'Thermal override must require CPU and GPU to reach the cached threshold together.'
}
$startupSource = [IO.File]::ReadAllText((Join-Path $root 'src\StartupTask.cs'))
$programSource = [IO.File]::ReadAllText((Join-Path $root 'src\Program.cs'))
foreach ($needle in @('e.CloseReason == CloseReason.UserClosing','HideToTray();','RequestExit()','StartupTask.VerifyExact')) {
    if (-not $uiSource.Contains($needle)) { throw "Tray/startup behavior missing: $needle" }
}
if (-not $startupSource.Contains('--startup')) { throw 'Startup argument is missing.' }
foreach ($needle in @('--enable-startup','--disable-startup')) {
    if (-not $programSource.Contains($needle)) { throw "Startup integration command missing: $needle" }
}
foreach ($needle in @('HighestAvailable','LogonTrigger','Application.ExecutablePath','startup argument does not match','EXE path does not match')) {
    if (-not $startupSource.Contains($needle)) { throw "Startup verification contract missing: $needle" }
}

$manifest = [xml][IO.File]::ReadAllText((Join-Path $root 'app.manifest'))
$level = $manifest.assembly.trustInfo.security.requestedPrivileges.requestedExecutionLevel.level
if ($level -ne 'requireAdministrator') { throw 'The release manifest is not requireAdministrator.' }

$hashLine = [IO.File]::ReadAllText((Join-Path $root 'dist\SHA256SUMS.txt')).Trim()
$actual = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if (-not $hashLine.StartsWith($actual, [StringComparison]::OrdinalIgnoreCase)) { throw 'SHA256 manifest mismatch.' }

$compiler = Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$smoke = Join-Path ([IO.Path]::GetTempPath()) ('DellG15FanUiSmoke-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /platform:x64 /out:"$smoke" /reference:System.dll /reference:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'UiSmoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'UI smoke harness compilation failed.' }
    & $smoke $exe
    if ($LASTEXITCODE -ne 0) { throw "UI smoke test failed with exit code $LASTEXITCODE" }
}
finally {
    if (Test-Path -LiteralPath $smoke) { [IO.File]::Delete($smoke) }
}

Write-Host 'Release checks: PASS'
Write-Host "SHA256: $actual"
