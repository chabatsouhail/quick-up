# build.ps1 - Build Dynamic Web Panel (.NET 10 + WebView2)
# Output:
#   dist\win-unpacked\        - Unpackaged single-file app + WebView2Loader.dll (for installer)
#   dist\DynamicWebPanel.exe  - Self-extracting single-file portable executable
#   dist\DynamicWebPanelSetup.exe - Optional Inno Setup installer (when ISCC.exe is installed)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

Write-Host "== Dynamic Web Panel (.NET 10 + WebView2) build ==" -ForegroundColor Cyan

$distRoot = Join-Path $PSScriptRoot 'dist'
$unpackedDir = Join-Path $distRoot 'win-unpacked'
$portableTmp = Join-Path $distRoot '_portable_tmp'
$portableExe = Join-Path $distRoot 'DynamicWebPanel.exe'

Get-Process -Name 'DynamicWebPanel' -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and $_.Path.StartsWith($distRoot, [System.StringComparison]::OrdinalIgnoreCase)
} | ForEach-Object {
    Write-Host "Stopping running instance (PID $($_.Id)) before rebuild..." -ForegroundColor Yellow
    Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    $_.WaitForExit(3000) | Out-Null
}

foreach ($dir in @($unpackedDir, $portableTmp)) {
    if (Test-Path $dir) {
        Remove-Item -LiteralPath $dir -Recurse -Force
    }
}
if (Test-Path $portableExe) {
    Remove-Item -LiteralPath $portableExe -Force
}

# 1. Unpacked release (single managed exe + loose native WebView2Loader.dll for Inno Setup)
Write-Host "`n[1/3] Publishing unpacked release (dist\win-unpacked)..." -ForegroundColor Yellow
dotnet publish 'QuickUp.csproj' `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $unpackedDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (unpacked) failed" }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets\icon.ico') -Destination (Join-Path $unpackedDir 'icon.ico') -Force
if ((Test-Path (Join-Path $unpackedDir 'WebView2Loader.dll')) -and (Test-Path (Join-Path $unpackedDir 'runtimes'))) {
    Remove-Item -LiteralPath (Join-Path $unpackedDir 'runtimes') -Recurse -Force
}

# 2. Portable single-file executable (bundles WebView2Loader.dll for self-extraction)
Write-Host "`n[2/3] Publishing portable single-file executable (dist\DynamicWebPanel.exe)..." -ForegroundColor Yellow
dotnet publish 'QuickUp.csproj' `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $portableTmp
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (portable) failed" }

$builtPortable = Join-Path $portableTmp 'DynamicWebPanel.exe'
if (-not (Test-Path $builtPortable)) { throw "Expected portable executable was not produced: $builtPortable" }
Move-Item -LiteralPath $builtPortable -Destination $portableExe -Force
Remove-Item -LiteralPath $portableTmp -Recurse -Force

# 3. Optional Inno Setup installer
Write-Host "`n[3/3] Checking Inno Setup (setup.iss)..." -ForegroundColor Yellow
$iscc = $null
$cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($cmd) {
    $iscc = $cmd.Source
} else {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 5\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $iscc = $c; break }
    }
}

if ($iscc) {
    & $iscc 'setup.iss'
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }
} else {
    Write-Host "  ISCC.exe not found - skipped installer compilation (portable and unpacked builds are ready)." -ForegroundColor DarkYellow
}

# Results summary
Write-Host "`n== Output ==" -ForegroundColor Cyan
$unpackedTotal = (Get-ChildItem $unpackedDir -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("{0,-32} {1,7:N2} MB" -f 'dist\win-unpacked (total)', ($unpackedTotal / 1MB))
Get-ChildItem 'dist' -File -Filter '*.exe' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host ("{0,-32} {1,7:N2} MB" -f $_.Name, ($_.Length / 1MB))
}

Write-Host "`nDone." -ForegroundColor Green
