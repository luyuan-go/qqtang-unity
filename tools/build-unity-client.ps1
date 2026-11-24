<#
.SYNOPSIS
Headless Windows 64-bit Unity client builder (ZERO visible GUI windows).
Prepares local StreamingAssets ArtCache/Audio/Catalogs (if not already present) and builds
local-runtime/build/QqtangDaoistClient.exe using Unity 6000.6.0f1 in -batchmode -nographics mode.
#>
param(
    [string]$UnityPath = "D:\luyuan\tools\Unity\6000.6.0f1\Editor\Unity.exe",
    [switch]$SkipArtPrepare
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ClientDir = Join-Path $RepoRoot "client"
$LogDir = Join-Path $RepoRoot "local-runtime\logs"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogFile = Join-Path $LogDir "unity-build.log"

if (-not (Test-Path $UnityPath)) {
    throw "Unity Editor not found at $UnityPath. Pass -UnityPath <path-to-Unity.exe>."
}

if (-not $SkipArtPrepare) {
    $Manifest = Join-Path $ClientDir "Assets\StreamingAssets\ArtCache\manifest.json"
    if (-not (Test-Path $Manifest)) {
        Write-Host "[build] Populating local StreamingAssets ArtCache/Audio/Catalogs..."
        python (Join-Path $PSScriptRoot "prepare-art-cache.py") --families town,common,box,bomb --max-fallback 32 --max-icons 60
    }
}

Write-Host "[build] Running Unity headless StandaloneWindows64 build..."
$proc = Start-Process -FilePath $UnityPath -ArgumentList @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $ClientDir,
    "-executeMethod", "Qqtang.UnityClient.Editor.HeadlessBuilder.BuildWindows64",
    "-logFile", $LogFile
) -NoNewWindow -Wait -PassThru

Write-Host "[build] Unity exited with code $($proc.ExitCode). Log: $LogFile"
if ($proc.ExitCode -ne 0) {
    Select-String -Path $LogFile -Pattern "error CS|BuildFailed|Result=Failed|Exception" | Select-Object -First 30
    exit $proc.ExitCode
}

$ExePath = Join-Path $RepoRoot "local-runtime\build\QqtangDaoistClient.exe"
if (-not (Test-Path $ExePath)) {
    throw "Build reported success but $ExePath was not found."
}
Write-Host "[build] SUCCESS! Standalone client ready at: $ExePath"
