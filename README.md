# GTACoop Remake — Türkçe Mod + Sunucu Yöneticisi

2.1 sürümü — kendi sunucu yönetimi, otomatik mod indirme ve **oturum modu**:
sunucu modları yalnızca oyun açıkken kurulu kalır, oyundan çıkınca otomatik kaldırılır.

---

## Ne işe yarar?

- **Kendi modlarınız karışmaz**: PC'nize kurduğunuz modlara hiç dokunulmaz; kendi modlarınızı
  yönetmenize gerek yoktur — zaten kurulu hâlleriyle çalışır.
- **Kendi sunucunuz olsun**: kendi GTACoOp sunucunuzu bu launcher'dan başlatın; addon araç/ped
  paketi, script, script klasörü, ASI eklentisi ekleyin.
- **Sunucuya girince modlar inisin**: sunucunun tüm modları otomatik indirilir ve yalnızca siz
  oynarken kurulu kalır. Oyunu kapatınca **otomatik kaldırılır**; tek oyunculu (offline) modda
  sunucu modları çalışmaz.
- **Tekrar indirme yok**: indirilen dosyalar bilgisayarınızda saklanır (önbellek); ikinci
  girişte yeniden indirilmez. DLC paketleri oyun klasörüne kopyalanmaz, bağlantı (junction)
  ile eklenir — GB'larca veri çoğaltılmaz.
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
- **Sunucular**: sunucu ekleme, **Bağlan ve Oyna** (modlar otomatik iner-kurulur), oturum modu
  ayarı, modları önceden indirme, oturumu bitirme.
- **Sunucum**: kendi sunucunuzu başlatma/durdurma, ayarlar, mod ekleme.
- **Günlük / Araçlar**: günlük, yedekten geri yükleme, kalıntı temizleme.

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
- Uygulama simgesi ve sürüm bilgisi (2.1.0) exe dosyasına işlenmiştir.

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
3. Kapılar: oyun portu **4499 UDP**, senkronizasyon portu **4599 TCP**. Arkadaşlarınız için
   yönlendirici (router) ayarında bu portları açın; Windows Güvenlik Duvarı ilk başlatmada izin
   ister.

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
daha indirilmez.

**Dosyalar nerede durur?** indirilen mod dosyaları `%LOCALAPPDATA%\GTACoopRemake\cache\`
altında saklanır; oyun klasörüne yalnızca oynarken eklenir. Oyun kapanınca oyun klasörü
temizlenir, önbellek kalır (tekrar indirme olmaz).

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

---

## Yedekleme ve güvenlik

- dlclist (update.rpf) her düzenlemeden önce `mods/GTACoop/backups/` altına yedeklenir
  (günde bir kez, son 3 yedek saklanır).
- **Orijinal** `update/update.rpf` dosyasına asla dokunulmaz; yalnızca `mods/update/update.rpf`
  düzenlenir.
- Silme işlemleri yalnızca launcher'ın kurduğu dosyaları kapsar; kendi dosyalarınıza ve
  oyunun dosyalarına dokunulmaz.
- **Günlük / Araçlar** sekmesinden: yedekleri listeleyip geri yükleyebilir, sunuculardan
  kurulmuş tüm eklentileri (script, ASI, dlc paketi) tek tuşla temizleyebilirsiniz.

---

## Arkadaşlarla oynama (katılım kodu veya IP)

**En kolay yol (katılım kodu):**

1. Siz **Sunucum → Katılım Kodu Oluştur...** ile adresi seçip kodu kopyalayın,
   arkadaşınıza gönderin (WhatsApp/Discord).
2. Arkadaşınız **Sunucular → Kod ile Katıl...** deyip kodu yapıştırsın ve
   **Ekle ve Bağlan**'a bassın. Kod bağlantı bilgilerini kendi taşır; hesap/internet
   sunucusu gerekmez (şifre/token içerebilir, yalnızca güvendiğiniz kişiye verin).
3. Siz **Sunucuyu Başlat** deyin; arkadaşınız bağlanınca modlar otomatik iner.

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

  → `dist\GTACoop-Remake-Paylas.zip` (uygulama + arkadaş okubeni + lisans) oluşur.
  Pakette Türkçe (`OKUBENI-ARKADASLAR.txt`, `KODLA-KATIL.txt`) ve İngilizce
  (`READ-ME-EN.txt`, `JOIN-WITH-CODE-EN.txt`) rehberler birlikte yer alır.
- **Pakete asla eklemeyin:** GTA V oyun dosyaları; ScriptHook V / ScriptHookVDotNet dosyaları
  (lisansları yeniden dağıtıma izin vermez). Arkadaşınız bunları kendisi kurar — gerekli
  liste `OKUBENI-ARKADASLAR.txt` içinde yazılıdır.
- Arkadaşınızda olması gerekenler: orijinal GTA V + ScriptHook V + ScriptHookVDotNet v3 +
  **.NET 8 Desktop Runtime** (launcher için). Kendi sunucusunu kurarsa ayrıca **.NET 10**.
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
