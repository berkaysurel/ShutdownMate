# Sürüm geçmişi

Biçim: [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/). Sürümleme: anlamsal sürümleme (MAJOR.MINOR.PATCH).

## [2.6.0] - 2026-11-05

### Değişti
- **Yeni ad: ShutdownMate.** Program `ShutdownMate.exe` olarak çıkar; pencere başlıkları, Hakkında ekranı ve yayın paketi yeni adı taşır. Arayüz metinleri ve işlevler değişmedi.
- Ayar klasörü `%APPDATA%\ShutdownMate` oldu.
- Beta etiketi kaldırıldı.
- Hakkında ekranındaki proje bağlantıları ShutdownMate deposuna gider; "depo özeldir" notu kaldırıldı.

### Eklendi
- **Dil desteği (Türkçe ve İngilizce):** arayüzün tamamı iki dilde; Ayarlar > Kişiselleştirme > Dil'den seçilir ("Sistem" Windows diline uyar), değişiklik uygulama yeniden açılınca geçerli olur. Geçmiş satırları yazıldıkları dilde saklanır.
- **README:** İngilizce tanıtım sayfası, tanıtım görseli ve gezinti GIF'leri.
- **Microsoft Store (MSIX) hazırlığı:** program Store paketi olarak çalıştığını algılar; bu durumda ayar ve kayıt klasörleri Gezgin ve görüntüleyiciye gerçek yolla açılır, GitHub güncelleme düğmesi gizlenir (güncellemeyi Store verir). Paket şablonu, simgeler ve `tools\package-store.ps1` eklendi.

### Dahili
- Kaynak klasörleri, proje dosyaları ve ad alanı `ShutdownMate` oldu (`src\ShutdownMate`, `src\ShutdownMate.Checks`).

## [2.5.5] - 2026-11-03

### Eklendi
- **Gözetim kaydı klasörü:** Ayarlar > Geçmiş'ten kayıtların tutulacağı klasör seçilebilir (seçilen klasörün altında `ShutdownMate\Gecmis`), kayıt adına isteğe bağlı sıra numarası eklenir (ör. `0007_160657_02_11_2026`) ve ekran görüntülerinin sağ altına tarih, saat, mod, program ve not damgası konabilir (hepsi açılıp kapanır).
- **Kişiselleştirme** (Ayarlar'da eski adı Görünüm): ana ekrandaki eylemleri, zaman kiplerini, işler bitince koşullarını ve diğer öğeleri (başlık, son çalıştırma yazısı, hızlı süre, kilit düğmeleri) gizleyip gösterebilirsiniz. Her grupta en az bir öğe kalır.
- **Animasyonlar:** ekran ve panel geçişleri, açılır paneller ve düğme basışı yumuşak animasyonla çalışır. Kişiselleştirme'deki "Animasyonlar" anahtarıyla ya da Windows animasyonları kapatılarak kapanır.

### Düzeltildi
- Bozuk ya da okunamayan ayar ve geçmiş dosyası artık sessizce ezilmez: içerik bozuksa dosya yanına `.bozuk` adıyla alınır, geçici okuma hatasında (başka program dosyayı tutuyorsa) dosyaya dokunulmaz. Ayar ve geçmiş kaydedilemezse hata `error.log`'a yazılır.
- USB güvenle kaldırma: aynı aygıtın birden çok sürücüsünden biri kullanımdaysa hiçbiri ayrılmadan çıkarma durdurulur; zaman aşımı mesajı çıkarmanın arka planda sürmüş olabileceğini söyler.

### Değişti
- Ana pencerenin kod arkası bakım için parçalara ayrıldı; davranış değişmedi.
- Ana ekran kartlarındaki simgeler artık her zaman görünür (`Kart ikonları` seçeneği kaldırıldı).

## [2.5.0] - 2026-11-02

### Eklendi
- **Gözetim kaydı** (varsayılan kapalı): zamanlayıcı çalışırken izlenen programın kapanması, yanıt vermemesi ve boşta kalması, internet bağlantısının kopması, Windows'un hata kayıtları, dakikalık işlemci / ekran kartı / aktarım değerleri ve ekran görüntüleri kaydedilir. Bilgisayar kapandıktan sonra Geçmiş'ten görülebilir. Kayıtlar yalnızca bu bilgisayarda, `%APPDATA%\ShutdownMate\Gecmis` içinde tutulur.
- Gözetim kaydı açılırken neyin kaydedileceğini anlatan onay penceresi; onay verilmeden anahtar açılmaz. Kapatınca mevcut kayıtların silinmesi sorulur. Kayıt sürerken sayaç ekranında "Gözetim kaydı alınıyor" yazar; yanındaki göz simgesi vurgu rengiyle yumuşakça yanıp söner.
- Gözetim kaydı ayarları: ekran görüntüsü alma, görüntü alanı (tüm ekran ya da yalnızca izlenen programın penceresi), ara görüntü sıklığı, saklama (son 10 / 30 / 50 kayıt ve disk sınırı), Windows hata kayıtlarını ekleme.
- Geçmiş satırları açılabilir: olay zaman çizelgesi (sorunlar vurgulu), ekran görüntüsü küçük resimleri, süre ve dakikalık işlemci / ekran kartı grafiği.
- **Hakkında** sekmesi: sürüm ve derleme tarihi, "Güncellemeleri denetle" ve proje sayfası bağlantıları, gizlilik özeti, klavye kısayolları, lisanslar.

- **Dosya aktarımı** koşulu (İşler bitince): yalnızca disklerin okuma/yazma yoğunluğuna bakar; ağ yükü sayılmaz. Otomatik sürücü seçimi diski kilitler, 20 sn'lik ortanca ile ani dalgalanmalar yumuşatılır. "Tüm diskler" seçiliyse sürücülerin en yükseği ölçülür.
- **USB güvenle kaldırma:** Kapat ve Yeniden başlat öncesinde takılı USB sürücüler güvenle çıkarılır; çıkarma sonucu doğru sayılır. Kapanış uyarısında İptal ya da Ertele denirse güç komutu verilmez.

### Değişti
- **Gösterge stilleri yenilendi:** Modern halka inceldi, Klasik yelkovan sadeleşti, çubukların uçları yuvarlak; halka, yelkovan ve çubuklar saniyede bir sıçramak yerine yumuşakça kayar. Dijital stil koyu panelde amber yedi segmentli LED rakamlar gösterir (son uyarıda kırmızı).
- Ayarlar > Görünüm'de stil kartları gerçek göstergenin küçük hâlini gösterir; altında "Tam ekranda önizle" ve "Kilit ekranında önizle" düğmeleri var.
- Ayarlardaki "Geçmiş ve hakkında" sekmesi "Geçmiş" ve "Hakkında" olarak ikiye ayrıldı; Ayar klasörünü aç ve Varsayılanlara dön Hakkında'ya taşındı.
- "Geçmişi temizle" gözetim kayıtlarını ve ekran görüntülerini de siler. Yalnızca gözetim kayıtlarını silmek için Gözetim kaydı bölümünde onaylı "Kayıtları sil" düğmesi var; geçmiş listesi kalır.
- Gözetim kaydı klasör ve görüntü adları saat_gün_ay_yıl biçiminde (ör. `160657_02_11_2026`).
- "Yalnızca program" görüntü alanında program küçültülmüşse boş görüntü alınmaz. İsteğe bağlı **deneysel** "Küçültülmüş programın görüntüsünü al" anahtarı (varsayılan kapalı) programı ekran dışında, odak almadan bir anlığına açıp görüntüsünü alır; ekranı ekran kartıyla çizen programlarda görüntü siyah çıkabilir.

- İşler bitince ortak ayarları (bekleme, üst süre sınırı vb.) tek bir "Ne zaman bitmiş sayılsın?" bölümünde toplandı.

### Güvenlik
- Kayıt silme yalnızca kayıt klasörünün altındaki tarih adlı klasörlerde çalışır; onay verilmeden açık gelen ayar kapatılır; Windows hata metinleri kısaltılarak saklanır.

## [2.4.0] - 2026-11-02

### Eklendi
- **Zamanlayıcı kurmadan kilit**: kurulum ekranındaki "Ekranı kilitle" ve "Tam ekran kilidi" düğmeleri, bilgisayarı kapatmadan yalnızca ekranı kilitler. Geri sayım yerine "kilitli" başlığı ve geçen süre görünür.
- **Kilit mesajı**: ekran kilidinde ve tam ekranda görünen kısa mesaj (en fazla 120 karakter). Mesaj ve anahtarı ana ekranda, hazır mesajların düzenlenmesi Ayarlar > Ekran kilidi'ndedir.
- **Kapanış uyarısında "bilgisayar kullanılıyor" bilgisi**: "Bilgisayar kullanılıyorsa işlem yapma" açıkken uyarıda işlemin neden ertelenebileceği canlı yazar. "Şimdi kapat" (ya da uyut, yeniden başlat) düğmesi ikinci bir onayla işlemi hemen yapar; onay ekranında Enter çalışmaz, Esc geri döner. Sessiz ertelemede sayaç ekranı saati ve nedeni gösterir.
- **Ekran kilidi ayarları**: kilitte nelerin görüneceği seçilir, ana pencere gizlenir, çıkış düğmesi üstte ya da altta olur, basılı tutma süresi ayarlanır ya da kapatılır.

### Değişti
- İşler bitince panelinde sayı kutularının yanındaki metinler alt satıra düşmez.
- Kapanış uyarısında erteleme kısayolu S yerine **E** (Ertele) oldu; klavye kısayolları düğmelerin altında yazar.
- Açıklaması olmayan düğme ve seçeneklere üzerinde bekleyince görünen açıklamalar eklendi: ayar sekmeleri, gösterge stilleri, İşler bitince değerleri, saat seçiciler, geçmiş ve ayar düğmeleri.
- "Geçmişi temizle" artık silmeden önce onay ister.

### Düzeltildi
- Ekran kilidi ve tam ekran kilidindeyken Windows tuşu Başlat menüsünü açıyor, Alt+Tab başka pencereye geçiyordu. Kilit açıkken Windows tuşu, Alt+Tab, Alt+Esc ve Ctrl+Esc yok sayılır (Ctrl+Alt+Del ve Win+L Windows'a aittir, engellenemez).
- Pencere kapandıktan sonra süreç takılı kalırsa 5 saniye içinde hata kaydına yazıp kendini sonlandırır; takılı süreç programın yeniden açılmasını engelliyordu. Takılmanın kök nedeni henüz bulunamadı.

## [2.3.0] - 2026-10-31

### Eklendi
- **Kapanış uyarısı**: işlemden ayarlanan süre önce (15 sn – 2 dk, varsayılan 30 sn) ekran kendiliğinden tam ekrana geçer; büyük "İptal et" ve "Ertele" düğmeleriyle masadaki kullanıcı işlemi durdurabilir. Enter/Esc iptal, S erteler.
- **Diğer uygulamaların sesini kıs**: zamanlayıcı çalışırken diğer programların sesi kapanır, durunca geri gelir; yalnızca bu uygulamanın uyarı sesleri duyulur.
- **Program bazlı ölçüm**: indirme/yükleme, işlemci ve ekran kartı koşulları isteğe bağlı olarak tek bir programa (ör. Chrome, DaVinci Resolve) göre ölçülür. Aynı adlı tüm süreçler toplanır.
- Uygulamaya özel, sentezlenmiş uyarı sesleri (sistem seslerinden bağımsız).
- Hakkında bölümünde geliştirici ve beta bilgisi; pencere başlığında "(Beta)" ifadesi.
- Tam ekranda isteğe bağlı **sistem verileri**: işlemci, ekran kartı, indirme ve yükleme kartları (anlık değer ve son bir dakikanın eğrisi).
- **Ekran kilidi**: tam ekrana geçmeden tıklamalar ve tuşlar yok sayılır, arkadaki ekran görünür kalır. Esc ya da basılı tutarak çıkılır; isteğe bağlı olarak başlatınca açılır.
- **Yaz/kış saati** ayarı: açıkken geri sayım saat değişiminden etkilenmez, belirli saat Windows saat dilimine göre hesaplanır.
- Tam ekran **kişiselleştirme** ayarları ve zamanlayıcı başlatmadan **önizleme**: gösterge boyutu, ilerleme, hedef saat, kilit etiketi, hareketsizken soldurma.
- **Güvenlik sekmesi**: başlatma ön onayı (işlem özeti penceresi), en kısa süre (varsayılan 5 dk; 1 dk – 1 saat), en uzun süre (varsayılan 24 saat; 6 – 99 saat), kapanış uyarısı, bilgisayar kullanılıyorsa işlemi erteleme, zorla kapatmada ek onay.
- Belirli saatte modunda **Bugün / Yarın** seçimi. Saat, en kısa süreye göre en erken zamanla gelir ve daha erkene alınamaz; 23'ten ileri alınınca seçim Yarın'a geçer.
- Düğmelerin üzerinde bekleyince kısa açıklamalar; Ayarlar > Görünüm'den kapatılabilir.
- Uygulamanın temasına uyan uyarı ve onay penceresi (Windows'un standart mesaj kutusunun yerine).

### Değişti
- Tam ekran ve kapanış uyarısı, uygulamanın kart ve gösterge diliyle baştan tasarlandı; ana pencereyle aynı gösterge denetimi kullanılır ve boyutu sabittir.
- Tam ekran her ekran oranına ve çözünürlüğe uyum sağlar (4:3, ultra geniş, dikey, 4K); Windows'ta geçerli çözünürlük kullanılır, uygulama değiştirmez.
- Klasik gösterge sade bir kadran, Dijital gösterge kutusuz yedi segmentli rakamlar olarak yeniden çizildi.
- Koyu temada vurgu rengi okunaklı hale getirildi: koyulaştırılmış vurgu zemini üzerinde beyaz yazı (en az 4.5:1 kontrast). Seçili kartlar düz mavi yerine hafif vurgu tonu ve vurgu kenarlıkla gösterilir.
- Süre ve saat seçimi büyük, ortalı sayı seçicilerle (fare tekerleği, ok tuşları, basılı tutma) yapılır; hızlı süre düğmeleri büyüdü ve ortalandı.
- Zamanlayıcı çalışırken kurulum ekranının altında durum, büyük "İptal Et" ve onun altında küçük "Yeniden kur" görünür.
- Ayarlar altı sekmeye ayrıldı: Zamanlayıcı, Güvenlik, Uyarılar ve ses, Tam ekran, Görünüm, Geçmiş ve hakkında. Ayarlar ve ana ekrandaki ilgili ikonlar eşleştirildi.
- Sayaç ekranında "Tam ekrana geç" büyük ana düğme oldu. Tam ekrandan çıkış düğmesi alt ortaya alındı, büyüdü ve hareketsizlikte kaybolmak yerine soluklaşır.
- Başlat'ın yanındaki özet metinleri büyüdü; hızlı süre ekleme satırı dar pencerede üst üste binmez.
- Enter tuşu ana penceredeki düğmeleri tetiklemez (sayı kutularında değeri onaylamaya devam eder).
- Sayı seçiciler uçlarda durur, başa dönmez.

### Güvenlik
- `shutdown.exe` ve `explorer.exe` tam yolla çalıştırılır, Windows DLL'leri yalnızca System32'den yüklenir (sahte dosya çalıştırılmasına karşı).
- Ayar ve geçmiş dosyaları önce geçici dosyaya yazılır; yazma yarıda kesilirse eski dosya bozulmaz. Ayar dosyasındaki geçersiz değerler güvenli aralığa çekilir.
- Zorla kapatma seçimi hatırlanmaz, her açılışta kapalı gelir.
- Bilgisayar uykudan dönünce işlem uyarısız yapılmaz, en az 1 dakika süre tanınır.
- Arayüzdeki beklenmeyen hatalar uygulamayı düşürmez, `error.log` dosyasına yazılır (1 MB'de yedeklenir). Ana pencere açılmadan oluşan hatalar yutulmaz.
- Bulanık arka plan alınamazsa tam ekran ve kapanış uyarısı düz arka planla yine açılır.
- Uygulama beklenmedik biçimde kapanırsa kıstığı sesler bir sonraki açılışta geri açılır.
- Sistem ölçümleri arka planda alınır, arayüz takılmaz. Küçük ekranlarda ana pencere çalışma alanına sığdırılır.
- Mica ve Akrilik desteklenmeyen sistemlerde (Windows 10) pencere düz arka plana geçer.

### Düzeltildi
- Onay penceresi uzun süre açık kalırsa hedef geçmişte kalıp işlem hemen uygulanabiliyordu; süre artık onay anında yeniden hesaplanır.
- Sistem saati geri alınınca ölçümler bir saate kadar donup "İşler bitince" modu eski değerlere bakabiliyordu.
- Yaz/kış saati geçiş gününde önizleme ve onay penceresi, işlemin yapılacağı saati gösterir.

## [2.2.0] - 2026-10-31

### Eklendi
- **İşler bitince** modu: indirme/yükleme, işlemci, ekran kartı ve program koşulları birlikte kullanılabilir. Seçilen işlerin hepsi belirlenen süre boyunca sakin kalınca bekleme süresi başlar.
- **Ekran kartı (GPU)** izleme: Windows performans sayaçlarıyla, Görev Yöneticisi'ndeki yöntemle ölçülür. GPU ile render alanlar için.
- **Üst süre sınırı**: işler bitmese bile en geç X saat sonra işlem yapılır (takılı kalan eşitlemelere karşı).
- **Gösterge stilleri**: Modern, Klasik (analog kadran), Dijital (yedi segmentli LED, DSEG yazı tipi), Minimal. Pencerede ve tam ekranda ortak.
- Tam ekran **arka planı**: tema rengi, 8 düz renk ya da bulanık masaüstü (hafif / orta / güçlü).
- Tam ekran **karartma modu**: ekran siyah kalır, fare ya da tuşa dokununca 5 saniye görünür; son uyarıda kendiliğinden açılır.
- **Ekranı kapat** düğmesi (sayaç ekranında): zamanlayıcı çalışırken monitörü kapatır.
- **Geçmiş**: bilgisayarın ne zaman ve neden kapandığı, iptaller ve atlanan işlemler kaydedilir; kurulum ekranında son kayıt görünür.
- Sayaç ekranında koşul durum etiketleri (sürüyor / sakin).

### Değişti
- Hızlı süre düğmeleri artık etiketlerle düzenlenir: saat + dakika girip "Ekle", kaldırmak için ×. En fazla 8 düğme, otomatik sıralanır.
- "İndirme / yükleme bitince", "Program kapanınca" ve "İşlemci boşta" modları tek bir "İşler bitince" modunda birleşti. Eski ayarlar otomatik taşınır.
- Göstergeler `Controls\CountdownDisplay` denetiminde toplandı.

## [2.1.0] - 2026-10-31

### Eklendi
- **İndirme / yükleme bitince** tetikleyicisi: yön seçimi (ikisi / indirme / yükleme), hız eşiği (KB/sn), eşiğin altında kalma süresi, "önce aktarımın başlamasını bekle" seçeneği ve canlı hız göstergesi.
- Koşula bağlı tetikleyiciler (ağ, program, işlemci) için ayarlanabilir **bekleme süresi**. Bu sırada aktarım veya işlemci yükü yeniden başlarsa geri sayım durur ve beklemeye döner.
- **Tam ekran** geri sayım: klavye ve fare tıklamaları yok sayılır, Alt+F4 ile kapanmaz. Esc ya da 1,5 saniye basılı tutulan düğmeyle çıkılır. Saat gösterimi ve hareketsiz imleci gizleme seçenekleri. Sayaç ekranında F11 kısayolu.
- **Ayarlar** sayfası: tema (sistem/açık/koyu), vurgu rengi, pencere arka planı (Mica/Akrilik/Düz), her zaman üstte, hızlı süre düğmeleri, erteleme süresi, son seçimleri hatırlama, uyarı zamanı, uyarı sesi, pencereyi öne getirme, tam ekran seçenekleri, ayar klasörünü açma, varsayılanlara dönme.
- Zamanlayıcı çalışırken pencere kapatılmak istenirse onay sorulur.

### Değişti
- Proje standart klasör yapısına taşındı (`src\`, `dist\`, `archive\`, `tools\`, `LICENSES\`). Kod dosyaları görevlerine göre `Models\`, `Services\`, `Helpers\`, `Views\` klasörlerine ayrıldı ve İngilizce adlandırıldı.
- Program artık `dist\ShutdownMate.exe` konumunda. `Derle.bat` yerini `build.cmd`'ye bıraktı; her derlemede kaynak kod `archive\` altına arşivlenir.
- Ayar dosyası `settings.json` olarak yeniden adlandırıldı (eski `ayarlar.json` ilk kayıtta silinir).
- "İşlemci boşta" ve "Program kapanınca" modlarındaki sabit 60 saniyelik son bekleme yerine ayarlanabilir bekleme süresi kullanılıyor.

## [2.0.1] - 2026-10-31

### Düzeltildi
- "Bilgisayar boşta" modundaki sayı kutuları dar olduğu için değer görünmüyordu.

## [2.0.0] - 2026-10-31

### Eklendi
- WPF UI ile Windows 11 görünümlü yeni arayüz, ayrı kurulum ve sayaç ekranları.
- Yeniden başlat eylemi; program kapanınca ve işlemci boşta tetikleyicileri; erteleme; son seçimleri hatırlama; tek kopya kontrolü.

## [1.0.0] - 2026-10-31

### Eklendi
- İlk sürüm (Windows Forms): kapat / uyku / hazırda beklet, süre veya saat ile zamanlama, zorla kapatma.
