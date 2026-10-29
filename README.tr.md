# GTACoop Remake — Türkçe Mod + Sunucu Yöneticisi

**Türkçe** | [English](README.md)

2.3.6 sürümü - kendi sunucu yönetimi, otomatik mod indirme, **oturum modu**,
**kısa kod** (ör. `Gel.com`) ile internetten sunucu bulma, **otomatik relay**,
**UPnP doğrudan bağlantı**, **özel relay sunucusu**, **tanılama kopyalama**,
**canlı sunucu durumu**, **Sunucu Tarayıcı** sekmesi, **OpenIV (.oiv) paket desteği**
ve **Bileşenler** sekmesinden
ScriptHookV / ScriptHookVDotNet / OpenIV.asi / RageOpenV kurulumu:
port yönlendirme veya Radmin VPN olmadan da arkadaşlarınız bağlanabilir. Sunucu modları
yalnızca oyun açıkken kurulu kalır, oyundan çıkınca otomatik kaldırılır.
Oyun başlatma dosyası seçilebilir; varsayılan **PlayGTAV.exe**'dir (GTA5.exe de seçilebilir).
**Sesli sohbet** (bas-konuş, `[MIC]` göstergesi) ve **host-led hikaye görevi koordinasyonu**
bu sürümde eklendi; ayrıntılar aşağıda.

### 2.3.6 — OpenIV (.oiv) paket desteği

- **Sunucum > OIV Paketi Ekle (.oiv)...** ile `.oiv` dosyalarını sunucuya ekleyin.
  Her paket kendi klasöründe saklanır: `Addons\oiv\<paket>\package.oiv`; böylece
  aynı addaki veya farklı sürümdeki paketler birbirine karışmaz.
- OIV'nin yalnızca **güvenli alt kümesi** uygulanır: `scripts\`, `mods\` ve
  `update\x64\dlcpacks\...` altına dosya ekleme; `mods\update\update.rpf` içindeki
  dlclist'e eksik girdi ekleme. `delete`, `replace`, orijinal RPF düzenlemesi ve
  doğrudan `update.rpf` değiştirme **uygulanmaz**, günlüğe uyarı yazılır.
- Var olan farklı bir dosyanın üzerine **asla** yazılmaz. Kurulan her dosyanın
  SHA-256 değeri kaydedilir; kaldırırken yalnızca launcher'ın yazdığı ve hâlâ
  aynı olan dosyalar silinir. Kullanıcı dosyası değiştirildiyse korunur.
- Güncelleme başarısız olursa eski sürümün dosyaları geri getirilir; dlclist
  girdileri yalnızca başka bir kurulu paket aynı DLC klasörünü sağlamıyorsa kaldırılır.

### 2.3.5 — canlı sunucu durumu

- **Sunucular** sekmesine **Durum** sütunu eklendi. Listede görünen her sunucunun
  sync portuna 5 saniyede bir kısa TCP bağlantısı denenir; sonuç Minecraft'taki
  sunucu listesi gibi **● Çevrimiçi (ms)** / **● Çevrimdışı** / **◌ Bilinmiyor**
  olarak canlı güncellenir.
- Sunucu açıldığında satır en geç 5 saniye içinde yeşile döner; kapandığında kırmızıya.
  Relay kullanan kayıtlarda önce doğrudan adres, sonra relay uç noktası denenir.
- Kısa kodla eklenmiş ve relay bilgisi eskiymiş gibi görünen kayıtlar, sekme
  görünürken en fazla 30 saniyede bir koddan sessizce tazelenir; böylece sunucu
  yeniden başlatılsa da durum doğru kalır.
- Durum kontrolü yalnızca **Sunucular** sekmesi görünürken çalışır; arka planda
  gereksiz ağ trafiği oluşturmaz.

### 2.3.4 — UPnP, özel relay ve tanılama

- Sunucu başlarken Windows UPnP (`HNetCfg.NATUPnP`) ile **oyun UDP** ve **sync TCP**
  portları otomatik yönlendirilir. Yönlendirici UPnP'yi desteklemiyorsa veya kapalıysa
  launcher hata vermez; relay aynen çalışmaya devam eder. Durdurunca yalnızca
  launcher'ın eklediği eşlemeler kaldırılır; mevcut veya başka uygulamalara ait
  eşlemelere dokunulmaz.
- **Sunucum > Sunucu Ayarları > Relay sunucusu** alanına `host` veya `host:port`
  yazılabilir. Boş bırakılırsa `bore.pub` kullanılır. Özel bir sunucu seçildiğinde
  başarısızlıkta **sessizce bore.pub'a dönülmez**; aynı sunucu yeniden denenir.
  `GTACOOP_RELAY_SERVER` ortam değişkeni geliştirici için önceliklidir.
- **Günlük / Araçlar > Tanılama Kopyala**: sürüm, işletim sistemi, .NET sürümü,
  GTA klasörü, sunucu/relay ayarları, yerel adresler ve son 80 günlük satırı
  panoya kopyalanır. Şifre ve sync token **eklenmez**; metni sorun bildirirken
  paylaşabilirsiniz.

### 2.3.3 — bağlantı ve indirme temeli

- 4 MB'tan büyük dosyalar **2**, 12 MB'tan büyükler **4 paralel HTTP Range bağlantısıyla**
  indirilir; her parça kendi kaldığı yerden devam eder, sonunda SHA-256 doğrulanır.
- Sunucu Range desteklemiyorsa otomatik olarak tek bağlantıya dönülür; bozuk indirme kurulmaz.
- Aynı bilgisayar/aynı ağ sunucusunda relay tamamen atlanır. İnternet sunucusunda sync
  portuna doğrudan erişim varsa büyük dosyalar doğrudan iner; relay yalnızca oyun trafiğinde kalır.
- Relay uç noktası değişirse kısa koddan otomatik yenilenir; eski port yüzünden indirme asılı kalmaz.
- 45 saniye ilerleme olmazsa indirme yeniden denenir; `scripts\scripts\...` biçimindeki eski
  kurulumlar otomatik onarılır.

---

## Ne işe yarar?

- **Kendi modlarınız karışmaz**: PC'nize kurduğunuz modlara hiç dokunulmaz; kendi modlarınızı
  yönetmenize gerek yoktur — zaten kurulu hâlleriyle çalışır.
- **Kendi sunucunuz olsun**: kendi GTACoOp sunucunuzu bu launcher'dan başlatın; addon araç/ped
  paketi, **OpenIV `.oiv` paketi**, script, script klasörü, ASI eklentisi ekleyin.
- **Port yönlendirme/Radmin olmadan**: kısa kod kullanırsanız launcher otomatik bir TCP relay
  açar. Arkadaşınızın yönlendirici ayarı yapmasına gerek kalmaz; launcher önce relay'i dener,
  relay kurulamazsa aynı ağ/Radmin/port yönlendirme adreslerine geçer.
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
  ayarı, isteğe bağlı **düşük gecikme profili**, modları önceden indirme, oturumu bitirme.
- **Sunucu Tarayıcı**: GTACoOp master listesi, LAN ve doğrudan `IP:port` ile sunucu bulma;
  sunucu adı, oyuncu sayısı, mod, şifre, ping ve protokol sürümü (güncel/eski) görüntüleme;
  tek tıkla bağlanma. Tarayıcıdan bağlanmak sunucuyu **Sunucular** listesine otomatik eklemez;
  kalıcı kayıt isterseniz **Sunuculara Ekle** düğmesini kullanın. Sunucuda GTACoop Remake
  sync sunucusu yoksa oyun modsuz başlatılır.
- **Sunucum**: kendi sunucunuzu başlatma/durdurma, ayarlar, **UPnP port yönlendirme**,
  **özel relay sunucusu**, mod ekleme, **mod yöneticisi** (seçip tek tek devre dışı
  bırakma/kaldırma), **kısa kod** (ör. `Gel.com`) ile sunucuyu internetten bulunur yapma.
- **Günlük / Araçlar**: günlük, **Tanılama Kopyala**, yedekten geri yükleme, kalıntı temizleme.

---

## Sesli sohbet ve hikaye görevleri

### Sesli sohbet

- Oyun içinde **F9 > Ayarlar > Voice Chat (Push-to-Talk)** ile sesli sohbeti açıp
  kapatabilirsiniz. Varsayılan olarak açıktır.
- **Voice Chat Key** satırına basıp istediğiniz tuşu atayabilirsiniz (varsayılan **N**).
  Tuş atarken menü kapanır; **Esc** ile iptal edilir.
- Konuşurken oyuncunun adının solunda `[MIC]` görünür ve adı yeşil olur. Oyuncu
  listesinde de `[MIC]` işareti gösterilir.
- Ses, sunucudaki **GTACoop.Remake.Plugin.dll** köprü eklentisi üzerinden taşınır.
  Eklenti yoksa ses akışı çalışmaya devam eder, ancak `[MIC]` durumu gösterilmez.
- Mikrofon yoksa veya Windows izin vermiyorsa sesli sohbet sessiz kalır; oyunu
  etkilemez.

### Hikaye görevleri (host-led)

GTA V'ın kendi hikaye görevleri tek oyunculu scriptlerdir. Aynı görevi her
istemcide ayrı ayrı başlatmak görev hedeflerini, cutscene'leri ve kayıt durumunu
birbirinden koparır; bu yüzden görev yalnızca **host** tarafında çalıştırılır.

- **F9 > Story Mission Co-op (Host-led)** menüsünden bir görev seçin. Arkadaşlarınıza
  davet bildirimi gider.
- Host, gerçek görevi **Pause > Game > Replay Mission** üzerinden başlatır. Bu ekranda
  yalnızca daha önce tamamladığınız görevler görünür.
- Arkadaşlar aynı dünyada kalır ve senkronize edilen araç/piyade aksiyonunda host'a
  yardım eder. Görev hedefleri ve cutscene'ler host'a aittir; arkadaşlar kendi
  kayıtlarını oynamaz.
- Görev başlangıcı/bitişi istemcilere bildirilir. **Hiçbir kayıt dosyası yazılmaz**
  ve aynı kayıt dosyasına sahip olmanız gerekmez.
- Host, menüden görev seçmeden **Pause > Game > Replay Mission** ile başlatsa bile
  görev otomatik algılanır ve arkadaşlara bildirilir.
- Görev sürerken host her 5 saniyede durum paketi gönderir; **sonradan katılan**
  arkadaşlar da aktif görevi görür. Aynı görev için tekrar bildirim gelmez.
- Bu mod gerçek bir görev senkronizasyon motoru değildir; GTA V'ın hikaye
  scriptlerini çok oyunculu hale getirmez. Arkadaşların görev hedeflerini ayrı ayrı
  tamamlaması beklenmemelidir.

## Düşük gecikme profili (isteğe bağlı)

**Sunucular** sekmesindeki **Düşük gecikme profili** kutusu, bağlantıda trafik ve
ped yoğunluğunu kapatır, akışa alınan NPC sayısını 4'e düşürür. Bu profil, kalabalık
bölgelerde görülen takılma/gecikme belirtilerini azaltır; **senkronizasyon motorunu
yeniden yazmaz** ve tüm desync sorunlarını ortadan kaldırmaz. Profil kapatıldığında
istemci ayarları GTACoOp varsayılanlarına döner (trafik/ped açık, 10 NPC).

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

## Dil (Türkçe / İngilizce / Rusça)

- İlk açılışta **Türkçe**, **English** veya **Русский** seçimi sorulur.
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
- Uygulama simgesi ve sürüm bilgisi (2.3.6) exe dosyasına işlenmiştir.

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
3. Kapılar: oyun portu **4499 UDP**, senkronizasyon portu **4599 TCP**. Kısa kod veya sunucu
   açıkken oluşturulan uzun kod **otomatik relay** bilgisini taşır; arkadaşlarınız için
   yönlendirici (router) ayarı veya Radmin VPN gerekmez. Yalnızca doğrudan modda bu portları
   yönlendirin; Windows Güvenlik Duvarı ilk başlatmada izin ister.

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
  - Scriptler doğrudan `scripts/` klasörüne (alt klasör yapısı korunarak), ASI eklentileri oyun
    kök klasörüne kopyalanır (küçük dosyalar). Paket `scripts/` klasörüyle birlikte gelmiş olsa
    bile dosyalar **`scripts\scripts\...` altına değil**, doğru yere kurulur.
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

**Büyük indirmeler**: DLC paketi relay üzerinden inerken ilerleme çubuğu dosya boyunca
güncellenir; inen/toplam boyut ve hız görünür. Bağlantı koparsa veya 45 saniye boyunca
veri gelmezse indirme **kaldığı yerden** devam eder, baştan başlamaz. 4 MB'tan büyük
dosyalar sunucu destekliyorsa **2 paralel bağlantıyla**, 12 MB'tan büyükler **4 paralel
bağlantıyla** indirilir; her parça kendi kaldığı yerden devam eder. Sunucu HTTP Range
desteklemiyorsa otomatik olarak tek bağlantıya dönülür. İndirme sonunda SHA-256
doğrulanır; bozuk dosya kurulmaz. Çok yavaş bir bağlantıda ilerleme durursa birkaç dakika
bekleyin; launcher otomatik yeniden dener. Sunucu aynı yerel ağdaysa relay atlanır ve
dosyalar doğrudan yerel bağlantıyla çok daha hızlı iner.

**Dosyalar nerede durur?** indirilen mod dosyaları program klasöründe sunucu başına ayrı
saklanır: `Servers\<sunucu adı>__<kimlik>\`. Oyun klasörüne yalnızca oynarken eklenir; oyun
kapanınca oyun klasörü temizlenir, indirilen dosyalar kalır (tekrar indirme olmaz).

---

## 4. Mod ekleme (sunucuya)

**Sunucum** sekmesi → **Sunucu Modları** bölümü:

- **DLC Paketi Ekle (araç/ped)...**: `dlc.rpf` içeren paket klasörünü seçin (araç/ped/harita
  paketleri). İç içe klasörler (`x64\dlcpacks\...`) desteklenir.
- **OIV Paketi Ekle (.oiv)...**: OpenIV `.oiv` dosyalarını seçin. Her paket
  `Addons\oiv\<paket>\package.oiv` altında ayrı saklanır; oyunculara DLC paketleri gibi
  dağıtılır. Yalnızca güvenli işlemler uygulanır; desteklenmeyen işlemler atlanır.
- **İstemci Scripti Ekle (dll + ini/xml/pdb)...**: script dosyalarını ve **yanındaki destek
  dosyalarını** (ini, xml, pdb, json, txt, config...) hep birlikte seçin; hepsi birlikte
  oyuncuların `scripts/` klasörüne kurulur. Alt klasörlü scriptler için aşağıdaki seçeneği
  kullanın.
- **Script Klasörü Ekle...**: scriptin **kendi klasörünü** (dll + ini + xml + alt dosyalar)
  olduğu gibi ekleyin. Klasör adı script adı olur. Klasörün içinde fazladan bir `scripts\`
  katmanı varsa (indirilen arşivlerde sık görülür) launcher bunu otomatik ayıklar; oyuncuda
  gereksiz `scripts\scripts\` klasörü oluşmaz.
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

### Mod yöneticisi: seç, devre dışı bırak, kaldır

**Sunucum > Sunucu Modları** bölümünde kurulu modların listesi görünür:

- **Kurulu modlar (işaretli = etkin)**: her modun yanındaki onay kutusu o modu tek tek
  etkinleştirir/devre dışı bırakır. İşaret kaldırıldığında mod oyunculara gönderilmez; daha önce
  kurmuş oyuncular bir sonraki bağlantıda o modu otomatik kaldırır. Dosyalar sunucudan silinmez.
  Addons modları için değişiklik sunucu çalışırken de etkilidir.
- **Seçilenleri Kaldır...**: listeden bir veya birden fazla mod seçip kaldırır. Dosyalar
  silinmez; `Addons-Removed\<tarih-saat>\` altına taşınır. Sunucu kimliği
  (`.gtacoop-server.json`) korunur. Güvenlik için sunucu durdurulmuş olmalıdır.
- **Yenile**: listeyi diskten yeniden okur.

Liste DLC paketlerini, OIV paketlerini, istemci scriptlerini, ASI eklentilerini, istemci
dosyalarını ve sunucu eklentilerini gösterir. Sunucu eklentisi (plugin) değişiklikleri sunucu
yeniden başlatılınca etkili olur.

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
- OIV paketlerinde kurulan her dosyanın SHA-256 değeri saklanır; kaldırırken yalnızca
  launcher'ın yazdığı ve içeriği değişmemiş dosyalar silinir. Var olan farklı bir dosya
  hiçbir zaman ezilmez; `delete`/`replace` ve orijinal RPF düzenlemeleri uygulanmaz.
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
(varsayılan `bore.pub`; **Sunucum > Relay sunucusu** ile özel sunucu seçilebilir) açar ve
kısa koda relay uç noktalarını da ekler. Yönlendirici UPnP destekliyorsa oyun UDP ve sync TCP
portları ayrıca otomatik yönlendirilir; desteklemiyorsa relay yedek yol olarak kalır.
Arkadaşınızın launcher'ı önce relay'i dener; relay kurulamazsa yalnızca aynı alt ağdaki,
Radmin/Tailscale veya genel adresleri doğrudan dener. Böylece **port yönlendirme veya
Radmin VPN gerekmeden** internetten oynayabilirsiniz. Relay, doğrudan bağlantıdan biraz daha
yavaş olabilir; sunucu sahibi ve arkadaşın launcher'ı oyun sırasında **açık kalmalıdır**.
Sunucu açık ve internet bağlantınız çalışır olmalıdır.

**Uzun kod (`GTC1...`) — bağlantı bilgilerini kendisi taşır:**

1. Siz **Sunucum → Katılım Kodu Oluştur...** ile adresi seçip kodu kopyalayın,
   arkadaşınıza gönderin (WhatsApp/Discord). Sunucu açıkken relay hazırsa uzun koda da
   relay uç noktaları eklenir. **Kısa kod** ayarlıysa o da uzun koda eklenir; arkadaşınız
   bağlanırken relay bilgisi bu koddan yenilenir.
2. Arkadaşınız **Sunucular → Kod ile Katıl...** deyip kodu yapıştırsın ve
   **Ekle ve Bağlan**'a bassın. Uzun kod bağlantı bilgilerini kendi taşır; kısa kod
   araması gerekmez. Relay bilgisi yoksa doğrudan bağlantı denenir (şifre/token içerebilir,
   yalnızca güvendiğiniz kişiye verin).
3. Siz **Sunucuyu Başlat** deyin; arkadaşınız bağlanınca modlar otomatik iner.

**Not:** Uzun kodu sunucu açıkken oluşturursanız relay bilgisi de koda eklenir. Kısa kod
ayarlıysa uzun kod onu taşır ve sunucu her yeniden başlatıldığında relay portları bağlanırken
otomatik yenilenir; aynı uzun kod çalışmaya devam eder. Kısa kod yoksa uzun koddaki relay
bilgisi tek oturumluktur; sunucuyu yeniden başlattıysanız yeni kod oluşturun. Relay
kullanılıyorsa arkadaşınızın launcher'ı oyun boyunca açık kalmalıdır.

**IP ile (kod olmadan):**

1. Yönlendiricinizde port yönlendirme yapın: **4499 UDP** ve **4599 TCP** → kendi bilgisayarınız.
   **Sunucum** sekmesinde UPnP destekleniyorsa bu iki port otomatik yönlendirilir; otomatik
   yönlendirme başarısız olursa yukarıdaki elle ayarı kullanın.
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
