# GTACoop Remake — Türkçe Mod + Sunucu Yöneticisi

**Türkçe** | [English](README.md)

**2.14.0** - **koyu WPF arayüzü** (daraltılabilir yan menü, tutarlı koyu tema, beyaz şerit ve gri satır düzeltmeleri), **VFS/DLC sıkılaştırması** (sahipliği doğrulanan junction, güvenli yedek geri alma), **14 smoke test** ve ayrıca **AddonPed+**: launcher ve sunucudan bağımsız addon ped modu (kendi exesi, İngilizce oyun içi menüsü, OpenIV `.oiv` paketi, dlclist'te tek `gtacoop_peds` girdisi).

**2.13.2** - **WPF kabuğu**: aynı exe adı, kurulum klasörü, kısayollar ve güncelleyici; sekiz sekme WinForms olarak korunuyor.

2.13.1 sürümü - **güvenlik sertleştirmesi (manifest yolu, symlink, HTTPS, sabit zamanlı token), smoke testleri ve WPF hazırlığı**, 2.13.0 - **özel portlu sunucular LAN taramasında bulunuyor + "Sunucumu Başlat ve Gir" tek tuşu**, 2.12.0 - **HUD giriş/çıkış bildirimi (resim + ses), saniye cinsinden görünme süresi, F9'da resmin kaybolması, beyaz yazı ve Ctrl+Z menüsü**, 2.11.4 - **master TLS hatasında HTTP'ye düşme (sunucu tarayıcı düzeltmesi)**, 2.11.3 - **kendi sunucuna bağlanırken güncel oyun portu**, 2.11.2 - **gerçek PNG desteği ve kendi sunucuna bağlanma düzeltmesi**, **HUD görünürlük ve resim düzeltmesi**, **sunucu modu (VDS: launcher kapalıyken de çalışan sunucu, RAM sınırı)**, kendi sunucu yönetimi, otomatik mod indirme, **oturum modu**,
**kısa kod** (ör. `Gel.com`) ile internetten sunucu bulma, **otomatik relay**,
**UPnP doğrudan bağlantı**, **özel relay sunucusu**, **tanılama kopyalama**,
**canlı sunucu durumu**, **Sunucu Tarayıcı** sekmesi, **P2P mod paylaşımı** (sunucusuz,
**OpenIV (.oiv) paket desteği**,
**mods klasöründeki RPF arşivlerine yerinde ekleme/değiştirme**, **Bileşenler** sekmesinden
ScriptHookV / ScriptHookVDotNet / OpenIV.asi / RageOpenV kurulumu, **F9 oyuncuya ışınlanma**
ve **sunucudan gelen addon pedler**:
port yönlendirme veya Radmin VPN olmadan da arkadaşlarınız bağlanabilir. Sunucu modları
yalnızca oyun açıkken kurulu kalır, oyundan çıkınca otomatik kaldırılır.
Oyun başlatma dosyası seçilebilir; varsayılan **PlayGTAV.exe**'dir (GTA5.exe de seçilebilir).
**Sesli sohbet** (bas-konuş, `[MIC]` göstergesi) ve **host-led hikaye görevi koordinasyonu**
bu sürümde eklendi; ayrıntılar aşağıda.

### 2.13.4 – 2.14.0 — Koyu WPF arayüzü ve VFS sıkılaştırması

- **DLC paketleri sahipliği doğrulanan junction ile bağlanıyor.** mount/unmount, hedef kontrolü ve yedek
  geri alma sıkılaştırıldı; sahipliği kanıtlanamayan geçici/yedek klasörlere dokunulmuyor ve çökme
  sonrası tek güvenli yedek geri alınıyor. Kaldırma artık cache dosyalarını silmiyor; cache temizliği
  ayrı bir işlem.
- **OIV içeriği OpenIV mods katmanına yönlendiriliyor.** OIV, ASI ve scriptler için gerçek zamanlı
  sanallaştırma yok. Ayrıntı: `VFS-TEKNIK-ANALIZ-TR.md`.
- **Daraltılabilir yan menü:** daraltınca yalnızca simgeler ve araç ipuçları kalıyor, içerik alanı genişliyor.
- **Tutarlı koyu tema:** açılır kutular, pasif düğmeler, liste satırları ve ızgara çizgileri koyu;
  sütunların sağındaki beyaz şerit kapandı, üzerine gelinen satır siyah olmuyor ve pencere ekranın
  çalışma alanına sığıyor.
- Grup başlıkları ve açıklamalar tam okunuyor, tekrar eden düğmeler tek satıra indirildi ve oyun durumu
  sekmesi açıldığında kendiliğinden yükleniyor.
- Sunucular, Sunucu Tarayıcı, Oyun ve Bileşenler metinleri çevrildi; yeni WPF metinleri için İngilizce ve
  Rusça sözlük anahtarları eklendi.
- `tests/GTACoop.Sync.SmokeTests`: 14 test (yol güvenliği, manifest doğrulama, DLC junction
  mount/unmount, sembolik bağ reddi).

### 2.13.2 — WPF kabuğu

- **Launcher artık WPF kabuğu:** aynı exe adı, aynı kurulum klasörü, aynı masaüstü/başlat menüsü
  kısayolu ve aynı güncelleyici dosya adı, yani güncelleme ek adım gerektirmiyor. Ayarlar, sunucu
  listesi, oturumlar ve günlükler `%LOCALAPPDATA%\GTACoopRemake` içinde kalıyor.
- **Mevcut sekmeler çalışmaya devam ediyor:** sunucu, oyun, P2P, HUD tasarımcısı, bileşenler, Addon Peds
  ve günlük sayfaları yeni kabuk içinde WinForms denetimleri olarak barınıyor.
- **Açılış hatası düzeltildi:** kaynak sözlüğünde `Muted` iki kez tanımlıydı; stil artık `MutedText`.
- 2.13.1'in güvenlik çalışmaları ve 13 kontrollü smoke test projesi dahil. Önceki WinForms launcher
  derlenebilir kalıyor; kurulum eski exeyi `GTACoop.Launcher.pre-wpf.bak` olarak yedekliyor.

### 2.13.1 — Güvenlik sertleştirmesi, smoke testleri ve WPF hazırlığı

- **Manifest yolu güvenliği.** Eklenti manifest yolları kanonikleştirilip doğrulanıyor: mutlak yollar,
  `..` parçaları, Windows ADS (`:`), aygıt adları, geçersiz karakterler ve tekrarlanan kayıtlar indirme
  başlamadan reddediliyor. Cache veya oyun klasöründeki sembolik bağ/junction bileşenleri kabul edilmiyor.
- **Sunucu taramaları bağlantıları izlemiyor.** Sunucu eklentisi taraması ve AddonPeds keşfi sembolik
  bağ/junction hedeflerini atlıyor, böylece eklenti klasörü dışındaki dosyalar hiç yayınlanmıyor.
- **Türe göre cache anahtarı.** Cache klasörleri eklenti türünü ve kimlik parmak izini içeriyor; artık
  `scripts/abc` ile `files/abc` çakışmıyor. Çakışan DLC mount adları uyarıyla manifest dışında bırakılıyor.
- **Sync sertleştirmesi.** `https://` adresleri HTTPS kalıyor, sync portu ve host doğrulanıyor, ping
  yanıtları temizleniyor, kullanıcı iptali artık zaman aşımı olarak raporlanmıyor ve `X-Sync-Token`
  sabit zamanlı karşılaştırılıyor (token/TLS yokken konsol uyarısı).
- **Testler ve CI.** Bağımlılıksız, 13 kontrollü smoke test projesi ve bir Windows GitHub Actions iş akışı.
- **WPF hazırlığı.** Aynı `GTACoop.Launcher.exe` adı ve eşitlenmiş sürüm; dağıtılan yapı hâlâ WinForms launcher.
- **ZIP SHA256:** `3A5FDDC03E5C75C1596DE0756271101A9F060E1171C49CDAAF18977D14ECC046`

### 2.13.0 — Özel portlu sunucular LAN'da, tek tıkla başlat ve gir

- **Özel portlu sunucular LAN'da bulunuyor.** LAN yayını yalnızca 4499/4498/44499 deniyordu, bu yüzden
  4535 portundaki sunucu hiç görünmüyordu. Tarama artık kendi sunucunuzun ayarlı oyun portunu ve kayıtlı
  sunucuların portlarını da deniyor.
- **"Sunucumu Başlat ve Gir" düğmesi:** tek tıkla sunucunuzu başlatır, sync portunu bekler, 127.0.0.1'i
  sunucu listesine ekler ve otomatik bağlanır (mod senkronizasyonu dahil).
- **ZIP SHA256:** `9A96BE8D9A702E0981E2098F7C47E8DA2112747EF6572474F98EB0E515FBA796`

### 2.12.0 — HUD bildirimleri, süreli öğeler ve F9 resim düzeltmesi

- **F9 artık PNG'leri gerçekten gizliyor.** HUD gizlenince sprite'lar kapatılıyor, böylece resimler ekranda
  kalmıyor. Yeni eklenen resimler varsayılan olarak HUD ile birlikte gizleniyor.
- **Öğe başına görünme süresi:** HUD tasarımcısında saniye girin; süre dolduktan sonra o öğe oturum boyunca
  bir daha çizilmez (0 = süresiz).
- **Oyuncu giriş/çıkış bildirimi:** bir PNG ile bir WAV/MP3 seçin (Arayüz Tasarımı → Oyuncu giriş/çıkış
  bildirimi), saniyeyi ayarlayın; ekranın üstünde yumuşak geçişle görünür, ses isteğe bağlıdır.
- Diyalog pencereleri artık koyu temayı izliyor, etiketler koyu zeminde siyah görünmüyor.
- Metin kutularında sağ tık: Geri Al (Ctrl+Z), Kes, Kopyala, Yapıştır, Tümünü Seç.
- **ZIP SHA256:** `2216625EACC55228CDD2007B526635B12D19105A31697D46ED7732BEEDF9E6B3`

### 2.11.4 — Master TLS bozukken sunucu tarayıcı çalışıyor

- **Sunucu tarayıcı genel listeyi yeniden yüklüyor.** `https://master.gtacoop.com` bir Cloudflare TLS
  hatası (526) döndürdüğü için master listesi hiç indirilemiyor, yalnızca yedek liste kullanılıyordu.
  HTTPS master başarısız olunca launcher artık aynı adresi düz HTTP ile bir kez daha deniyor.
- Kısa kod günlüğü, kayıtlı relay bilgisi olmayan bir kayıt için "saklandı" demiyor.
- **ZIP SHA256:** `2C9DA0374C9E83B97AD768A59605F4B778B98D1FF5179107307A528B97C4495D`

### 2.11.3 — Kendi sunucunuza bağlanırken güncel oyun portu

- **Kendi sunucunuza bağlanma düzeltildi.** Kayıtlı bir giriş (ör. kısa koddan eklenen) varsayılan oyun
  portunu tutarken sunucu başka porta taşınmışsa istemci kimsenin dinlemediği porta bağlanıp "no response
  from remote host" diyordu. Launcher artık canlı sunucu ayarlarını okuyor ve kendi sunucunuz bu makinede
  farklı bir portta dinliyorsa o portu kullanıp kaydediyor.
- Aynı kontrol modsuz bağlanma yolunda da çalışıyor; başkalarının sunucularına bağlanma değişmedi.
- **ZIP SHA256:** `4A2A9F3ADC29CCD274C9C4EACAA88F0EB0FAEE35203C643CA161924077CFD8FD`

### 2.11.2 — Gerçek PNG HUD resimleri ve kendi sunucunuza bağlanma

- **Gerçek PNG HUD resimleri:** istemci artık her PNG'yi ScriptHookVDotNet'in `CustomSprite`'ı ile
  çiziyor; dosya doku olarak yüklenip tek çağrıda tam çözünürlükte çiziliyor (renkli dikdörtgenlerden
  yeniden kurulmuyor). Eski ScriptHookVDotNet sürümleri otomatik olarak önceki çiziciye düşüyor.
- **Launcher'dan kendi sunucunuza bağlanma:** oyun portu bu makinede zaten açıksa launcher kayıtlı
  genel/relay adresi yerine `127.0.0.1` üzerinden bağlanıyor. NAT loopback kapalı yönlendiricilerde
  görülen "Failed to establish connection - no response from remote host" hatası bu şekilde çözülüyor.
- **ZIP SHA256:** `D87CB73B725C3FE53C5E17854C333DD047C12431DCC2342BA40CE0EB70126070`

### 2.11.1 — HUD görünürlüğü, eksiksiz PNG'ler ve sunucu onarımı

- Menü kapalıyken oyun içi HUD yeniden çiziliyor: "menü açık" bayrağı ters aktarıldığı için "her zaman
  görünür" işaretlenmemiş tüm öğeler F9'a basılana kadar gizli kalıyordu.
- **"Her zaman görünür"** artık çalışıyor: launcher bayrağı `alwaysVisible` olarak kaydederken oyun içi
  çizici `always` alanını okuyordu. Artık iki ad da okunuyor, eski sürümlerle kaydedilen düzenler bozulmuyor.
- **PNG resimleri tam çiziliyor:** pişirilmiş bir resim 5000'den fazla hücre olabiliyor ve oyun kare başına
  çizim sınırını aşınca resmin altı kesiliyordu. Satırlar aynı renk şeritlerine birleştiriliyor ve ızgara
  yalnızca gerektiği kadar kabalaştırılıyor (64×91 logo: 5327 → 1415 çizim), güvenlik için kare başına
  sınır uygulanıyor.
- **Sunucu modu:** yarım kopyalanmış sunucu klasörü başlangıçta onarılıyor. `gtaserver.core.exe` varken
  `gtaserver.core.runtimeconfig.json` ve diğer şablon dosyaları yoksa adanmış sunucu hiç açılmıyor,
  kimse bağlanamıyor ve sunucu tarayıcı hiçbir şey bulamıyordu.
- Yerelde doğrulandı: sunucu modu oyun sunucusunu ayarlı portta başlatıyor ("ready to accept connections"),
  sync sunucusu `/api/v1/ping` isteğine 200 dönüyor, yeniden başlatma döngüsü yok ve kapanış temiz.

### 2.11.0 — Sunucu modu (VDS) ve zengin HUD

#### Sunucu modu (VDS) - isteğe bağlı, varsayılan kapalı

- Adanmış sunucu (`gtaserver.core` + eklenti sync sunucusu) artık **GTA V olmadan ve launcher açık
  değilken** çalışabiliyor. FiveM FXServer ile aynı model: oyuncular *Sunucular* sayfasından `IP:port` ile
  bağlanır, kimsenin oyunda olması gerekmez.
- **Sunucu süreci başına RAM bütçesi** (0 = sınırsız); Windows job object ile uygulanır ve bütçenin
  %75'inde çalışma kümesi kırpılır.
- Sunucu süreçlerinden biri kapandığında **otomatik yeniden başlatma**; host PID'i, RAM sınırı, iki sürecin
  anlık RAM'i ve yeniden başlatma sayacını gösteren canlı durum satırı.
- İsteğe bağlı **Windows ile başlat** oturum açma görevi (aynı sayfadan kaldırılabilir) ve tek düğmeyle
  açılabilen, döndürülen `logs/server-host.log`.
- Sunucu modu açıkken launcher'ı kapatmak sunucuyu durdurmuyor; launcher'ı yeniden açtığınızda ikinci bir
  kopya başlatmak yerine çalışan sunucuyu gösteriyor.
- Sunucu modunu kapatın (veya **Durdur**'a basın), sunucu eskisi gibi launcher tarafından yönetilir. Oyun
  veya sunucu dosyalarına hiçbir şey yazılmaz.

#### Arayüz tasarımcısı

- Yeni şekiller: **dış çizgi, daire, çizgi, degrade**; yeni canlı göstergeler: **hız, silah + mermi,
  pusula, bölge/sokak, koordinatlar, FPS**.
- Öğe başına **her zaman görünür**: PNG logolar ve katmanlar, HUD kapatma tuşuyla veya launcher menüsü
  açıkken gizlense bile ekranda kalır.
- PNG resimleri daha yüksek, en-boy oranına duyarlı ızgara çözünürlüğünde pişiriliyor ve her karede değil bir
  kez çözümleniyor: daha net logolar, çizerken daha az CPU.
- Kısayol tuşlarında bir karelik takılma ve sağa dayalı metinlerin sonraki metinlere taşması düzeltildi.

> Sunucu modu paylaşım ZIP'i içinde gelir (`tools/ServerHost`). Yalnızca launcher exesini değiştirirseniz
> host bileşeni için klasörün geri kalanını da koruyun.

### 2.10.0 — Özel HUD tasarımcısı (isteğe bağlı)

**Yeni sayfa:** Arayüz Tasarımı. Paneller, metin, saat, ping, oyuncu listesi, sağlık/zırh çubukları ve PNG
resimleri 16:9 önizlemeye yerleştirin; oyunda aynı konumda çizilir.

- Düzen, launcher veri klasöründe `ui/hud.json` olarak saklanır ve oyun içi çizici değişiklikleri iki
  saniye içinde alır; yeniden başlatma gerekmez.
- PNG resimleri kaydederken kompakt bir renk ızgarasına pişirilip dikdörtgen olarak çizilir
  (ScriptHookVDotNet'in dosyadan doku API'si yok).
- **Varsayılan kapalı.** Tek anahtar özel HUD'u açar; *Kapat ve varsayılana dön* kapatır ve oyun HUD'unu
  doğrudan geri verir.
- Klavye kısayolları her biri tek iş yapar: HUD'u aç/kapat, oyuncu listesi, sesli sohbet, radar, arayüzü
  gizle.
- Oyun HUD'u parça parça değiştirilebilir: tüm HUD, radar, yalnızca harita resmi (radar çerçevesi kalır),
  aranan yıldızları, sokak adları, araç bilgisi, yardım metni.
- Oyun klasörüne veya sunucuya hiçbir şey yazılmaz. HUD değişiklikleri kare kare uygulanır ve özellik
  kapatıldığı anda bırakılır; tek oyunculu ve GTACoop oturumları bozulmaz.

### 2.9.0 — P2P mod paylaşımı

- Yeni **P2P Paylaşım** sayfası: bir mod klasörü doğrudan arkadaşa açılır (yerel ağ, hotspot veya
  Radmin/Tailscale gibi sanal ağ). Sunucu, relay veya hesap gerekmez.
- Paylaşan kişi `IP:port` kodu alır; arkadaş kod ile bağlanıp dosya listesini görür ve indirir. Dosya
  başına ilerleme ve iptal düğmesi vardır.
- Her dosya SHA256 ile doğrulanır; hedefte aynı dosya zaten varsa yeniden indirilmez (artımlı eşitleme).
- Sayfa ayrıca klasörü `.zip` olarak dışa aktarabilir, paylaşım portu için güvenlik duvarı kuralı
  ekleyebilir ve son kodu/portu/klasörleri hatırlar.
- Düzeltme: oyundaki **Bileşenleri Yönet...** düğmesi ve Sunucu Tarayıcı'daki **Sunucular** bağlantıları
  hiç sayfa değiştirmiyordu; görünmeyen TabControl yerine görünen sayfa listesi kullanılıyor.
- Not: Oyun içi test bu makinede yapılmadı. P2P aktarımı 3 MB'lık test klasörüyle uçtan uca doğrulandı
  (hash eşleşmesi + artımlı eşitleme).

### 2.8.4 — Arayüz düzeltmeleri

- İçerik alanı artık başlık çubuğunun altından başlıyor; her sayfanın üstü (Hakkında sayfası, başlığı ve
  avatarı dahil) başlığın altına kaymıyor.
- Grup çerçeveleri Windows yerine launcher tarafından çiziliyor; koyu zemin ve yeşil başlık korunuyor,
  parlak beyaz kenarlıklar gitti.
- Hakkında sayfası bölümleri sabit boşluklarla yukarıdan aşağı yerleştiriliyor; hiçbir şey üst üste
  binmiyor veya kesilmiyor.
- Windows desteklediği yerde yerel kaydırma çubukları koyu görünümü izliyor.
- Oynanış, mod veya kayıt verisinde değişiklik yok.

### 2.8.3 — Yan çubuk navigasyonu ve güvenilir sayfa geçişi

- Menü artık solda dikey bir yan çubuk; etkin öğede yeşil işaret ve üzerine gelme durumu var.
- İçerik alanı daha geniş ve uzun sayfalar kendi içinde kaydırılıyor.
- Sayfa geçişi gizli TabControl'e bağlı değil (seçim olayını hiç tetiklemiyordu, bu yüzden menü
  tıklamaları hiçbir şey yapmıyordu); seçimi artık form kendisi yönetiyor ve her menü öğesi kendi
  sayfasını açıyor.
- Başka davranış değişmedi; ayarlar, sunucular, modlar ve kayıtlar korunuyor.

### 2.8.2 — Navigasyon çubuğu, tamamen koyu arayüz

- Sekme şeridi içeriğin üstünde bir navigasyon çubuğuyla değiştirildi; Windows'un sekme başlıklarının
  yanına çizdiği sistem renkli bant gitti.
- Etkin sayfa yeşil vurgulanıyor, pasif öğeler soluk gri.
- Koyu yüzeyler artık saf siyah değil koyu gri tonlarında; göz için daha rahat.
- Düzen, metinler ve davranış aynı; ayarlar, sunucular, modlar ve kayıtlar korunuyor.

### 2.8.1 — Yeşil vurgulu saf siyah tema

- Her yüzey, panel, liste başlığı, liste satırı ve onay kutusu siyah; pencerede beyaz alan kalmadı.
- Vurgu rengi yeşil: sekme alt çizgisi, grup başlıkları, form numaraları, onay işaretleri ve üzerine gelme.
- Pencere başlığı, işletim sistemi desteklediğinde Windows koyu başlık çubuğunu kullanıyor.
- Düzen, metin veya davranış değişikliği yok; ayarlar, sunucular, modlar ve kayıtlar korunuyor.

### 2.8.0 — Yeni arayüz, hava kilidi, çakışma tespiti ve mod detayları

- **Yeni arayüz:** launcher genelinde koyu, düz tema: kompakt başlık, vurgu alt çizgili sekme şeridi,
  temalı listeler, giriş alanları, düğmeler ve grup kutuları. Düzen, metinler ve davranış değişmedi;
  yalnızca renkler uygulanıyor ve anlamlı durum renkleri (yeşil/kırmızı) korunuyor.
- **Hava kilidi:** **Sunucum → Hava:** dinamik (oyunun kendi döngüsü) veya sabit hava: ekstra güneşli,
  açık, bulutlu, kapalı, yağmurlu, fırtınalı, sisli, puslu, karlı, tipi, yılbaşı. Saat gibi manifest
  üzerinden dağıtılır; misafirlerin ayar yapması gerekmez, eski istemciler yok sayar. Sunucudan çıkınca
  zorlama kalkar.
- **Script çakışma tespiti:** launcher üzerinden bağlanmadan önce sunucunun script/ASI dosyaları sizin
  `scripts`, `asi` ve `lua` klasörlerinizdeki dosyalarla karşılaştırılır; olası çakışmaları listeleyen net
  bir uyarı çıkar ve devam etme veya iptal seçeneği sunulur. Salt okunur: hiçbir şey yazılmaz, taşınmaz
  veya silinmez.
- **Daha zengin tek oyunculu paketler:** paketler ad, yazar, sürüm, kategori ve açıklama içeren `mod.json`
  (veya basit `mod.ini`) getirebilir. Liste bu sütunları gösterir ve seçili paketi bir detay bölmesi anlatır.
- **Kendi relay sunucunuz:** `relay/` klasöründe `docker-compose.yml` ve kendi `bore` relay'inizi VPS'te
  çalıştırmak için kısa bir İngilizce rehber var.
- **Kurulum:** `GTACoop.Launcher.exe` dosyasını indirip çalıştırın veya paylaşım ZIP'ini kullanın.

### 2.7.7 — Ped değişimi, sunucu saati ve tek oyunculu mod yöneticisi

- **"Ped uygulanamadı" hatası giderildi.** Değişim hazırlığı her karede yeniden zamanlanıyordu ve
  süre hiç dolmuyordu. Artık hazırlık bir kez yapılır, model 20 saniyeye kadar beklenir, modelin
  oyunda gerçekten bulunduğu doğrulanır (`IS_MODEL_IN_CDIMAGE`, `IS_MODEL_VALID`, `IS_MODEL_A_PED`)
  ve ancak ondan sonra `SET_PLAYER_MODEL` çağrılır. Hata mesajı artık nedenini söyler.
- **Sunucu saati.** *Sunucum* sekmesinde **Saat** ayarı: kapalı (oyun saati), **gerçek zamanlı**
  (bilgisayar saatinizi izler) veya **sabit saat**. Ayar manifest üzerinden bağlanan herkese dağıtılır;
  ağ protokolü değişmez ve eski istemciler etkilenmez. Sunucudan çıkınca saat zorlaması kalkar.
- **Tek Oyunculu sekmesi.** Launcher'ın yanında `Singleplayer\Mods` klasörü oluşur. Paket ekleyip
  etkinleştirdiğinizde dosyalar oyun klasörüne kopyalanır; devre dışı bıraktığınızda yalnızca eklenen
  dosyalar silinir, üzerine yazılanlar yedekten geri gelir. Oyunun .rpf arşivleri ve mevcut dosyaları
  değiştirilmez; yalnızca scripts, asi, mods, plugins ve lua klasörlerine yazılır.

### 2.7.6 — Addon ped çökmeleri, port kontrolü ve sıralama

- **Addon ped çökmesi giderildi.** Model değişimi artık menü tıklamasında değil, oyun döngüsünde
  güvenli anda yapılıyor: görevler bırakılır, hız sıfırlanır, ped bir kare sakin kalır, sonra model
  değişir. Havada, ragdoll'da veya araçtayken değişim reddedilir ve kısa süre sonra yeniden denenir.
- **Varyasyon koruması.** Drawable/texture sayısı sıfır olan pedlere varyasyon uygulanmaz; her native
  çağrısı pedin hâlâ var olduğu ve hedef modelde olduğu doğrulandıktan sonra yapılır. Animasyon ve
  clipset uygulaması model değişimi boyunca tamamen durur.
- **Port kontrolü.** *Sunucum* sekmesinde **Port Kontrolü**: oyun (UDP) ve sync (TCP) portları başka bir
  uygulama tarafından kullanılıyorsa uyarır. Sunucu başlatılırken de aynı kontrol yapılır; sessiz
  başarısızlık yerine anlaşılır bir mesaj gösterilir. **Yeniden Başlat** düğmesi eklendi.
- **Sunucu listesinde sıralama.** *Sırala: Favoriler / Ad / Ping / Son girilenler*.

### 2.7.5 — Oyuncu listesi, moderasyon ve kasma düzeltmesi

- **Oyuncular paneli.** *Sunucum* sekmesinde canlı oyuncu listesi (ad, ping, sağlık). Liste sunucu
  eklentisinin yazdığı `gtacoop-players.json` dosyasından okunur; ağ protokolü ve oyun dosyaları
  değişmez, oyun tarafında hiçbir ek iş yapılmaz.
- **Kontrol.** Seçili oyuncuyu *At (Kick)*, adını yasakla, tek oyuncuya mesaj gönder veya sunucudaki
  herkese *Duyuru* yap. *Yasak / Beyaz Liste* penceresinde yasak listesi ve "yalnızca beyaz
  listedekiler girebilsin" seçeneği yönetilir; yasaklı bir ad bağlanınca otomatik atılır.
- **Kasma/lag düzeltmesi.** 2.7.4'te her karede çizilen HUD tamamen kaldırıldı. Oyun içi döngüde
  artık ek çizim, LINQ ve metin ayırma yok; güncellemeden sonra başlayan takılmalar bu yüzeydi.
- **Sunucu tarayıcıda arama.** Sunucu listesini ad, adres veya kısa koda göre anında filtreler.

### 2.7.4 — Güncelleme, destek paketi, sunucu yedeği, favoriler ve HUD

Protokol değişmedi; hepsi launcher/istemci tarafında:

- **Kendi kendini güncelleme.** Açılışta GitHub'daki en son sürüm sorulur ve Hakkında sekmesinde
  gösterilir; *İndir ve Kur* yeni exe'yi indirip yerine koyar ve launcher'ı yeniden başlatır.
  Yalnızca launcher exe'si değişir — oyun dosyaları, ayarlar, önbellek ve sunucu klasörleri
  ellenmez; değiştirme başarısız olursa eski exe geri konur.
- **Destek paketi.** Günlük / Araçlar sekmesinde *Destek Paketi (.zip)*: sürüm, sistem, oyun
  klasörü durumu, launcher günlükleri ve oyun tarafı günlükleri (ScriptHookVDotNet, asiloader,
  GTACoOp) masaüstünde tek zip. Parola ve token yazılmaz.
- **Sunucu yedeği.** Sunucum sekmesinden sunucu ayarları, Addons klasörü ve sunucuya özel mod
  klasörleri tek zip'e alınır; başka makinede geri yüklenir. Geri yüklemeden önce mevcut durumun
  güvenlik yedeği alınır, oyun klasörüne dokunulmaz.
- **Sunucu listesinde favoriler.** *Favori ★* ile işaretlenen sunucular üstte listelenir;
  *Sadece favoriler* diğerlerini gizler.
- **Oyun içi HUD.** F9 > Ayarlar > *HUD (FPS / Ping)* köşede FPS, ortalama ping ve oyuncu
  sayısını gösterir (varsayılan açık).

### 2.7.3 — Eklenen köpek ped meta'sıyla yürüyor

Launcher'ın hayvan pedi için yazdığı ped meta'sı hareket verisini kendisi taşıyor:
`<ClipDictionaryName>creatures@<ırk>@move` ve `<MovementClipSet><ırk>@move`. Böylece launcher'dan
eklenen köpek doğduğu anda yürüyor; oyun içi menüden bir şey seçmek gerekmiyor.

Meta'nın arkasındaki şablon vanilla retriever tanımıydı; ped retriever setiyle kalıyor ve kendi
hareketi olmuyordu. Artık varsayılan rottweiler (Chop) seti ve ırk model adından okunuyor:
shepherd, husky, retriever, pug, westy, poodle ve kedi kendi clipset'ini alıyor, tanınmayan
modeller rottweiler'a düşüyor. Paket kendi meta'sını getiriyorsa yalnızca boş / `null` hareket
alanları doldurulur, mod yapımcısının clipset'i ezilmez. Clipset verilip sözlük verilmezse
sözlük clipset'ten türetilir. Protokol değişmedi.

### 2.7.2 — Hayvan hareket (yürüme/koşma) setleri

Hayvan menüsü artık yalnızca duruş değil **hareket** seti listeliyor. Her satır bir ırk:
Rottweiler (Chop), Shepherd, Husky, Retriever, Pug, Westy, Poodle ve kedi. Seçilen ırkın
kendi movement clipset'i (`ROTTWEILER@MOVE`, `SHEPHERD@MOVE`, …) ve
`CREATURES@<IRK>@MOVE` klip sözlüğü uygulanıyor; hayvan scriptli bir pozda durmak yerine o
ırk gibi yürüyüp koşuyor. Clipset ve sözlük adı her menü satırında yazılı.

Clipset ancak klip sözlüğü gerçekten yüklendikten sonra uygulanıyor. Kurulu oyun sürümünde
bir ırkın sözlüğü yoksa istemci rottweiler setine düşüyor (kedi modelleri `CAT@MOVE`
üzerinde kalır), loga yazıp bildirim gösteriyor; tanınmayan bir clipset oyuna hiç
gönderilmiyor. Kedi iskeleti farklı olduğu için köpek setleri kediye, kedi seti köpeğe
uygulanmıyor. Otur/havla/ısır/kalk duruşları **Duruş animasyonları** başlığı altında duruyor.

### 2.7.1 — Hayvan pedi varsayılan hareketi

Hayvan pedi artık duruş animasyonunda kalmıyor. Varsayılan sözlük **rottweiler
hareket sözlüğü** ve `ROTTWEILER@MOVE` klip seti; tüm köpek modelleri bu iskeleti
paylaştığı için shepherd, husky ve pug da yürüyebiliyor. İkisi de yüklenemezse
istemci güvenli sözlüğe düşüp bunu bildiriyor.

### 2.7 — Legacy RPF kurulumu, hayvan animasyonları ve ped değişimi çökmesi

Bu sürüm, addon DLC paketlerinin **Legacy** kurulumda kurulamaması sorununu ve addon
ped değiştirirken oluşan çökmeleri hedefler.

- **Legacy'de RPF artık kuruluyor.** `mods/update/update.rpf` için sağlık/boyut
  sezgileri kurulumu **engellemiyordu ama yanlış pozitif verdiğinde paketi tamamen
  blokluyordu**. Artık engel kaldırıldı: arşiv açılabiliyorsa dlclist yazılır, yedek
  alınır, gerekirse içerik arşivin sonuna eklenir ve **sonuç arşiv yeniden açılıp
  doğrulanır**. Yalnızca arşiv hiç açılamıyorsa veya 3,9 GB sınırı yüzünden hiçbir
  yazma sığmıyorsa paket atlanır. Bozuk arşive yine yazılmaz.
- **Başlık alanı taşması düzeltildi.** TOC/isim tablosu büyüdüğünde yazma
  `RpfHeaderSpaceException` ile düşüyordu; artık içerik arşivi büyütmeden mevcut
  alana yazılır. Bu, retail `update.rpf` kopyalarında görülen bir durumdur.
- **Yazma doğrulaması.** dlclist yazıldıktan sonra arşiv yeniden açılır ve beklenen
  tüm paket adları okunur; eksik varsa paket "kuruldu" diye kaydedilmez.
- **Addon ped değiştirirken çökme düzeltildi.** `SET_PLAYER_MODEL` pedi yeniden
  yarattığı için art arda değişimler eski handle'ları geçersiz bırakıyordu. Artık:
  değişim sürerken veya son 0,9 sn içinde değişim yapıldıysa yeni istek reddedilir,
  ölmüşken/ragdoll'dayken değişim yapılmaz, değişimden önce görevler ve ragdoll
  bayrağı temizlenir, model ped değilse (`IS_MODEL_A_PED`) hiç denenmez ve **aynı
  pedi tekrar seçmek modeli yeniden yaratmaz** (yalnızca varyant onarılır).
- **Hayvan animasyon menüsü.** Ana menüye **Animal Animations** eklendi. Seçili
  hayvan modeline göre set listelenir: **Rottweiler (tüm köpekler)**, **Retriever**
  ve **Kedi**. Köpek ırkları aynı iskeleti paylaştığı için shepherd/husky/pug gibi
  kendi sözlüğü olmayan modellerde rottweiler sözlükleri kullanılır; kedi
  modellerine köpek animasyonu oynatılmaz. Animasyonlar: otur, havla, yat, ısır,
  kalk ve durdur. Sözlük yüklenemezse güvenli sözlüğe düşülür ve bildirim gösterilir;
  `melee@streamed_core@` içindeki `victim_*` klipleri (insan içindir) kullanılmaz.

### 2.6.2 — Çift ısırık ve zeminden geçme düzeltildi

Hayvanla saldırmanın iki sorunu giderildi.

- **Tek basış artık tek ısırık.** Saldırı tuşu kısa devre (`||`) ile okunuyordu; sol tık
  kontrolü çoğu karede hiç okunmuyor, Windows'un "son okumadan beri basıldı" biti
  birikiyor ve bekleme süresi bitince kendiliğinden ikinci bir ısırık üretiyordu.
  Artık tuş durumu her karede okunuyor ve ısırık yalnızca **yeni basışta** (tuş
  bırakılıp yeniden basıldığında) tetikleniyor; tuş basılı tutulsa bile tekrar etmiyor.
- **Animasyon kendini kesmiyor.** Aynı ısırık animasyonu hâlâ oynarken yenisi
  başlatılmıyor, bekleme süresi 0,9 sn'ye çıkarıldı.
- **Yüksekten düşünce zeminden geçme.** Ragdoll'u kapalı hayvan bazen zeminden geçip
  haritanın altına iniyor ve düşmeye devam ediyordu. Artık hayvan zemine gömülürse
  zeminin üstüne, haritanın çok altına inerse son güvenli konumuna geri alınıyor
  (hız sıfırlanır). Koruma yalnızca gerçekten düşerken çalışır; köprü veya üst geçit
  altında duran hayvanı yanlışlıkla yukarı çekmez.
- 2.6.1'in ısırık senkronu ve 2.6.0'ın ragdoll çökme koruması aynen duruyor;
  **ağ protokolü değişmedi** (`VERSION_0_9_4`), eski istemcilerle birlikte oynanabilir.

### 2.6.1 — Hayvan pediyle saldırma (ısırma) geldi

Addon veya vanilla bir hayvan modelindeyken **saldırı tuşu artık çalışıyor**: karakter
vanilla köpek melee animasyonunu oynatıp önündeki en yakın pedi ısırıyor.

- **Neden hiçbir şey olmuyordu?** Model değişiminde silahlar temizleniyor ve oyunun hayvan
  kontrol setinde insan melee görevi yok; saldırı tuşu boşa basılıyordu.
- **Isırık.** `creatures@rottweiler@melee@streamed_core@` sözlüğündeki `attack` animasyonu
  oynatılır; hayvanın önünde 2,6 m içindeki en yakın ped 20 hasar alır ve kısa süre
  sendelemeye girer. 0,8 sn bekleme süresi vardır.
- **Diğer oyuncular da görür.** `PedData` bayraklarına `IsAttacking` biti eklendi; karşı
  istemci uzak hayvanda aynı ısırık animasyonunu oynatır. Yeni bir bit olduğu için eski
  istemciler yok sayar; **ağ protokolü değişmedi** (`VERSION_0_9_4`).
- **Tuş algılama.** Oyunun saldırı kontrolü ve yedek olarak sol tık okunur; menü/sohbet
  açıkken, araçtayken veya ölüyken saldırı tetiklenmez.
- 2.6.1 istemciler 2.6.0, 2.5.9 ve daha eski istemcilerle aynı sunucuda oynayabilir.

### 2.6.0 — Addon hayvanlarda ragdoll çökmesi düzeltildi

Bu sürüm, addon hayvan pedi **düşünce veya bir araç çarpınca** oluşan çökmeyi iki
katmanda düzeltir: hayvan meta verisi artık oyunun kendi hayvan tanımından kopyalanıyor
ve ragdoll verisi doğrulanmamış hayvanlarda çökme koruması devreye giriyor.
Replace edilen hayvanlarda sorun olmamasının nedeni, oyunun o model için zaten doğru
ragdoll verisini bilmesiydi; addon modelde bu veriyi biz üretiyorduk.

- **Şablon artık vanilla hayvan meta verisi.** Elle uydurulan kısa şablon yerine oyunun
  kendi köpek pedi tanımının birebir kopyası kullanılıyor: `PoseMatcherProneName`,
  `TaskDataName`, `NavCapabilitiesName` (`STANDARD_QUADRUPED`), `DefaultBrawlingStyle`,
  `DefaultUnarmedWeapon`, `AnimalAudioObject`, `MinActivationImpulse` ve offset alanları
  artık dolu. Eksik alanlar ragdoll sırasında oyunu çökertiyordu.
- **Doğrulanmamış meta veride ragdoll kapalı.** Pedin meta verisi gerçek bir hayvan
  tanımından gelmiyorsa (paket kendi meta verisini sağlamıyor, temel model meta verisi de
  bulunamıyorsa) `auto` ayarı o hayvanın ragdoll'unu kapatır ve çarpışmada ragdoll'u da
  kapatır. Hayvan görünür ve animasyonlu kalır, yalnızca düşmez. **on** seçilirse ragdoll
  zorla açılır.
- **Gerçek meta veri varsa ragdoll açık kalır.** Paket kendi hayvan meta verisini
  sağlıyorsa (veya Temel model olarak vanilla bir `a_c_...` pedi seçildiyse) ragdoll
  replace modellerdeki gibi normal çalışır.
- **Doğrulama sıkılaştırıldı.** Şablonla üretilen hayvan meta verisinde ragdoll için
  zorunlu alanlardan biri boşsa paket oyuna ulaşmadan açık hatayla durur.
- **Eski hazır paketler yeniden üretilir.** Hazır DLC biçim sürümü yükseltildi; önceki
  sürümden kalan hazır `dlc.rpf` yeniden kullanılmaz.
- **Ağ protokolü değişmedi.** 2.6.0 istemciler `VERSION_0_9_4` kullanmaya devam eder;
  2.5.9, 2.5.8, 2.5.7, 2.5.6, 2.5.5, 2.5.4, 2.5.3, 2.5.2, 2.5.1 ve 2.5.0 istemcilerle
  oynayabilir.

### 2.5.9 — Addon ped meta verisi ve paket doğrulaması sağlamlaştırıldı

Bu sürüm, addon hayvan çökmesinin arkasındaki **ped meta verisi hattını** sağlamlaştırır:
bir paket `a_c_*` hayvanını insan meta verisiyle tanımlayabiliyor, oyun da hayvan iskeleti
üzerine insan pedi kuruyordu.

- **Gerçek meta veri varsa kullanılır.** Sync sunucusu ped meta verisini artık paketin
  kendisinden okur (gevşek `peds.meta`, `pedmeta.xml`, `ap_m.xml` ve benzeri dosyalar,
  paketin RPF arşivlerine gömülü meta ve `Belgeler\Rockstar Games\GTA V\UserMods\ap_m.xml`);
  her zaman sabit şablondan yeniden oluşturmaz.
- **Meta veri yalnızca tür uyuşuyorsa korunur.** Kaynak Item yalnızca `Pedtype` değeri pedin
  türüyle uyuşuyorsa yeniden kullanılır. İnsan meta verili `a_c_*` modeli artık insan kapsül
  ve komponent alanlarını devralmaz; hayvan şablonuna düşer. İnsan ve kadın pedler
  `CIVMALE`/`CIVFEMALE` olarak kalır.
- **Ped başına temel model.** Ped düzenleyicisine eklenen **Temel model** alanı, yeniden
  dokulanmış bir pedin meta verisini türetildiği modelden almasını sağlar.
- **Ragdoll artık ped başına ayar.** `auto` (varsayılan) hayvanlarda ragdoll'u açık tutar,
  insanlarda oyunun varsayılanını bırakır; yalnızca açıkça `off` seçilirse kapatılır.
- **Paketler oyuna gitmeden doğrulanır.** Eksik dosya, RSC7 olmayan dosya, eksik model,
  yinelenen model ve `Pedtype` veya `IsStreamedGfx` değeri pedle uyuşmayan üretilmiş meta
  verisi, oyuna ulaşmak yerine açık bir hatayla paket oluşturmayı durdurur.
- **Eski hazır paketler yeniden oluşturulur.** Hazır DLC biçim sürümü yükseltildi; önceki
  sürümden kalan önbellek yeniden kullanılmak yerine yeni meta veriyle oluşturulur.
- **Daha fazla tanılama.** `error.log` yerel addon pedler, hayvan denetleyicisi ve uzak hayvan
  kopyaları için `isPlayerPed`, `pedType`, `isHuman`, `ragdoll` ve `baseModel` kaydeder.
  Sync aracına `peds-prepare`, `peds-meta` ve `rsc-dump` komutları eklendi.
- **Ağ protokolü değişmedi.** 2.5.9 istemciler `VERSION_0_9_4` kullanmaya devam eder;
  2.5.8, 2.5.7, 2.5.6, 2.5.5, 2.5.4, 2.5.3, 2.5.2, 2.5.1 ve 2.5.0 istemcilerle oynayabilir.

### 2.5.8 — Addon hayvan görünmezliği ve ragdoll çökmesi düzeltildi

Bu sürüm, modeli Addon Pedler menüsü yerine Menyoo, Ped Selector veya başka bir
trainer ile uygulanan **görünmez ve ragdoll sırasında çöken addon hayvanları** düzeltir.

- **Neden oldu?** 2.5.7 varyasyon düzeltmesini yalnızca ped bu launcher'ın Addon Pedler
  menüsünden gelince çalıştırıyordu. Menyoo veya Ped Selector ile uygulanan pedde
  drawable varyasyonu ayarlanmıyordu: ped görünmüyor ve ragdoll verisi bozuk kalıyordu.
- **Dış model değişiklikleri de yakalanır.** Yerel oyuncu modelini izleyen bir denetleyici,
  hayvan Menyoo, Ped Selector veya Rampage ile gelmiş olsa da model değişimi sonrası
  aynı varyasyon düzeltmesini uygular.
- **Meta veri güvencesi.** Sunucu paketi `a_c_*` hayvanını insan (`CIVMALE`) olarak
  tanımlayabiliyordu. Oyun pedi insan sayıp çarpışmada çöken insan iskeleti kuruyordu.
  Sync sunucusu artık tüm `a_c_*` modelleri için `ANIMAL` meta verisini zorunlu kılar
  ve yanlış meta verili paketleri yeniden oluşturur.
- **Ragdoll açık kalır.** Hayvanlarda ragdoll bayrağı veya çarpışmada ragdoll kapatılmaz.
- **Tanılama.** `error.log` artık `Local animal model detected`, `Local addon ped ready`
  ve `Addon variation stage 0/1` satırlarını ped türü ve drawable sayısıyla kaydeder.
- **Ağ protokolü değişmedi.** 2.5.8 istemciler `VERSION_0_9_4` kullanmaya devam eder;
  2.5.7, 2.5.6, 2.5.5, 2.5.4, 2.5.3, 2.5.2, 2.5.1 ve 2.5.0 istemcilerle oynayabilir.

### 2.5.7 — Addon hayvan görünürlüğü geri geldi

Bu sürüm, 2.5.6'daki çarpışma düzeltmesini koruyarak **yerel oyuncuda addon hayvanın görünmesini** geri getirir.

- **Hayvan yeniden görünür.** 2.5.6 yerel oyuncuya akışlı komponent varyasyonunu uygulamayı
  bıraktığı için model görünmüyordu. Varsayılan varyasyon artık yeni ped onaylandıktan sonra
  uygulanıyor; eski pede değil.
- **Çarpışma düzeltmesi korunur.** Varyasyon ve ped ayarları hâlâ yalnızca model değişimi
  tamamlandıktan sonra çalışır; 2.5.6'daki çökme yolu değişmedi.
- **Ragdoll açık kalır.** Hayvanlarda ragdoll bayrağı veya çarpışmada ragdoll kapatılmaz.
- **Ağ protokolü değişmedi.** 2.5.7 istemciler `VERSION_0_9_4` kullanmaya devam eder;
  2.5.6, 2.5.5, 2.5.4, 2.5.3, 2.5.2, 2.5.1 ve 2.5.0 istemcilerle oynayabilir.

Bu istemci tarafında bir düzeltmedir; launcher sürümü yalnızca güncellemenin dağıtılması
için yükseltildi.

### 2.5.6 — Hayvan pedi çarpışma çökmesi ragdoll kapatılmadan düzeltildi

Bu sürüm, **addon hayvan pedine araç çarptığında oluşan çökmeyi** ragdoll'u kapatmadan düzeltir.

- **Ragdoll açık kalır.** Hayvanlar diğer pedler gibi düşer. İstemci artık ragdoll bayrağını
  veya çarpışmada ragdoll davranışını kapatmaz.
- **Ped ayarları model değişimini bekler.** `ChangeModel` oyun pedi değiştirmeden döner.
  İstemci artık yeni model etkin olana kadar bekler ve sağlık/silah temizliğini eski ped yerine
  yeni pede uygular.
- **Yerel oyuncuda zorunlu komponent varyasyonu yok.** Hayvan iskeletinde insan komponent
  yuvalarını sıfırlamak pedin drawable durumunu bozuyor ve çarpmada çöküyordu. Yerel oyuncu
  artık oyunun kendi varsayılan varyasyonunu kullanır; varyasyon düzeltmesi yalnızca uzak
  kopyalarda kalır.
- **Uzak hayvan kopyaları hayvan ped türüyle oluşturulur.** Kopyalar `CIVMALE` yerine ped
  türü 28 ile oluşturulur; böylece oyun hayvan iskeletinde hayvan kodunu çalıştırır.
- **Ağ protokolü değişmedi.** 2.5.6 istemciler `VERSION_0_9_4` kullanmaya devam eder;
  2.5.5, 2.5.4, 2.5.3, 2.5.2, 2.5.1 ve 2.5.0 istemcilerle oynayabilir.

Bu istemci tarafında bir düzeltmedir; launcher sürümü yalnızca güncellemenin dağıtılması
için yükseltildi.

### 2.5.5 — Hayvan pedi araç çarpışması çökmesi düzeltildi

Bu sürüm, **hayvan pedi bir araca çarptığında oyunun çökmesini** düzeltir.

- **Çarpışma çökmesi bitti.** Addon hayvan pedlerinde insan ragdoll verisi yoktur.
  Araç çarpması oyunun insan ragdoll başlatmasına ve çökmesine yol açıyordu. İstemci artık
  yalnızca hayvanlarda çarpışmada ragdoll'u ve ped ragdoll bayrağını kapatır.
- **Hayvan hareketleri aynı kalır.** Yürüme, koşma ve addon ped menüsü eskisi gibi çalışır.
- **Diğer oyuncuların hayvanları da korunur.** Uzak hayvan kopyalarına da aynı koruma uygulanır;
  yerel bir araba onlara çarptığında oyun çökmez.
- **İnsan pedleri etkilenmez.** Koruma gerçek modele bakar; insan ve kadın addon pedleri
  normal ragdoll davranışını korur.
- **Ağ protokolü değişmedi.** 2.5.5 istemciler `VERSION_0_9_4` kullanmaya devam eder;
  2.5.4, 2.5.3, 2.5.2, 2.5.1 ve 2.5.0 istemcilerle oynayabilir.

Bu istemci tarafında bir güvenlik düzeltmesidir; launcher sürümü yalnızca güncellemenin
dağıtılması için yükseltildi.

### 2.5.4 — Görünmeyen hayvan pedleri düzeltildi

Bu sürüm **görünmeyen addon hayvan pedlerini** düzeltir.

- **Hayvanlar artık görünür.** Model değişiminden sonra oyunun varsayılan komponent
  varyasyonu uygulanır ve doku yenilemesi zorlanır. Aynı düzeltme diğer oyuncuların
  istemcilerinde oluşturulan kopyalara da uygulanır.
- **İnsan pedleri etkilenmez.** Hayvana özel varyasyon sıfırlaması insan kıyafetlerine,
  aksesuarlarına veya prop'larına dokunmaz.
- **Ağ protokolü değişmedi.** 2.5.4 istemciler `VERSION_0_9_4` kullanmaya devam eder;
  2.5.3, 2.5.2, 2.5.1 ve 2.5.0 istemcilerle oynayabilir.

### 2.5.3 — Gevşek ped dosyalarından peds.rpf

Bu sürüm addon pedleri **gevşek ped dosyaları** üzerinden yeniden düzenler.

- **Dosyaları bırakın, tek peds.rpf oluşsun.** `.ydd/.ytd/.ymt/.yft` dosyalarını
  `Addons\peds\<paket>` klasörüne koyun (düz dosyalar veya streamed ped başına bir klasör).
  Sunucu dosyaları modele göre gruplar, AddonPeds biçimindeki tek bir `peds.rpf` içinde
  paketler ve manifest hazırlanırken benzersiz bir `dlc.rpf` içine yerleştirir.
- **Ped bazında yönetim.** **Addon Peds** sekmesi artık PedSelector gibi çalışır: gevşek
  dosya ekleme, streamed bileşen klasörü ekleme, klasör veya ZIP/RAR/7Z arşivi içe aktarma,
  görünen adı/türü/sağlığı/meta veriyi düzenleme, pedin dosyalarını görüntüleme ve pedi
  kaldırma.
- **Güvenli kaldırma.** Kaldırılan ped dosyaları silinmez;
  `Addons-Removed\<zaman>\peds\...` klasörüne taşınır, böylece yanlış bir işlem geri alınabilir.
- **Daha fazla ped türü.** İnsan, kadın ve hayvan pedleri desteklenir. Hayvanlarda insana
  özel kıyafet, silah, nişan alma, ateş etme, zıplama ve paraşüt mantığı atlanır; otomatik
  algılama yanlışsa tür ve meta veri Düzenle ile düzeltilebilir.
- **Eski paketler çalışmaya devam eder.** Mevcut `dlc.rpf`/`peds.rpf` paketleri bir kez
  gevşek dosyalara açılır ve sonra bu dosyalardan yeniden üretilir.
- **Kendi AddonPeds kurulumunuza yine dokunulmaz.** `ap_m.xml`, `UserMods` ve mevcut
  `dlcpacks` yalnızca okunur. Sunucu paketleri benzersiz bağlantı/cihaz/değişiklik kümesi
  adları kullandığı için kendi paketlerinizle çakışmaz.

Ağ protokolü değişmedi (`VERSION_0_9_4`); 2.5.3 istemciler 2.5.2, 2.5.1 ve 2.5.0
istemcilerle aynı sunucuda oynayabilir. Eski istemciler ped listesini yok sayar.

### 2.5.2 — Sunucudan addon pedler

- **Launcher'da Addon Peds sekmesi.** Sunucum bölümündeki **Addon Peds** düğmesi ayrı bir
  sekme açar; `Addons\peds` içindeki her ped paketi, insan/hayvan türü, model adı ve ped
  başına etkinleştirme kutusuyla listelenir. Oyuncular yalnızca açık bıraktığınız pedleri görür.
- **İnsan ve hayvan pedleri.** AddonPeds paketleri ve normal ped DLC paketleri desteklenir.
  Hayvan modelleri (`a_c_...`) otomatik algılanır; hayvan pedlerinde insana özel kıyafet,
  silah, nişan alma, ateş etme, zıplama ve paraşüt mantığı atlanır. Böylece senkron ve
  çarpışma bozulmaz.
- **Kendi AddonPeds kurulumunuza dokunulmaz.** Sunucu paketleri benzersiz bağlantı adı ve
  gömülü meta veriyle kendi kendine yeterli DLC'lere dönüştürülür. `ap_m.xml`, `UserMods`
  klasörü ve mevcut `dlcpacks` yalnızca okunur; değiştirilmez, üzerine yazılmaz.
- **Diğer paketlerle çakışmaz.** Her sunucu paketi `gcs_<paket>` olarak, kendi cihaz adı,
  `nameHash` ve değişiklik kümesi adıyla bağlanır. Aynı adlı paket bilgisayarınızda zaten
  varsa algılanır ve atlanır.
- **Oyun içi menü.** F9 > **Addon Peds** ile sunucunun insan ve hayvan pedleri arasında
  geçiş yapın veya normal karakterinize dönün. Menü her açılışta kurulu pedlerden yenilenir.

Ağ protokolü değişmedi (`VERSION_0_9_4`); 2.5.2 istemciler 2.5.1 ve 2.5.0 istemcilerle aynı
sunucuda oynayabilir. Eski istemciler yeni ped listesini yok sayar.

### 2.5.1 — Gecikme ve takılma düzeltmeleri

- **Önce doğrudan oyun bağlantısı.** Sunucuya doğrudan erişilebiliyorsa (aynı ağ,
  Radmin/Tailscale, port yönlendirme, UPnP) oyun artık relay yerine doğrudan sunucuya
  bağlanır. Launcher relay'i arka planda yine açar; doğrudan yol kurulamazsa istemci
  6 saniye içinde otomatik olarak relay'e döner. Bağlantı güvenilirliği aynı kalır.
- **NPC senkronu NPC başına saniyede 10 kez** gönderilir ve saniyede 4 kez taranır
  (önce her karede). Sunucuda yalnızken hiç taranmaz. Yoğun trafik artık bağlantıyı
  ve kare döngüsünü boğmaz.
- **Ses yalnızca gerçekten konuşurken gönderilir.** Bas-konuş basılı ama sessizken boş
  ses akmaz; oynatma tamponu 500 ms ile sınırlandığı için konuşma gecikmesi zamanla
  birikmez.
- **Kare başına daha az iş:** araç radyosu ve mod listesi her karede değil saniyede bir
  uygulanır, radyo istasyonu listesi önbelleğe alınır, uzak pedler tek bir ilişki
  grubunu paylaşır, eski oyuncu/NPC temizliği saniyede bir çalışır ve Lidgren mesaj
  geri dönüşümü açıktır.

Ağ protokolü değişmedi (`VERSION_0_9_4`); 2.5.1 istemciler 2.5.0 istemcilerle aynı
sunucuda oynayabilir.

### 2.5.0 — F9 oyuncuya ışınlanma

- **F9 > Teleport to Player** menüsü sunucudaki tüm oyuncuları listeler.
- Yürüyen bir oyuncuya tıklayınca yanına ışınlanırsınız.
- Araç süren bir oyuncuya tıklayınca **aracın ilk boş yan koltuğuna** geçersiniz
  (MTA'daki gibi). Sürücü koltuğu asla kullanılmaz.
- Araç doluysa veya yan koltuk yoksa aracın yanına ışınlanırsınız.
- Araç henüz görüş mesafesinde değilse istemci oyuncunun son konumunu takip eder
  ve araç yüklenir yüklenmez yan koltuğa geçer.
- Menü her açılışta yenilenir; yeni katılan veya ayrılan oyuncular anında yansır.

### 2.4.1 — F9 sunucu tarayıcısı düzeltmesi

- Oyun **launcher olmadan normal başlatıldığında** da F9 sunucu tarayıcısı çalışır.
  Varsayılan master (`master.gtacoop.com`) HTTP 526 döndürdüğü için istemci artık önce
  çalışan yedek listeyi dener; ölü sunucuda takılıp kalmaz.
- Master listesi arka planda, 5 saniye zaman aşımıyla çekilir; F9 açılırken oyun donmaz.
  "Searching..." satırı tarama durumunu gösterir, bulunan sunucular geldikçe listeye eklenir.
- Bulunan sunucular `%LOCALAPPDATA%\cgmp\gtacoop\servers.json` dosyasına yazılır ve
  sonraki F9 açılışında anında gösterilir. Launcher da başarılı taramadan sonra aynı
  önbelleği yazar; böylece oyun içi UDP keşfi kısıtlı olsa bile liste dolu gelir.
- Yerel keşif 4499, 4498 ve 44499 portlarını dener; her master uç noktası ayrı hata
  yönetimiyle sorgulanır.
- Hâlâ ölü `https://master.gtacoop.com` adresini kullanan mevcut `ClientSettings.xml`
  dosyaları yedek listeye taşınır. Kullanıcının özel master adresine dokunulmaz.

### 2.4.0 — mods klasöründeki RPF arşivleri

- OIV `<archive>` `add` ve `replace` işlemleri artık `mods\` altındaki **tüm mevcut
  arşivlerde** çalışır: `x64a.rpf`, `common.rpf`, `update\update.rpf`,
  `update\update2.rpf`, `update\x64\dlcpacks\...\dlc.rpf` ve
  `x64\audio\sfx\...rpf` gibi iç içe yollar. Kök oyun arşivlerine asla yazılmaz.
- Arşivin tamamı kopyalanmaz; yalnızca değişen girdinin verisi ve küçük TOC/isim
  başlığı yazılır.
- Yeni girdi başlığı büyütünce yalnızca başlıkla çakışan ilk küçük veri bloğu
  arşivin sonuna taşınır; ham (şifreli/sıkıştırılmış) baytlar aynen korunur ve
  TOC offseti güncellenir. Arşivin geri kalanı yeniden yazılmaz.
- Geri alma orijinal TOC ve offsetleri geri getirir. Kullanıcının kendi
  değiştirdiği dosya SHA-256 ile algılanır ve korunur.
- Şifreli binary girdiler ve RSC7 kaynakları (`ytd`, `yft`, `ymap`, ...) eklenebilir
  veya değiştirilebilir. Eski şifreli blok geri alma için yerinde bırakılır.

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
- **GTA V Enhanced (Gen9) ve addon DLC paketleri**: Addon araç/ped paketleri `update.rpf`
  içindeki `dlclist.xml` girdisine ihtiyaç duyar. GTA V Enhanced arşivleri **NG şifreli**dir ve
  yazılamaz; bu yüzden launcher sizin `mods/update/update.rpf` dosyanızı **kopyalamaz, değiştirmez
  ve silmez**. Böyle bir kurulumda script/ASI modları kurulur, addon DLC paketleri atlanır ve
  günlüğe nedeni yazılır (`Oyun: GTA V Enhanced; mods/update/update.rpf: NG (Gen9)`).
  FiveM aynı paketleri sorunsuz çalıştırır çünkü FiveM istemcisi `stream/` dosyalarını
  **oyun çalışırken belleğe kaydeder**; Hikaye Modu'nda böyle bir kayıt mekanizması yoktur ve
  `dlclist.xml` yalnızca açılışta, `update.rpf` içinden okunur. Enhanced'da addon DLC kullanmak
  için `mods/update/update.rpf` dosyasının **şifresiz (OPEN)** bir kopyası ve RageOpenV/OpenRPF
  gerekir; bu kopya GB boyutundadır ve launcher bunu sizin yerinize oluşturmaz. Legacy (klasik)
  GTA V kurulumunda aynı paketler normal çalışır.
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
- Uygulama simgesi ve sürüm bilgisi (2.5.3) exe dosyasına işlenmiştir.

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

## AddonPed+ (bağımsız ped modu)

- `src/GTACoop.PedSelector.App` projesi **AddonPed+** aracını üretir: launcher'dan ve sunucudan tamamen
  bağımsız bir addon ped paketi ve oyun içi ped seçici.
- Kendi DLC klasörünü (`gtacoop_peds`), kendi script dosyalarını ve dlclist içinde tek bir girdiyi ekler;
  kaldırınca bunları geri alır. Launcher'ın Addon Peds sekmesi ve sunucu ped dağıtımı ayrı çalışmaya devam eder.
- Kurulumun iki yolu var: araçtaki **Oyuna Kur...** ya da OpenIV için üretilen `AddonPedPlus.oiv` paketi.
- Masaüstü arayüzü **varsayılan olarak İngilizcedir** ve düğme çubuğundaki dil seçicisiyle **Türkçeye**
  çevrilebilir; ped ekleme, düzenleme, kaydetme ve OIV üretimi vardır.
- Oyun içi menü İngilizcedir ve varsayılan **F10** ile açılır (tuş `scripts\AddonPedPlus.ini` içindeki
  `MenuKey` ile değiştirilir). Listenin ilk satırı **Default (normal character)**, son satırı **Back**'tir;
  `Esc` yalnızca menüyü kapatır, pedi değiştirmez.
- Ayrıntılı kurulum/kaldırma: `ADDONPEDPLUS-OKUBENI.txt` (İngilizce) ve `PED-SELECTOR-MOD-TR.md` (Türkçe).

## Notlar

- Oyunu **Steam açıkken** başlatın; senkronizasyon ve kurulum işlemleri için oyunun kapalı
  olması gerekir (launcher kontrol eder).
- Kurulum gereksinimleri: **.NET 8** (launcher/sunucu) ve **.NET 10** (oyun sunucusu çekirdeği).
- Günlük dosyası: `%LOCALAPPDATA%\GTACoopRemake\logs\` (oturum izleyici günlüğü:
  `session-watch.log`).
- Sorun olursa **Günlük / Araçlar** sekmesine bakın; kalıntı temizliği çoğu sorunu çözer.
