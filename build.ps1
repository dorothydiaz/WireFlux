$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$srcDir = Join-Path $root "src"
$instDir = Join-Path $srcDir "Installer"
$distDir = Join-Path $root "dist"
$outApp = Join-Path $distDir "WireFlux.exe"
$outUninst = Join-Path $distDir "Uninstall.exe"
$outSetup = Join-Path $distDir "WireFluxSetup.exe"
$manifest = Join-Path $srcDir "app.manifest"
$iconPath = Join-Path $root "WireFlux.ico"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

if (-not (Test-Path $iconPath)) {
    throw "Icon file not found at $iconPath"
}

if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir | Out-Null
}

# 1. Compile portable WireFlux.exe into dist\WireFlux.exe
$appSources = @(
    (Join-Path $srcDir "AssemblyInfo.cs"),
    (Join-Path $srcDir "Program.cs"),
    (Join-Path $srcDir "NetworkAdapterService.cs"),
    (Join-Path $srcDir "NetworkToolsService.cs"),
    (Join-Path $srcDir "SpeedTestService.cs"),
    (Join-Path $srcDir "StartupManager.cs"),
    (Join-Path $srcDir "IconGenerator.cs"),
    (Join-Path $srcDir "ThemeManager.cs"),
    (Join-Path $srcDir "UiControls.cs"),
    (Join-Path $srcDir "MainForm.cs"),
    (Join-Path $srcDir "TrayApplicationContext.cs")
)

$appArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/win32manifest:$manifest",
    "/win32icon:$iconPath",
    "/out:$outApp",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Management.dll",
    "/r:System.Windows.Forms.dll"
) + $appSources

Write-Host "Compiling portable WireFlux.exe..."
& $csc @appArgs
if ($LASTEXITCODE -ne 0) {
    throw "WireFlux.exe compilation failed with exit code $LASTEXITCODE"
}

# 2. Compile portable Uninstall.exe into dist\Uninstall.exe
$uninstSources = @(
    (Join-Path $instDir "SetupControls.cs"),
    (Join-Path $instDir "UninstallerProgram.cs")
)
$uninstArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/win32manifest:$manifest",
    "/win32icon:$iconPath",
    "/out:$outUninst",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll"
) + $uninstSources

Write-Host "Compiling portable Uninstall.exe..."
& $csc @uninstArgs
if ($LASTEXITCODE -ne 0) {
    throw "Uninstall.exe compilation failed with exit code $LASTEXITCODE"
}

# 3. Package standalone single-file installer: dist\WireFluxSetup.exe
$setupSources = @(
    (Join-Path $instDir "ShortcutHelper.cs"),
    (Join-Path $instDir "SetupControls.cs"),
    (Join-Path $instDir "InstallerProgram.cs")
)
$setupArgs = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/win32manifest:$manifest",
    "/win32icon:$iconPath",
    "/out:$outSetup",
    "/resource:$outApp,WireFlux.exe",
    "/resource:$outUninst,Uninstall.exe",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll"
) + $setupSources

Write-Host "Packaging standalone installer WireFluxSetup.exe..."
& $csc @setupArgs
if ($LASTEXITCODE -ne 0) {
    throw "WireFluxSetup.exe compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Build complete:"
Write-Host "  - $outApp (Portable App)"
Write-Host "  - $outUninst (Portable Uninstaller)"
Write-Host "  - $outSetup (Standalone Installer)"
