# Packaging

Bu klasör, launcher'ı paket yöneticileriyle dağıtmak için hazırlanmış manifestleri içerir.
Sürüm numarası ve exe özeti (SHA256) her sürümde güncellenir: `2.7.7` / `91CE14C9704A5FC712421F833F295DF650E99F40B5AB6863ECEAA5A9B2C5F4CF`.

## winget

- `winget/Baloraa.GTACoopRemake*.yaml` üç dosyalı (version / defaultLocale / installer)
  manifesttir. Doğrulama ve yerel kurulum denemesi:

  ```
  winget validate packaging/winget
  winget install --manifest packaging/winget
  ```

- Yayına almak için manifestler `microsoft/winget-pkgs` deposuna PR olarak gönderilir
  (`manifests/b/Baloraa/GTACoopRemake/2.7.7/...`). PR'ı senin adına açamam; dosyalar hazır.

## Chocolatey

- `chocolatey/gtacoop-remake.nuspec` ve `chocolatey/tools/chocolateyinstall.ps1`.
  Paketlemek için `gtacoop-remake.nuspec` yanına `GTACoop.Launcher.exe` kopyalanır:

  ```
  cd packaging/chocolatey
  copy ..\..\dist\GTACoopRemake\GTACoop.Launcher.exe tools  choco pack
  ```

  Ardından `choco push gtacoop-remake.2.7.7.nupkg --source https://push.chocolatey.org/`
  (Chocolatey API anahtarı gerekir).

## Kode imzalama

Exe şu an imzasızdır. İmzalamak için bir kod imzalama sertifikası gerekir:
`signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a dist\GTACoopRemake\GTACoop.Launcher.exe`
Sertifika olmadan SmartScreen "bilinmeyen yayıncı" uyarısı verir; bu bir hata değildir.
