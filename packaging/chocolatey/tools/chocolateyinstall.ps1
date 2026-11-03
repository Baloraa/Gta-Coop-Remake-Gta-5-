$ErrorActionPreference = 'Stop'

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$packageArgs = @{
  packageName   = 'gtacoop-remake'
  fileFullPath  = Join-Path $toolsDir 'GTACoop.Launcher.exe'
  checksum      = '49b33ec0078a21f41e4a6d295ef0165aad76167fcc41559d7d47431278ef34f6'
  checksumType  = 'sha256'
}

# Launcher taşınabilir bir exe'dir; kurulum yalnızca araç dizinine yerleştirir.
$installDir = Join-Path (Get-ToolsLocation) 'GTACoopRemake'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $packageArgs.fileFullPath (Join-Path $installDir 'GTACoop.Launcher.exe') -Force

Write-Host "GTACoop Remake $installDir klasörüne kuruldu."
