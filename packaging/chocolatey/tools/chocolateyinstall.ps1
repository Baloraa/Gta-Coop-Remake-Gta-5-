$ErrorActionPreference = 'Stop'

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$packageArgs = @{
  packageName   = 'gtacoop-remake'
  fileFullPath  = Join-Path $toolsDir 'GTACoop.Launcher.exe'
  checksum      = 'a19a1e4bf6ee7fab349640705b245a30d51d43d84b6a7e4c11b4040a35fac3fa'
  checksumType  = 'sha256'
}

# Launcher taşınabilir bir exe'dir; kurulum yalnızca araç dizinine yerleştirir.
$installDir = Join-Path (Get-ToolsLocation) 'GTACoopRemake'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $packageArgs.fileFullPath (Join-Path $installDir 'GTACoop.Launcher.exe') -Force

Write-Host "GTACoop Remake $installDir klasörüne kuruldu."
