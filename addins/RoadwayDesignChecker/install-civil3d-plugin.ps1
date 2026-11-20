<#
.SYNOPSIS
    Builds and installs the Roadway Design Checker plugin (SHC 301 & AASHTO) for Autodesk Civil 3D 2025 / 2026 / 2027 (.NET 10).

.EXAMPLE
    .\install-civil3d-plugin.ps1
    .\install-civil3d-plugin.ps1 -Civil3dYear 2027
    .\install-civil3d-plugin.ps1 -AllUsers
#>
[CmdletBinding()]
param(
    [ValidateSet("Auto", "2025", "2026", "2027")]
    [string]$Civil3dYear = "Auto",

    [switch]$AllUsers,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

Write-Host "=== Roadway Design Checker (SHC 301 & AASHTO) — .NET 10 Installer ===" -ForegroundColor Cyan

# 1. Verify .NET 10 SDK (or newer)
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK was not found. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and re-run this script."
}

$dotnetVersion = (dotnet --version).Trim()
Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Green

# 2. Detect Civil 3D 2027, 2026, or 2025 installation
if ($Civil3dYear -eq "Auto") {
    foreach ($candidate in @("2027", "2026", "2025")) {
        if (Test-Path "C:\Program Files\Autodesk\AutoCAD $candidate\C3D\AeccDbMgd.dll") {
            $Civil3dYear = $candidate
            break
        }
    }
    if ($Civil3dYear -eq "Auto") {
        throw "Could not find Civil 3D 2025, 2026, or 2027 in 'C:\Program Files\Autodesk\'. Specify -Civil3dYear or edit Directory.Build.props."
    }
}

$acadDir = "C:\Program Files\Autodesk\AutoCAD $Civil3dYear"
$c3dDir  = Join-Path $acadDir "C3D"
if (-not (Test-Path (Join-Path $c3dDir "AeccDbMgd.dll"))) {
    throw "Civil 3D $Civil3dYear API DLLs were not found at '$c3dDir'."
}

Write-Host "Detected Autodesk Civil 3D $Civil3dYear at: $acadDir" -ForegroundColor Green

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot   = Resolve-Path (Join-Path $scriptDir "..\..")
$coreTests  = Join-Path $scriptDir "tests\RoadwayDesignChecker.Core.Tests\RoadwayDesignChecker.Core.Tests.csproj"
$pluginProj = Join-Path $scriptDir "src\RoadwayDesignChecker.Civil3D\RoadwayDesignChecker.Civil3D.csproj"
$bundleSrc  = Join-Path $repoRoot "plugins\RoadwayDesignChecker.bundle"

# 3. Run Unit Tests
if (-not $SkipTests) {
    Write-Host "`n[1/3] Running SHC 301 & AASHTO unit tests (.NET 10)..." -ForegroundColor Yellow
    dotnet test $coreTests -c Release
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed." }
}

# 4. Build Plugin (automatically stages DLLs & Standards into plugins/RoadwayDesignChecker.bundle/Contents)
Write-Host "`n[2/3] Building Civil 3D $Civil3dYear plugin (.NET 10 x64)..." -ForegroundColor Yellow
dotnet build $pluginProj -c Release -p:Platform=x64 -p:Civil3dYear=$Civil3dYear
if ($LASTEXITCODE -ne 0) { throw "Plugin build failed." }

# 5. Copy Bundle to Autodesk ApplicationPlugins folder
$pluginsRoot = if ($AllUsers) {
    Join-Path $env:ProgramData "Autodesk\ApplicationPlugins"
} else {
    Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
}

$bundleDest = Join-Path $pluginsRoot "RoadwayDesignChecker.bundle"

Write-Host "`n[3/3] Installing bundle to: $bundleDest" -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $pluginsRoot | Out-Null
if (Test-Path $bundleDest) {
    Remove-Item -Recurse -Force $bundleDest
}
Copy-Item -Recurse -Force $bundleSrc $bundleDest

Write-Host "`nInstallation complete!" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Close and reopen Autodesk Civil 3D $Civil3dYear."
Write-Host "  2. Open any drawing with an Alignment (and optional Profile)."
Write-Host "  3. Click Analyze -> Roadway QA -> Roadway Checker (or type CHECKROADWAY)."
