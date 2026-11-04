# Packaging

Bu klasör, launcher'ı paket yöneticileriyle dağıtmak için hazırlanmış manifestleri içerir.
Sürüm numarası ve exe özeti (SHA256) her sürümde güncellenir: `2.8.1` / `A4BAA4D20928ABA36DBC2F4FBE8C06E7921FA5AAD3FDCB1EDFFCB828BE2A568F`.

## winget

- `winget/Baloraa.GTACoopRemake*.yaml` üç dosyalı (version / defaultLocale / installer)
  manifesttir. Doğrulama ve yerel kurulum denemesi:

  ```
  winget validate packaging/winget
  winget install --manifest packaging/winget
  ```

- Yayına almak için manifestler `microsoft/winget-pkgs` deposuna PR olarak gönderilir
  (`manifests/b/Baloraa/GTACoopRemake/2.8.1/...`). PR'ı senin adına açamam; dosyalar hazır.

## Chocolatey

- `chocolatey/gtacoop-remake.nuspec` ve `chocolatey/tools/chocolateyinstall.ps1`.
  Paketlemek için `gtacoop-remake.nuspec` yanına `GTACoop.Launcher.exe` kopyalanır:

  ```
  cd packaging/chocolatey
  copy ..\..\dist\GTACoopRemake\GTACoop.Launcher.exe tools  choco pack
  ```

  Ardından `choco push gtacoop-remake.2.8.1.nupkg --source https://push.chocolatey.org/`
  (Chocolatey API anahtarı gerekir).

## Kode imzalama

Exe şu an imzasızdır. İmzalamak için bir kod imzalama sertifikası gerekir:
`signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a dist\GTACoopRemake\GTACoop.Launcher.exe`
Sertifika olmadan SmartScreen "bilinmeyen yayıncı" uyarısı verir; bu bir hata değildir.
