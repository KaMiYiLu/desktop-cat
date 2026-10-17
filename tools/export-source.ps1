param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$release = (Get-Content -LiteralPath (Join-Path $root 'VERSION') -Raw).Trim()
if ($release -notmatch '^\d+$') { throw 'Invalid VERSION' }
$files = @(Get-Content -LiteralPath (Join-Path $root 'SOURCE-FILES.txt') -Encoding UTF8 | Where-Object { $_.Trim() -and -not $_.StartsWith('#') })
if (($files | Sort-Object -Unique).Count -ne $files.Count) { throw 'Duplicate source manifest entry' }
$destination = Join-Path $root 'artifacts/source'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$name = "DesktopCat-source-candidate-v$release"
$zipPath = Join-Path $destination "$name.zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$validated = @()
foreach ($relative in $files) {
    if ($relative -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::IsPathRooted($relative)) { throw "Unsafe manifest entry: $relative" }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source entry escaped project root' }
    $file = Get-Item -LiteralPath $path
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Only regular source files may be exported' }
    if ($relative -match '(^|[\\/])(artifacts|\.git|\.openai|desktop-cat-promo)([\\/]|$)' -or $file.Extension -in @('.html','.css','.js','.exe','.pdb','.zip','.gz','.log')) { throw "Non-source entry rejected: $relative" }
    $validated += [pscustomobject]@{ File = $file; Relative = $relative.Replace('\','/') }
}
$stream = [IO.File]::Create($zipPath)
$archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($item in $validated) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $item.File.FullName, "$name/$($item.Relative)", [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose(); $stream.Dispose() }
"{0}  {1}" -f (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant(), "$name.zip" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii
Get-Item -LiteralPath $zipPath
