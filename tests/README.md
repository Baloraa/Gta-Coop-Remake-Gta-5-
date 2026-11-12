# GTACoop testleri

Bu depoda NuGet test framework'ü gerektirmeyen küçük bir güvenlik smoke-test projesi bulunur.

Windows veya .NET 8 SDK yüklü bir geliştirme makinesinde depo kökünden çalıştırın:

```powershell
dotnet run --project .\tests\GTACoop.Sync.SmokeTests\GTACoop.Sync.SmokeTests.csproj
```

Testler; manifest yolları, Windows aygıt adları, SHA-256/uzunluk metaverisi, yinelenen dosya yolları,
güvenli kök birleştirme ve desteklenen sistemlerde sembolik bağlantı/junction korumasını kontrol eder.

Sembolik bağlantı testi Windows'ta Developer Mode veya uygun yetki gerektirebilir. Yetki yoksa yalnızca
bu test `SKIP` edilir; diğer testler çalışmaya devam eder.
