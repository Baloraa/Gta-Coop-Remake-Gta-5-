$ErrorActionPreference = 'Stop'

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$packageArgs = @{
  packageName   = 'gtacoop-remake'
  fileFullPath  = Join-Path $toolsDir 'GTACoop.Launcher.exe'
  checksum      = '6e3b8ce77ff9c5ec1edd78a8a17c9b3533e07dbc9f3933f01a74b84d2c8b2720'
  checksumType  = 'sha256'
}

# Launcher taşınabilir bir exe'dir; kurulum yalnızca araç dizinine yerleştirir.
$installDir = Join-Path (Get-ToolsLocation) 'GTACoopRemake'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $packageArgs.fileFullPath (Join-Path $installDir 'GTACoop.Launcher.exe') -Force

Write-Host "GTACoop Remake $installDir klasörüne kuruldu."
