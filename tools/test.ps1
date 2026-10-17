param([switch]$Desktop, [switch]$Audio)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$release = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
$executable = Join-Path $root "artifacts/bin/DesktopCat-v$release.exe"
if (-not (Test-Path -LiteralPath $executable)) { throw 'Run build.ps1 first.' }
$results = Join-Path $root 'artifacts/checks'
New-Item -ItemType Directory -Path $results -Force | Out-Null
function Invoke-Check([string]$Mode, [string]$ResultPath) {
    $process = Start-Process -FilePath $executable -ArgumentList @($Mode, ('"' + $ResultPath + '"')) -WorkingDirectory $results -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "Check timed out: $Mode" }
    $process.Refresh()
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $ResultPath)) { throw "Check failed: $Mode" }
    $report = Get-Content -LiteralPath $ResultPath -Raw
    if (-not $report.StartsWith('PASS')) { throw "Check failed: $Mode" }
    Write-Output $report
}
foreach ($mode in @('--verify', '--verify-lick', '--verify-idle')) {
    Invoke-Check $mode (Join-Path $results ($mode.TrimStart('-') + '.txt'))
}
if ($Desktop) {
    foreach ($mode in @('--debug-check', '--smoke-actions')) {
        Invoke-Check $mode (Join-Path $results ($mode.TrimStart('-') + '.txt'))
    }
    # Check new process launches, failure cleanup, and absence of automatic logs.
    $sandbox = Join-Path $results ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $sandbox | Out-Null
    $standalone = Join-Path $sandbox 'DesktopCat.exe'
    Copy-Item -LiteralPath $executable -Destination $standalone
    foreach ($mode in @('--smoke', '--diagnostic-crash-test', '--diagnostic-paint-test')) {
        $report = Join-Path $results 'standalone.txt'
        $arguments = if ($mode -eq '--diagnostic-crash-test') { @($mode) } else { @($mode, ('"' + $report + '"')) }
        $process = Start-Process -FilePath $standalone -ArgumentList $arguments -WorkingDirectory $sandbox -WindowStyle Hidden -PassThru
        if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Standalone test timed out' }
        $process.Refresh()
        $expected = if ($mode -eq '--smoke') { 0 } else { 1 }
        if ($process.ExitCode -ne $expected) { throw "Unexpected standalone exit: $mode" }
        if (@(Get-ChildItem -LiteralPath $sandbox -Force).Count -ne 1) { throw 'Unexpected automatic files or directories' }
    }
    'PASS standalone startup and failure cleanup / no automatic logs'
}
if ($Audio) { Invoke-Check '--audio-check' (Join-Path $results 'audio.txt') }
& (Join-Path $PSScriptRoot 'package-release.ps1') | Out-Null
$zipPath = Join-Path $root "artifacts/release/DesktopCat-v$release-win.zip"
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $names = @($archive.Entries | ForEach-Object { $_.FullName.Split('/')[-1] } | Sort-Object)
    if (($names -join '|') -ne ((@('DesktopCat.exe', 'config.ini', '使用说明.txt') | Sort-Object) -join '|')) { throw 'Release must contain exactly the three user files' }
}
finally { $archive.Dispose() }
$expectedHash = (Get-Content -LiteralPath "$zipPath.sha256").Split(' ')[0]
if ((Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expectedHash) { throw 'Release SHA-256 mismatch' }
'PASS three-file release / SHA-256 / matching executable version'
