$ErrorActionPreference = 'Stop'

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$packageArgs = @{
  packageName   = 'gtacoop-remake'
  fileFullPath  = Join-Path $toolsDir 'GTACoop.Launcher.exe'
  checksum      = 'ca01a6d1c75383d496ae3b15f5c45de4c8f00b916110ada36324b8b390eb36e2'
  checksumType  = 'sha256'
}

# Launcher taşınabilir bir exe'dir; kurulum yalnızca araç dizinine yerleştirir.
$installDir = Join-Path (Get-ToolsLocation) 'GTACoopRemake'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $packageArgs.fileFullPath (Join-Path $installDir 'GTACoop.Launcher.exe') -Force

Write-Host "GTACoop Remake $installDir klasörüne kuruldu."
