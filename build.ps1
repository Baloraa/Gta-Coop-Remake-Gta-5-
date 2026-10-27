# GTACoop Remake v2 - build script
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
# Output: dist\GTACoopRemake\GTACoop.Launcher.exe

$ErrorActionPreference = "Stop"

# Offline-friendly policy: no .NET telemetry.
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = $PSScriptRoot
$dist = Join-Path $root "dist\GTACoopRemake"
$vendor = Join-Path $root "vendor\Distribution"
$clientProject = Join-Path $root "src\GTACoOp.Client\GTACoOp.Client.csproj"
$pluginProject = Join-Path $root "src\GTACoop.ServerPlugin\GTACoop.ServerPlugin.csproj"
$clientDll = Join-Path $root "src\GTACoOp.Client\bin\Release\net48\GTACoOp.dll"
$pluginDll = Join-Path $root "src\GTACoop.ServerPlugin\bin\Release\net10.0\GTACoop.Remake.Plugin.dll"
$shvdn = Join-Path $root "vendor\Build\ScriptHookVDotNet3.dll"

Write-Host "== GTACoop Remake v2 build ==" -ForegroundColor Cyan
Write-Host "Root:   $root"
Write-Host "Output: $dist"
Write-Host ""

if (-not (Test-Path $shvdn)) {
    throw "Build reference not found: $shvdn"
}

# --- 1) Clean output
if (Test-Path $dist) {
    Write-Host "[1/8] Cleaning old output..."
    Remove-Item $dist -Recurse -Force
} else {
    Write-Host "[1/8] Clean not needed."
}
New-Item -ItemType Directory -Force $dist | Out-Null

# --- 2) Build the GTACoOp client (voice + co-op client)
Write-Host "[2/8] Building GTACoOp client..."
dotnet build $clientProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "GTACoOp client build failed." }
if (-not (Test-Path $clientDll)) { throw "Client output not found: $clientDll" }
Copy-Item $clientDll (Join-Path $vendor "Scripts\GTACoOp.dll") -Force
Write-Host "      GTACoOp.dll copied to vendor."

# --- 3) Build the server bridge plugin
Write-Host "[3/8] Building server bridge plugin..."
dotnet build $pluginProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Server bridge plugin build failed." }
if (-not (Test-Path $pluginDll)) { throw "Plugin output not found: $pluginDll" }
New-Item -ItemType Directory -Force (Join-Path $vendor "Server\Plugins") | Out-Null
Copy-Item $pluginDll (Join-Path $vendor "Server\Plugins\GTACoop.Remake.Plugin.dll") -Force
Write-Host "      GTACoop.Remake.Plugin.dll copied to vendor."

# --- 4) Publish the launcher
Write-Host "[4/8] Publishing launcher..."
dotnet publish (Join-Path $root "src\GTACoop.Launcher\GTACoop.Launcher.csproj") `
    -c Release -o $dist --nologo --no-restore
if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed." }

# --- 5) Publish the sync server
Write-Host "[5/8] Publishing sync server..."
$syncOut = Join-Path $dist "Distribution\SyncServer"
dotnet publish (Join-Path $root "src\GTACoop.SyncServer\GTACoop.SyncServer.csproj") `
    -c Release -o $syncOut --nologo --no-restore
if ($LASTEXITCODE -ne 0) { throw "SyncServer publish failed." }

# --- 6) Copy GTACoOp client/server distribution (vendor)
Write-Host "[6/8] Copying GTACoOp client/server files..."
$serverSrc = Join-Path $vendor "Server"
$scriptsSrc = Join-Path $vendor "Scripts"
if (-not (Test-Path $serverSrc)) { throw "Distribution not found: $serverSrc" }
if (-not (Test-Path $scriptsSrc)) { throw "Distribution not found: $scriptsSrc" }

robocopy $serverSrc (Join-Path $dist "Distribution\Server") /E /NFL /NDL /NJH /NJS /NP | Out-Null
robocopy $scriptsSrc (Join-Path $dist "Distribution\Scripts") /E /NFL /NDL /NJH /NJS /NP | Out-Null

# --- 7) Bundle OpenIV.asi + GPL notices
Write-Host "[7/8] Copying components and licenses..."
$componentsSrc = Join-Path $vendor "Components"
$openIv = Join-Path $componentsSrc "OpenIV.asi"
if (-not (Test-Path $openIv)) { throw "Bundled OpenIV.asi not found: $openIv" }
robocopy $componentsSrc (Join-Path $dist "Distribution\Components") /E /NFL /NDL /NJH /NJS /NP | Out-Null

Copy-Item (Join-Path $vendor "LICENSE.md") (Join-Path $dist "Distribution") -Force
Copy-Item (Join-Path $vendor "README.md") (Join-Path $dist "Distribution\ATIF-README.md") -Force

# --- 8) Publish SyncTool
Write-Host "[8/8] Publishing SyncTool..."
$toolOut = Join-Path $dist "tools\SyncTool"
dotnet publish (Join-Path $root "src\GTACoop.SyncTool\GTACoop.SyncTool.csproj") `
    -c Release -o $toolOut --nologo --no-restore
if ($LASTEXITCODE -ne 0) { throw "SyncTool publish failed." }

# --- Clean apphost files that can leak into the root
Remove-Item (Join-Path $dist "GTACoop.SyncServer.exe") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist "GTACoop.SyncServer.deps.json") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $dist "GTACoop.SyncServer.runtimeconfig.json") -ErrorAction SilentlyContinue

# --- Per-server folder root
New-Item -ItemType Directory -Force (Join-Path $dist "Servers") | Out-Null

Write-Host ""
Write-Host "== BUILD COMPLETE ==" -ForegroundColor Green
Write-Host "Run: $dist\GTACoop.Launcher.exe"
Write-Host ""
Write-Host "Tip: install/update to the real app folder with:  powershell -ExecutionPolicy Bypass -File kur.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "Contents:"
Get-ChildItem $dist | ForEach-Object { Write-Host "  $($_.Name)" }
