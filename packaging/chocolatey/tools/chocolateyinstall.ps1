$ErrorActionPreference = 'Stop'

$toolsDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

$packageArgs = @{
  packageName   = 'gtacoop-remake'
  fileFullPath  = Join-Path $toolsDir 'GTACoop.Launcher.exe'
  checksum      = '7da6edb04f7d8beb684258a69a108a4106c155968b20ab4dab2b425fd440b30e'
  checksumType  = 'sha256'
}

# Launcher taşınabilir bir exe'dir; kurulum yalnızca araç dizinine yerleştirir.
$installDir = Join-Path (Get-ToolsLocation) 'GTACoopRemake'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item $packageArgs.fileFullPath (Join-Path $installDir 'GTACoop.Launcher.exe') -Force

Write-Host "GTACoop Remake $installDir klasörüne kuruldu."
