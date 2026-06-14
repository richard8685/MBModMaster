param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\MBModMaster\MBModMaster.csproj"
$outputRoot = Join-Path $repoRoot "artifacts\portable"
$publishDir = Join-Path $outputRoot "MBModMaster-$Runtime"
$zipPath = Join-Path $outputRoot "MBModMaster-$Runtime.zip"

function Resolve-DotNet {
    $scoopDotNet = "D:\Scoop\apps\dotnet-sdk\current\dotnet.exe"
    if (Test-Path $scoopDotNet) {
        return $scoopDotNet
    }

    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        return $dotnetCommand.Source
    }

    throw "dotnet SDK was not found."
}

$dotnet = Resolve-DotNet
$dotnetRoot = Split-Path -Parent $dotnet
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_ROOT_X64 = $dotnetRoot

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir | Out-Null

& $dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $publishDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

$portableReadme = @"
MBModMaster Portable

Run MBModMaster.exe directly. This build is self-contained and does not require installing the .NET runtime.

Notes:
- The app still reads and writes Mount & Blade II: Bannerlord game and launcher files.
- Back up your Bannerlord launcher configuration before changing load order.
"@

Set-Content -Path (Join-Path $publishDir "PORTABLE-README.txt") -Value $portableReadme -Encoding UTF8

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Portable build:"
Write-Host $publishDir
Write-Host "Archive:"
Write-Host $zipPath
