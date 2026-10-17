param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$release = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($release -notmatch '^\d+$' -or [int]$release -gt 65535) { throw 'Invalid VERSION' }
$exe = Join-Path $root "artifacts/bin/DesktopCat-v$release.exe"
if (-not (Test-Path -LiteralPath $exe)) { throw 'Run build.ps1 before packaging.' }
if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne "0.$release.0.0") { throw 'Executable version does not match VERSION; rebuild first.' }
$name = "DesktopCat-v$release-win"
$releaseRoot = Join-Path $root 'artifacts/release'
$stage = Join-Path $releaseRoot $name
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'DesktopCat.exe') -Force
Copy-Item -LiteralPath (Join-Path $root 'config.example.ini') -Destination (Join-Path $stage 'config.ini') -Force
$guide = (Get-Content -LiteralPath (Join-Path $root 'docs/USER-GUIDE.txt') -Raw -Encoding UTF8).Replace('{{VERSION}}', $release)
$notices = Get-Content -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Raw -Encoding UTF8
[IO.File]::WriteAllText((Join-Path $stage '使用说明.txt'), $guide + "`r`n`r`n" + $notices, (New-Object Text.UTF8Encoding($true)))
$zipPath = Join-Path $releaseRoot "$name.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$stream = [IO.File]::Create($zipPath)
$archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($filename in @('DesktopCat.exe', 'config.ini', '使用说明.txt')) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $stage $filename), "$name/$filename", [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose(); $stream.Dispose() }
"{0}  {1}" -f (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant(), "$name.zip" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii
Get-Item -LiteralPath $zipPath
