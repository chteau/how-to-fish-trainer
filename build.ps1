#Requires -Version 5.1
<#
    Builds the trainer and the patcher, then stages everything into dist\.
    Windows (and cross-platform pwsh). Linux/macOS users: use build.sh.

    Usage:
        .\build.ps1
        .\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\How to Fish\How to Fish"
#>
param(
    [string]$GameDir
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$root = Split-Path -Parent $PSCommandPath

function Get-Dotnet {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    foreach ($candidate in @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "${env:ProgramFiles(x86)}\dotnet\dotnet.exe",
        "$env:USERPROFILE\.dotnet\dotnet.exe",
        "$env:HOME/.dotnet/dotnet"
    )) {
        if ($candidate -and (Test-Path $candidate)) { return $candidate }
    }

    throw "dotnet not found. Install the .NET 8 SDK: https://dotnet.microsoft.com/download"
}

$dotnet = Get-Dotnet

function Invoke-Build([string]$project) {
    & $dotnet build $project -c Release -v quiet --nologo
    if ($LASTEXITCODE -ne 0) { throw "build failed: $project" }
}

Write-Host '==> building patcher'
Invoke-Build (Join-Path $root 'src\HtfPatcher')

$patcher = Join-Path $root 'src\HtfPatcher\bin\Release\HtfPatcher.dll'

# The patcher knows how to find the game on every platform, so it vendors the reference
# assemblies rather than each build script reimplementing Steam library discovery.
Write-Host '==> vendoring game references into lib\'
$refArgs = @($patcher, 'refs', '--out', (Join-Path $root 'lib'))
if ($GameDir) { $refArgs += @('--game', $GameDir) }
& $dotnet @refArgs
if ($LASTEXITCODE -ne 0) { throw 'could not vendor game references' }

Write-Host '==> building trainer'
Invoke-Build (Join-Path $root 'src\HtfTrainer')

$dist = Join-Path $root 'dist'
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist | Out-Null

Copy-Item (Join-Path $root 'src\HtfTrainer\bin\Release\HtfTrainer.dll') $dist
foreach ($file in @('HtfPatcher.dll', 'HtfPatcher.runtimeconfig.json', 'dnlib.dll', 'HtfPatcher.deps.json')) {
    $source = Join-Path $root "src\HtfPatcher\bin\Release\$file"
    if (Test-Path $source) { Copy-Item $source $dist }
}

@'
@echo off
setlocal
where dotnet >nul 2>nul || (
  echo dotnet not found. Install the .NET 8 runtime: https://dotnet.microsoft.com/download
  exit /b 1
)
dotnet "%~dp0HtfPatcher.dll" %*
'@ | Set-Content -Path (Join-Path $dist 'htf.cmd') -Encoding ASCII

@'
#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if command -v dotnet >/dev/null 2>&1; then DOTNET="$(command -v dotnet)"
else DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"; fi
exec "$DOTNET" "$HERE/HtfPatcher.dll" "$@"
'@ -replace "`r`n", "`n" | Set-Content -Path (Join-Path $dist 'htf') -Encoding ASCII -NoNewline

Write-Host ''
Write-Host "==> staged in $dist"
Write-Host '    dist\htf.cmd status'
Write-Host '    dist\htf.cmd patch'
Write-Host '    dist\htf.cmd unpatch'
