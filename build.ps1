param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts/bin'))
$ErrorActionPreference = 'Stop'
$release = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($release -notmatch '^\d+$' -or [int]$release -gt 65535) { throw 'VERSION must be a number between 0 and 65535' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework 4.x C# compiler is required.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$objects = Join-Path $PSScriptRoot 'artifacts/obj'
New-Item -ItemType Directory -Path $objects -Force | Out-Null
$versionSource = Join-Path $objects 'VersionInfo.cs'
@"
using System.Reflection;
[assembly: AssemblyVersion("0.$release.0.0")]
[assembly: AssemblyFileVersion("0.$release.0.0")]
"@ | Set-Content -LiteralPath $versionSource -Encoding utf8
$executable = Join-Path $output "DesktopCat-v$release.exe"
$arguments = @(
    '/nologo', '/target:winexe', '/optimize+', '/debug:pdbonly', "/out:$executable",
    '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll',
    "/win32icon:$PSScriptRoot/Assets/Branding/DesktopCat.ico",
    "/resource:$PSScriptRoot/Assets/Branding/DesktopCat.ico,DesktopCat.ico",
    "/resource:$PSScriptRoot/Assets/Sounds/meow.wav,DesktopCat.meow.wav",
    "/resource:$PSScriptRoot/Assets/Sounds/purr.wav,DesktopCat.purr.wav",
    "/resource:$PSScriptRoot/Assets/Sounds/step.wav,DesktopCat.step.wav",
    "/resource:$PSScriptRoot/Assets/Sounds/bite.wav,DesktopCat.bite.wav",
    $versionSource
)
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File | Sort-Object Name | ForEach-Object FullName)
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw 'Desktop Cat build failed' }
$configuration = Join-Path $output 'config.ini'
if (-not (Test-Path -LiteralPath $configuration)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'config.example.ini') -Destination $configuration
}
Get-Item -LiteralPath $executable
