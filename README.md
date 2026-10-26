# GTACoop Remake — Türkçe Mod + Sunucu Yöneticisi

2.2.5 sürümü — kendi sunucu yönetimi, otomatik mod indirme, **oturum modu**,
**kısa kod** (ör. `Gel.com`) ile internetten sunucu bulma, **otomatik relay** ve
**Bileşenler** sekmesinden ScriptHookV / ScriptHookVDotNet / OpenIV.asi / RageOpenV kurulumu:
port yönlendirme veya Radmin VPN olmadan da arkadaşlarınız bağlanabilir. Sunucu modları
yalnızca oyun açıkken kurulu kalır, oyundan çıkınca otomatik kaldırılır.
Oyun başlatma dosyası seçilebilir; varsayılan **PlayGTAV.exe**'dir (GTA5.exe de seçilebilir).

---

## Ne işe yarar?

- **Kendi modlarınız karışmaz**: PC'nize kurduğunuz modlara hiç dokunulmaz; kendi modlarınızı
  yönetmenize gerek yoktur — zaten kurulu hâlleriyle çalışır.
- **Kendi sunucunuz olsun**: kendi GTACoOp sunucunuzu bu launcher'dan başlatın; addon araç/ped
  paketi, script, script klasörü, ASI eklentisi ekleyin.
- **Port yönlendirme/Radmin olmadan**: kısa kod kullanırsanız launcher otomatik bir TCP relay
  açar. Arkadaşınızın yönlendirici ayarı yapmasına gerek kalmaz; doğrudan bağlantı (LAN,
  Radmin veya port yönlendirme) varsa önce o denenir, yoksa relay devreye girer.
- **Sunucuya girince modlar inisin**: sunucunun tüm modları otomatik indirilir ve yalnızca siz
  oynarken kurulu kalır. Oyunu kapatınca **otomatik kaldırılır**; tek oyunculu (offline) modda
  sunucu modları çalışmaz.
- **Tekrar indirme yok**: indirilen dosyalar bilgisayarınızda saklanır (önbellek); ikinci
  girişte yeniden indirilmez. DLC paketleri oyun klasörüne kopyalanmaz, bağlantı (junction)
  ile eklenir — GB'larca veri çoğaltılmaz.
- **Büyük/bozuk update.rpf güvenliği**: `mods/update/update.rpf` 3,9 GB'a yakınsa veya
  yapısı bozuksa DLC paketleri **kurulmaz/atlanır**; dosyanız değiştirilmez, otomatik yedekle
  ezilmez. Diğer script/ASI modları kurulmaya devam eder. Onarım için **Yedekler** bölümünü
  kullanabilirsiniz.
- **Gerekirse elle yenileme**: bir sunucunun indirdiği dosyaları bozuk/eksik bulursanız
  **Sunucular** sekmesinde sunucuyu seçip **Yeniden İndir...** deyin; yalnızca o sunucunun
  önbelleği silinip baştan indirilir. Bu sunucunun modları oyuna kuruluyken yenileme
  çalışmaz; önce **Oturumu Bitir** ile kaldırın.
- **Aynı mod zaten sizde varsa atlanır**: sunucudaki bir mod bilgisayarınızda zaten mevcutsa
  (aynı adlı script, ASI eklentisi veya DLC paketi) o mod **kabul edilmez**: indirilmez,
  kurulmaz, üzerine yazılmaz. Bu kural addon pedler dahil tüm türler için geçerlidir
  (ped içerikleri karşılaştırılmaz/ellenmez); sahip olduğunuz sürüm aynen korunur.
- **Orijinal oyun bozulmaz**: dlclist her düzenlemeden önce yedeklenir; orijinal `update.rpf`
  dosyasına asla dokunulmaz. Sizin dosyalarınız (aynı adlı olsa bile) korunur.

---

## Uygulamadaki sekmeler

- **Oyun**: oyun klasörünü seçme, istemciyi kurma (GTACoOp), **oyuncu adınızı** seçme, oyun
  durumu, yedekler.
- **Bileşenler**: ScriptHookV, ScriptHookVDotNet (stable / nightly / Enhanced), paket içi
  OpenIV.asi ve RageOpenV alternatifini sürüm seçerek kurma/güncelleme; eski dosyaları
  yedekleme ve geri yükleme. NativeTrainer.asi hiçbir zaman kurulmaz; kendi
  script/modlarınıza dokunulmaz.
- **Sunucular**: sunucu ekleme, **Bağlan ve Oyna** (modlar otomatik iner-kurulur), oturum modu
  ayarı, modları önceden indirme, oturumu bitirme.
- **Sunucum**: kendi sunucunuzu başlatma/durdurma, ayarlar, mod ekleme, modları
  **devre dışı bırakma/kaldırma**, **kısa kod** (ör. `Gel.com`) ile sunucuyu internetten
  bulunur yapma.
- **Günlük / Araçlar**: günlük, yedekten geri yükleme, kalıntı temizleme.

---

## Bileşenler (ScriptHookV / ScriptHookVDotNet / OpenIV.asi / RageOpenV)

**Bileşenler** sekmesi, oyunun çalışması için gereken temel mod dosyalarını oyun kök
klasörüne kurar. ScriptHookV / ScriptHookVDotNet / RageOpenV internetten indirilir;
OpenIV.asi ise kullanıcı tarafından sağlanan dosya olarak **paylaşım paketinde gelir**.

| Bileşen | Kaynak | Not |
|---|---|---|
| **ScriptHookV** | `dev-c.com` (resmî, Alexander Blade) | GitHub'da resmî sürüm yoktur ve yeniden dağıtım yasaktır. Yalnızca güncel resmî sürüm sunulur. |
| **ScriptHookVDotNet — Stable** | GitHub `scripthookvdotnet/scripthookvdotnet` | Kararlı sürümler; istediğiniz eski sürümü seçebilirsiniz. |
| **ScriptHookVDotNet — Nightly** | GitHub `scripthookvdotnet/scripthookvdotnet-nightly` | Günlük derlemeler; yeni oyun sürümleri için daha güncel olabilir. |
| **ScriptHookVDotNet — Enhanced** | GitHub `Chiheb-Bacha/ScriptHookVDotNetEnhanced` | Legacy + Enhanced uyumlu genişletilmiş sürüm. |
| **OpenIV.asi (paket içi)** | Paylaşım paketindeki kullanıcı dosyası | ZIP ile gelir; **Bileşenler > OpenIV.asi (paket içi)** seçilince oyun köküne kurulur. `OpenIV.asi`, `OpenRPF.asi` veya `RageOpenV.asi` zaten kuruluysa **çakışmayı önlemek için kurulmaz**; mevcut dosyanız korunur. ASI loader gerekir (ScriptHookV ile birlikte kurun). |
| **RageOpenV (alternatif)** | GitHub `Chiheb-Bacha/RageOpenV` (GPL) | OpenIV.asi'nin açık kaynak alternatifi. Yukarıdaki köprülerden biri zaten kuruluysa **kurulmaz**; mevcut dosyanız korunur. RageOpenV için ASI loader gerekir (ScriptHookV ile birlikte kurun). |

Kurallar:

- **NativeTrainer.asi asla kurulmaz.** ScriptHookV arşivinde bulunsa bile dışarıda bırakılır.
  Daha önce kendi kurduğunuz bir trainer varsa silinmez.
- Yalnızca bilinen dosyalar değiştirilir: `ScriptHookV.dll`, gerekirse ASI loader
  (`dinput8.dll` / `xinput1_4.dll`), `ScriptHookVDotNet.asi`, `ScriptHookVDotNet2/3.dll`,
  `OpenIV.asi`, `RageOpenV.asi`. `scripts/`, `mods/` ve kendi dosyalarınız
  **taranmaz/silinmez**.
- Mevcut `dinput8.dll` / `xinput1_4.dll` ve `ScriptHookVDotNet.ini` kullanıcı dosyası olarak
  korunur; yalnızca bu launcher'ın daha önce kurduğu ve siz değiştirmediğiniz dosyalar
  güncellenir.
- Değiştirilecek her dosya önce
  `mods/GTACoop/backups/components/<bileşen>/<tarih>/` altına yedeklenir. **Son Yedeği Geri
  Yükle** ile geri dönebilirsiniz; geri yüklemeden önce mevcut dosyalar da ayrıca saklanır.
- Aynı sürüm zaten kuruluysa **yeniden indirilmez**; dosyalardan biri eksik/bozuksa **Onar**
  düğmesi görünür. Farklı bir sürüm seçerseniz **Güncelle** denir.
- Sürüm listesi 6 saat önbellekte tutulur; GitHub API'sine gereksiz istek gitmez. İnternet
  yoksa son başarılı liste kullanılır.
- ScriptHookV ve RageOpenV lisansları yeniden dağıtıma izin vermediği için bu dosyalar
  paylaşım ZIP'ine **konmaz**; indirme yalnızca kullanıcının kendi bilgisayarında yapılır.
  OpenIV.asi kullanıcı tarafından sağlandığı için ZIP içinde
  `Distribution\Components\OpenIV.asi` olarak gelir. Kurulumdan önce oyunu kapatın;
  launcher bunu kontrol eder.
- Arkadaşlarınız OpenIV.asi'yi **Bileşenler > OpenIV.asi (paket içi)** ile kalıcı olarak
  kurabilir. Ayrıca **Sunucum > ASI Eklentisi Ekle...** ile sunucuya eklerseniz, bağlanan
  oyuncuda uyumlu bir mods köprüsü yoksa oturum sırasında otomatik kurulur; varsa kendi
  dosyası korunur.

---

## Dil (Türkçe / İngilizce)

- İlk açılışta **Türkçe** veya **English** seçimi sorulur.
- Daha sonra üst çubuktaki **Dil:** kutusundan değiştirebilirsiniz; değişiklik bir sonraki
  açılışta geçerli olur (isterseniz anında yeniden başlatmayı onaylayabilirsiniz).

---

## Kurulum (uygulama konumu + kısayollar)

- `build.ps1` yalnızca `dist\GTACoopRemake\` klasörünü üretir.
- Uygulamayı **güzel konumuna kurmak / güncellemek** için:

  ```powershell
  powershell -ExecutionPolicy Bypass -File kur.ps1
  ```

- Bu betik uygulamayı `%LOCALAPPDATA%\Programs\GTACoop Remake` klasörüne kopyalar ve
  **masaüstü** + **Başlat menüsü** kısayollarını buraya bağlar. Hiçbir şey silinmez
  (eski `dist` klasörü olduğu yerde kalır).
- Uygulama simgesi ve sürüm bilgisi (2.2.5) exe dosyasına işlenmiştir.

---

## Hızlı başlangıç (tek PC)

### 1. Oyun klasörünü seçin

1. Launcher'ı açın.
2. **Oyun** sekmesinden **Gözat...** ile GTA V klasörünüzü seçin (GTA5.exe'nin olduğu klasör).
3. **İstemciyi Kur / Güncelle** düğmesine basın. Bu işlem yalnızca **scripts/** klasörüne
   GTACoOp.dll ve yanındaki dosyaları kopyalar; oyunun orijinal dosyalarına dokunulmaz.
4. İsterseniz **Oyuncu** bölümünden **oyuncu adınızı** yazıp **Kaydet**'e basın — sunucularda
   bu ad görünür.

### 2. Kendi sunucunuzu başlatın

1. **Sunucum** sekmesinden **Sunucuyu Başlat** düğmesine basın.
2. Sunucunuz otomatik oluşturulur ve başlar (ilk seferde birkaç saniye sürebilir). Durum
   kutusunda "çalışıyor" yazısını görün.
3. Kapılar: oyun portu **4499 UDP**, senkronizasyon portu **4599 TCP**. Kısa kod kullanırsanız
   **otomatik relay** devreye girer; arkadaşlarınız için yönlendirici (router) ayarı veya
   Radmin VPN gerekmez. Doğrudan bağlantı/uzun kod kullanacaksanız bu portları yönlendirin;
   Windows Güvenlik Duvarı ilk başlatmada izin ister.

### 3. Sunucuya bağlanın

4. **Sunucular** sekmesinde **kendi sunucum (127.0.0.1) ekle** düğmesine basın; çıkan pencerede
   **Ekle**'ye basın (varsayılanlar doğrudur).
5. Sunucuyu seçip **Bağlan ve Oyna**'ya basın. Sunucu modları (varsa) otomatik iner ve kurulur;
   **yeni ve farklı** modlar için bir kez onay sorulur. Oyun açılır ve sunucuya bağlanır.
6. İlk girişte oyunda **T** tuşuna basıp sohbeti açın ve `/register şifreniz` yazın (bir
   kerelik kayıt). Sonraki girişlerde `/login şifreniz`. (Oyuncu listesi: **Z** tuşu.)
7. Oyunu kapatınca sunucu modları **otomatik kaldırılır** (aşağıdaki "Oturum modu" bölümü).

---

## Oturum modu (modlar oyunda kalmaz)

**Sunucular** sekmesindeki "Oyun kapanınca sunucu modları otomatik kaldırılsın (oturum modu)"
seçeneği varsayılan olarak **açıktır**. Çalışma biçimi:

- **Bağlan ve Oyna** dediğinizde sunucunun modları indirilir ve oyuna eklenir:
  - DLC paketleri `mods/update/x64/dlcpacks/gcs_<ad>` altına **bağlantı (junction)** ile eklenir —
    dosyalar kopyalanmaz, GB'larca veri çoğaltılmaz.
  - Scriptler `scripts/` klasörüne, ASI eklentileri oyun kök klasörüne kopyalanır (küçük dosyalar).
  - Aynı adlı bir dosya sizde zaten **farklı sürümde** varsa sizinki korunur, dokunulmaz.
- Oyunu kapatınca arka plandaki izleyici sunucu modlarını kaldırır; dlclist ve tüm dosyalar
  eski hâline döner. Launcher açık olmasa bile temizlik yapılır (izleyici ayrı süreç olarak çalışır).
  **Alt+F4 ile kapatsanız, oyun çökse veya bilgisayarı kapatıp yeniden açsanız bile** temizlik
  otomatik yapılır (temizlik kaçırılırsa launcher bir sonraki açılışta tamamlar).
- Kaldırılan mod dosyaları **silinmez**; `%LOCALAPPDATA%\GTACoopRemake\removed\<tarih>` klasörüne
  taşınır (en yeni 3 oturum saklanır, eskiler otomatik temizlenir). İndirme önbelleği ayrıca
  durduğu için tekrar bağlandığınızda **hiçbir şey yeniden indirilmez**.
- Oyun hiç başlamazsa (örn. başlatmayı iptal ederseniz) 12 dakika sonra yine otomatik temizlenir.
- **Oturumu Bitir (modları kaldır)** düğmesiyle istediğiniz an (oyun kapalıyken) modları
  hemen kaldırabilirsiniz.
- Seçeneği kapatırsanız modlar kurulu kalır (eski davranış). Önerilen: açık.

**Modları önceden indirme**: **Sunucu Modlarını İndir (ön yükleme)** düğmesi, modları *oyuna
kurmadan* yalnızca indirir. Böylece **Bağlan ve Oyna** sırasında beklemezsiniz; dosyalar bir
daha indirilmez. İndirilmiş dosyaları elle yenilemek için **Yeniden İndir...** kullanın
(o sunucunun dosyaları silinip baştan indirilir, oyuna kurulmaz). Modlar oyuna kuruluyken
yenileme engellenir; önce **Oturumu Bitir**.

**Dosyalar nerede durur?** indirilen mod dosyaları program klasöründe sunucu başına ayrı
saklanır: `Servers\<sunucu adı>__<kimlik>\`. Oyun klasörüne yalnızca oynarken eklenir; oyun
kapanınca oyun klasörü temizlenir, indirilen dosyalar kalır (tekrar indirme olmaz).

---

## 4. Mod ekleme (sunucuya)

**Sunucum** sekmesi → **Sunucu Modları** bölümü:

- **DLC Paketi Ekle (araç/ped)...**: `dlc.rpf` içeren paket klasörünü seçin (araç/ped/harita
  paketleri). İç içe klasörler (`x64\dlcpacks\...`) desteklenir.
- **İstemci Scripti Ekle (dll + ini/xml/pdb)...**: script dosyalarını ve **yanındaki destek
  dosyalarını** (ini, xml, pdb, json, txt, config...) hep birlikte seçin; hepsi birlikte
  oyuncuların `scripts/` klasörüne kurulur. Alt klasörlü scriptler için aşağıdaki seçeneği
  kullanın.
- **Script Klasörü Ekle...**: scriptin **kendi klasörünü** (dll + ini + xml + alt dosyalar)
  olduğu gibi ekleyin. Klasör adı script adı olur.
- **ASI Eklentisi Ekle (.asi)...**: `.asi` eklentisini ve varsa yanındaki destek dosyalarını
  seçin. Oyuncuların oyun **kök klasörüne** kurulur (ASI Loader'ın okuduğu yer) ve oyundan
  çıkınca kaldırılır. İzinli uzantılar: `.asi .ini .xml .pdb .log .txt .cfg .config`
  (yeni bir .asi eklentisi eklerken yanındaki ini/xml'leri de seçmeyi unutmayın).
- **Sunucu Eklentisi Ekle (plugin dll)...**: yalnızca sunucuda çalışan eklenti (istemciye
  indirilmez).

Notlar:

- Eklediğiniz dosyalar `Addons` klasörüne kopyalanır ve sunucuya bağlanan oyunculara otomatik
  indirilir; sunucu yeniden başlatılmadan etkin olur.
- Sunucuya eklediğiniz paketler, oyuncularda zaten kurulu paketlerden **farklı** olmalı
  (aynı dlc.rpf adları çakışabilir). Ayrıca addon araç/ped paketleri oyunun `mods` klasöründe
  OpenIV.asi + dlclist düzenini gerektirir (launcher bunu otomatik yönetir).
- Ayrıntılı meta veri (görünen ad/açıklama) için paket klasörüne `addon.json` koyabilirsiniz.

### Modları devre dışı bırakma ve kaldırma

**Sunucum > Sunucu Modları** bölümündeki iki kontrol:

- **Sunucu modlarını devre dışı bırak (oyunculara gönderilmez)**: işaretliyken sunucu boş
  manifest döndürür; oyuncular bağlanınca sunucu modlarını indirmez/kurmaz. Daha önce kurmuş
  oyuncular bir sonraki bağlantıda bu modları otomatik kaldırır. `Addons` klasöründeki dosyalar
  silinmez; ayarı kaldırınca modlar yeniden dağıtılır. Sunucu çalışırken de etkilidir.
- **Sunucu Modlarını Kaldır...**: `Addons` klasöründeki tüm modları sunucudan kaldırır.
  Dosyalar silinmez; `Addons-Removed\<tarih-saat>\` klasörüne taşınır. Sunucu kimliği
  (`.gtacoop-server.json`) korunur. Güvenlik için sunucu durdurulmuş olmalıdır.

Not: Bu kontroller yalnızca `Addons` içeriğini (oyunculara giden DLC/script/ASI modları) yönetir;
`Plugins` klasöründeki sunucu tarafı eklentileri kapsamaz.

---

## Yedekleme ve güvenlik

- dlclist (update.rpf) her düzenlemeden önce `mods/GTACoop/backups/` altına yedeklenir
  (günde bir kez, son 3 yedek saklanır).
- Bileşenler sekmesinde değiştirilen dosyalar
  `mods/GTACoop/backups/components/<bileşen>/<tarih>/` altına yedeklenir; her bileşen için
  son yedek geri yüklenebilir.
- **Orijinal** `update/update.rpf` dosyasına asla dokunulmaz; yalnızca `mods/update/update.rpf`
  düzenlenir.
- Silme işlemleri yalnızca launcher'ın kurduğu dosyaları kapsar; kendi dosyalarınıza ve
  oyunun dosyalarına dokunulmaz.
- **Günlük / Araçlar** sekmesinden: yedekleri listeleyip geri yükleyebilir, sunuculardan
  kurulmuş tüm eklentileri (script, ASI, dlc paketi) tek tuşla temizleyebilirsiniz.

---

## Arkadaşlarla oynama (kısa kod, uzun kod veya IP)

**En kolay yol (kısa kod, ör. `Gel.com`):**

1. **Sunucum** sekmesindeki **Kısa kod** alanına `Gel.com` gibi bir kod yazın.
   3–64 karakter; harf, rakam, nokta, tire ve alt çizgi kullanılabilir, büyük/küçük harf
   fark etmez. Boş bırakırsanız kısa kod kapalıdır. **Kaydet**'e basın.
2. **Sunucuyu Başlat** deyin. Launcher kodu internet üzerinden yayınlar ve 60 saniyede bir
   tazeler; durum satırında **Kısa kod yayında: Gel.com** yazısını görün.
3. Arkadaşınıza **yalnızca kısa kodu** gönderin (WhatsApp/Discord). Arkadaşınız
   **Sunucular → Kod ile Katıl...** ekranına kodu yazıp **Ekle ve Bağlan**'a bassın; launcher
   sunucu bilgilerini internetten alır.
4. Sunucuyu durdurunca (veya uygulamayı kapatınca) kod çevrimdışı olur; arkadaşınız bağlanamaz.

Kısa kod, sunucu bilgilerini şifreli olarak **ntfy.sh** üzerinden yayınlar; hesap açmanız veya
kendi sunucunuzu kurmanız gerekmez. Sunucu başlayınca launcher ayrıca **otomatik relay**
(`bore.pub`) açar ve kısa koda relay uç noktalarını da ekler. Arkadaşınızın launcher'ı önce
doğrudan adresleri (LAN/Radmin/port yönlendirme) dener; ulaşamazsa relay'e geçer. Böylece
**port yönlendirme veya Radmin VPN gerekmeden** internetten oynayabilirsiniz. Relay, doğrudan
bağlantıdan biraz daha yavaş olabilir; sunucu sahibi ve arkadaşın launcher'ı oyun sırasında
**açık kalmalıdır**. Sunucu açık ve internet bağlantınız çalışır olmalıdır.

**Uzun kod (`GTC1...`) — internet gerektirmez:**

1. Siz **Sunucum → Katılım Kodu Oluştur...** ile adresi seçip kodu kopyalayın,
   arkadaşınıza gönderin (WhatsApp/Discord).
2. Arkadaşınız **Sunucular → Kod ile Katıl...** deyip kodu yapıştırsın ve
   **Ekle ve Bağlan**'a bassın. Uzun kod bağlantı bilgilerini kendi taşır; hesap/internet
   sunucusu gerekmez, sunucu kapalıyken de oluşturulabilir (şifre/token içerebilir, yalnızca
   güvendiğiniz kişiye verin).
3. Siz **Sunucuyu Başlat** deyin; arkadaşınız bağlanınca modlar otomatik iner.

**Not:** Uzun kod relay bilgisi taşımaz; CGNAT/port yönlendirme sorunu varsa **kısa kod**
kullanın. Kısa kodla gelen arkadaşınızın launcher'ı oyun boyunca açık kalmalıdır.

**IP ile (kod olmadan):**

1. Yönlendiricinizde port yönlendirme yapın: **4499 UDP** ve **4599 TCP** → kendi bilgisayarınız.
2. Arkadaşınız **Sunucular** sekmesinden sizin dış IP adresinizle sunucuyu ekleyip
   **Bağlan ve Oyna**'ya basar; gerekli değilse oyun kapılarını (4499/4599) açık tutun.
3. İlk girişte arkadaşınız da oyunda **T** → `/register şifre` yazar.

**Modlar nerede?** Sunucudan indirilen içerik program klasöründe sunucu başına ayrı
saklanır: `Servers\<sunucu adı>__<kimlik>\` (`Sunucum → Sunucu Klasörlerini Aç`).
Oyun sırasında geçici kurulur/bağlanır; oyundan çıkınca kaldırılır. Ayrıntı: `KODLA-KATIL.txt`.

---

## Arkadaşlara gönderme ve lisans

- Bu araç **GNU GPL v3** lisanslıdır (GTA Coop upstream'i ile birlikte). Arkadaşlarınıza
  **ücretsiz** paylaşabilirsiniz; lisans metni ve atıf pakette kalır
  (`Distribution\LICENSE.md`, `Distribution\ATIF-README.md`).
- Hazır paylaşım paketi üretmek için:

  ```powershell
  powershell -ExecutionPolicy Bypass -File paylas.ps1
  ```

  → `dist\GTACoop-Remake-Share.zip` (uygulama + arkadaş okubeni + lisans) oluşur.
  Pakette Türkçe (`OKUBENI-ARKADASLAR.txt`, `KODLA-KATIL.txt`) ve İngilizce
  (`READ-ME-EN.txt`, `JOIN-WITH-CODE-EN.txt`) rehberler birlikte yer alır.
- **Pakete asla eklemeyin:** GTA V oyun dosyaları; ScriptHook V / ScriptHookVDotNet /
  RageOpenV dosyaları (lisansları yeniden dağıtıma izin vermez). Bileşenler sekmesi bunları
  yalnızca kullanıcının bilgisayarında, resmî/GitHub kaynağından indirir; ZIP'e konmaz.
- Arkadaşınızda olması gerekenler: orijinal GTA V + **.NET 8 Desktop Runtime** (launcher
  için). ScriptHookV ve ScriptHookVDotNet'i artık **Bileşenler** sekmesinden kendisi
  kurabilir; ayrıntılı liste `OKUBENI-ARKADASLAR.txt` içinde de yazılıdır. Kendi sunucusunu
  kurarsa ayrıca **.NET 10**.
- Ücretsiz, arkadaş arası paylaşım sorun değildir. **Satmayın / ticari dağıtmayın**;
  sunucunuza eklediğiniz modların paylaşım izni olduğundan emin olun (başkasına ait telifli
  araç/ped paketlerini arkadaşlarınıza otomatik indirtmeyin — modun kendi lisansına bakın).

### GitHub'da yayınlama

- Bu proje **GPL v3** uyumludur; GitHub'da açık kaynak olarak yayınlanabilir. Kök dizindeki
  `LICENSE.md` lisans metnidir; kaynak kod repoda olduğu için GPL'in kaynak koşulu sağlanır.
- `dist\`, `bin\`, `obj\` klasörleri `.gitignore` ile dışlanır. Repoya **oyun dosyası,
  ScriptHook V / ScriptHookVDotNet dosyası veya telifli mod içeriği koymayın**.
- Paylaşım ZIP'ini (`paylas.ps1` çıktısı) repoya değil, **Releases** bölümüne ekleyin.
- Repo açıklamasına şu notu koyun: "Rockstar Games / Take-Two Interactive ile bağlantılı
  değildir. Resmi ürün değildir. Orijinal GTA V gerektirir."
- Ücretsiz tutun; satıştan ve GTA Online'a yönelik özelliklerden kaçının.

---

## Notlar

- Oyunu **Steam açıkken** başlatın; senkronizasyon ve kurulum işlemleri için oyunun kapalı
  olması gerekir (launcher kontrol eder).
- Kurulum gereksinimleri: **.NET 8** (launcher/sunucu) ve **.NET 10** (oyun sunucusu çekirdeği).
- Günlük dosyası: `%LOCALAPPDATA%\GTACoopRemake\logs\` (oturum izleyici günlüğü:
  `session-watch.log`).
- Sorun olursa **Günlük / Araçlar** sekmesine bakın; kalıntı temizliği çoğu sorunu çözer.
