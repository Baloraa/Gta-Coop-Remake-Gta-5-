# Packaging

Bu klasör, launcher'ı paket yöneticileriyle dağıtmak için hazırlanmış manifestleri içerir.
Sürüm numarası ve exe özeti (SHA256) her sürümde güncellenir: `2.7.5` / `A19A1E4BF6EE7FAB349640705B245A30D51D43D84B6A7E4C11B4040A35FAC3FA`.

## winget

- `winget/Baloraa.GTACoopRemake*.yaml` üç dosyalı (version / defaultLocale / installer)
  manifesttir. Doğrulama ve yerel kurulum denemesi:

  ```
  winget validate packaging/winget
  winget install --manifest packaging/winget
  ```

- Yayına almak için manifestler `microsoft/winget-pkgs` deposuna PR olarak gönderilir
  (`manifests/b/Baloraa/GTACoopRemake/2.7.5/...`). PR'ı senin adına açamam; dosyalar hazır.

## Chocolatey

- `chocolatey/gtacoop-remake.nuspec` ve `chocolatey/tools/chocolateyinstall.ps1`.
  Paketlemek için `gtacoop-remake.nuspec` yanına `GTACoop.Launcher.exe` kopyalanır:

  ```
  cd packaging/chocolatey
  copy ..\..\dist\GTACoopRemake\GTACoop.Launcher.exe tools  choco pack
  ```

  Ardından `choco push gtacoop-remake.2.7.5.nupkg --source https://push.chocolatey.org/`
  (Chocolatey API anahtarı gerekir).

## Kode imzalama

Exe şu an imzasızdır. İmzalamak için bir kod imzalama sertifikası gerekir:
`signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a dist\GTACoopRemake\GTACoop.Launcher.exe`
Sertifika olmadan SmartScreen "bilinmeyen yayıncı" uyarısı verir; bu bir hata değildir.
