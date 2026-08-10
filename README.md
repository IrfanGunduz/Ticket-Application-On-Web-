# Ticket Application

> !!!!**VIBE CODED PROJECT**!!!!! !!!!**VIBE CODED PROJECT**!!!!!<br>
> Ticket application for company usage. Turkish language only for now!

ASP.NET Core MVC ile geliştirilmiş, SQL Server tabanlı bir destek talebi yönetim uygulamasıdır. Müşteri ve sorun/hizmet kayıtlarını merkezi olarak yönetir; taleplerin açılmasını, personele atanmasını, durum değişikliklerini ve işlem geçmişini takip eder. IMAP veya POP3 üzerinden gelen e-postaları otomatik olarak talebe dönüştürebilir.

## Özellikler

- Manuel, e-posta ve telefon kanallarından talep oluşturma
- Talep numarası, konu, durum, kanal, müşteri ve sorumluya göre arama/filtreleme
- Talepleri kullanıcıya atama; durum, müşteri ve sorun/hizmet bilgisini güncelleme
- Dahili notlar, gelen e-postalar ve işlem geçmişiyle ayrıntılı talep zaman çizelgesi
- Aktif/pasif müşteri kayıtları ve müşteriye bağlı iletişim kişileri
- Departman bilgisi içeren sorun/hizmet kataloğu
- IMAP ve POP3 ile periyodik e-posta alımı
- E-posta gönderenini kayıtlı müşteri veya izinli müşteri kişisiyle eşleştirme
- Ticket numarası ve e-posta başlıkları (`Message-Id`, `In-Reply-To`, `References`) ile konuşma zinciri eşleştirme
- Aynı e-postanın yeniden işlenmesini önleyen kayıt mekanizması
- Rol bazlı yetkiler ile kullanıcıya özel izin verme/reddetme
- Açık, atanmamış ve bugün oluşturulan talepler için gösterge paneli
- En çok talep açan müşteriler, en sık karşılaşılan sorunlar ve son 12 aylık adetler için raporlar
- İlk çalıştırmada web arayüzünden SQL bağlantı testi, veritabanı oluşturma, migration ve başlangıç verisi kurulumu

## Teknolojiler

- .NET 9 / ASP.NET Core MVC
- Entity Framework Core 9
- SQL Server
- ASP.NET Core Identity
- MailKit (IMAP ve POP3)
- Bootstrap ve jQuery Validation

## Proje yapısı

```text
Ticket.sln
├── Ticket.Domain          # Domain katmanı için ayrılmış çekirdek proje
├── Ticket.Application     # Uygulama soyutlamaları ve servis sözleşmeleri
├── Ticket.Infrastructure  # EF Core, Identity, veri modelleri ve migration'lar
└── Ticket.Web             # MVC arayüzü, kurulum, yetkilendirme ve e-posta worker'ı
```

Katman bağımlılıkları:

```text
Ticket.Web
├── Ticket.Application ─────> Ticket.Domain
├── Ticket.Infrastructure ──> Ticket.Application + Ticket.Domain
└── Ticket.Domain
```

## Gereksinimler

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server, SQL Server Express veya erişilebilir bir SQL Server instance'ı
- Veritabanı yoksa `CREATE DATABASE` yetkisi; varsa migration çalıştırabilecek tablo/şema yetkileri
- HTTPS geliştirme profili kullanılacaksa güvenilir ASP.NET Core geliştirme sertifikası

Uygulama özellikle Windows ve IIS kurulumları düşünülerek geliştirilmiştir. SQL Server bağlantısı Windows Authentication veya SQL Server Authentication ile kurulabilir.

## Hızlı başlangıç

Depoyu klonlayın:

```bash
git clone https://github.com/IrfanGunduz/Ticket-Application-On-Web-.git
cd Ticket-Application-On-Web-
```

Bağımlılıkları yükleyip uygulamayı başlatın:

```bash
dotnet restore Ticket.sln
dotnet run --project Ticket.Web
```

Geliştirme profilleri varsayılan olarak aşağıdaki adresleri kullanır:

- `https://localhost:7021`
- `http://localhost:5065`

HTTPS sertifikası uyarısı alırsanız:

```bash
dotnet dev-certs https --trust
```

## İlk kurulum

Uygulama ilk açılışta otomatik olarak `/Setup` sayfasına yönlendirir.

1. SQL Server adresini girin. Örnek: `localhost`, `SUNUCU\\SQLEXPRESS` veya `10.0.0.5,1433`.
2. Veritabanı adını girin. Varsayılan ad `TicketDatabase`'dir.
3. Windows Authentication ya da SQL Authentication seçin.
4. Gerekirse şifreleme ve sunucu sertifikasına güven seçeneklerini düzenleyin.
5. Önce **Test Connection** ile bağlantı ve yetkileri kontrol edin.
6. **Kur (Migrate + Seed)** ile veritabanını oluşturun, migration'ları uygulayın ve başlangıç verilerini ekleyin.
7. Kurulum tamamlandığında giriş ekranına yönlendirilirsiniz.

### Başlangıç kullanıcıları

| Kullanıcı | Parola | Rol |
| --- | --- | --- |
| `Admin` | `1234` | Admin |
| `User1` | `1234` | User |
| `User2` | `1234` | User |
| `User3` | `1234` | User |

> [!WARNING]
> Bu hesaplar yalnızca ilk kurulum içindir. Özellikle `Admin` parolasını ilk girişten hemen sonra değiştirin; kullanılmayacak örnek kullanıcıları silin veya parolalarını yenileyin.

## Yetkilendirme

Uygulamada tüm sayfalar varsayılan olarak oturum açmayı gerektirir. `Admin` rolü bütün izinlere sahiptir. `User` rolüne ilk kurulumda talep görüntüleme/oluşturma ile müşteri ve sorun/hizmet görüntüleme izinleri verilir.

Tanımlı izinler:

| Alan | İzinler |
| --- | --- |
| Talepler | `Tickets.View`, `Tickets.Create`, `Tickets.Edit`, `Tickets.Assign` |
| Müşteriler | `Customers.View`, `Customers.Edit` |
| Sorun/Hizmet | `Problems.View`, `Problems.Edit` |
| Raporlar | `Reports.View` |
| Yönetim | `Admin.Users`, `Admin.Permissions` |

Admin menüsünden rol izinleri değiştirilebilir; ayrıca belirli bir kullanıcıya rolünden bağımsız izin verilebilir veya izin reddi uygulanabilir. Yetki değişiklikleri sonraki isteklerde yeniden hesaplanır.

## E-posta entegrasyonu

E-posta alımını yapılandırmak için `Admin > Mail Ayarları` sayfasını kullanın:

- Protokol olarak IMAP veya POP3 seçin.
- Sunucu, port, SSL, kullanıcı adı ve parola bilgilerini girin.
- Kontrol aralığını 5-3600 saniye arasında belirleyin.
- İsterseniz yalnızca belirli bir `To/Cc` hedef adresine gelen iletileri işleyin.
- IMAP kullanırken klasör seçebilir ve işlenen iletileri okundu olarak işaretleyebilirsiniz.

Worker her döngüde en fazla son 25 uygun iletiyi inceler. IMAP yalnızca okunmamış iletileri tarar; POP3 iletileri sunucudan silmez.

Bir e-postanın işlenebilmesi için gönderen adresinin aşağıdakilerden biriyle eşleşmesi gerekir:

- Aktif bir müşterinin e-posta adresi
- Aktif ve **e-posta alımına izinli** bir müşteri iletişim kişisinin e-posta adresi

Eşleşmeyen iletiler talep oluşturmaz ve tekrar işlenmemeleri için sonuç kaydı tutulur. Kapalı, iptal edilmiş veya müşteri bekleyen bir talebe yeni e-posta geldiğinde talep tekrar **İşlemde** durumuna alınır.

## Yapılandırma ve veri güvenliği

İlk kurulumdan sonra bağlantı bilgileri kaynak depoya veya `appsettings.json` dosyasına yazılmaz. Windows üzerinde çalışma zamanı dosyaları aşağıdaki konumda tutulur:

```text
%ProgramData%\Ticket\setup.json
%ProgramData%\Ticket\keys\
```

- Bağlantı dizesi ASP.NET Core Data Protection ile şifrelenerek `setup.json` içinde saklanır.
- IMAP ve POP3 parolaları şifrelenmiş olarak veritabanında tutulur.
- `keys` klasörü kaybolursa kayıtlı bağlantı dizesi ve e-posta parolaları çözülemez. Üretimde bu klasörü güvenli biçimde yedekleyin ve yalnızca uygulama kimliğine gerekli dosya izinlerini verin.
- `setup.json`, Data Protection anahtarları ve gerçek bağlantı bilgileri Git'e eklenmemelidir.

## Veritabanı ve migration'lar

Normal kurulumda migration'lar `/Setup` akışı tarafından otomatik uygulanır. Yeni bir migration geliştirmek için `dotnet-ef` aracını kurun ve tasarım zamanı bağlantı dizesini `TICKET_CONNECTION` ortam değişkeniyle sağlayın.

PowerShell örneği:

```powershell
$env:TICKET_CONNECTION="Server=localhost;Database=TicketDatabase;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True"
dotnet ef migrations add MigrationAdi --project Ticket.Infrastructure --startup-project Ticket.Web
dotnet ef database update --project Ticket.Infrastructure --startup-project Ticket.Web
```

> `TICKET_CONNECTION` yalnızca EF Core tasarım zamanı işlemleri içindir. Uygulamanın normal çalışma zamanı bağlantısı `/Setup` tarafından kaydedilen şifreli yapılandırmadan okunur.

## Derleme ve yayınlama

Release derlemesi:

```bash
dotnet build Ticket.sln --configuration Release
```

Dağıtıma hazır çıktı üretmek için:

```bash
dotnet publish Ticket.Web/Ticket.Web.csproj --configuration Release --output ./artifacts/publish
```

IIS ile yayınlarken:

- Sunucuda uygun .NET 9 Hosting Bundle kurulu olmalıdır.
- Uygulama havuzu kimliğine `%ProgramData%\Ticket` altında okuma/yazma izni verilmelidir.
- Windows Authentication ile SQL Server'a bağlanılacaksa uygulama havuzu kimliğinin SQL Server yetkileri ayrıca tanımlanmalıdır.
- Reverse proxy veya IIS üzerinde HTTPS yapılandırılmalıdır.

## Geliştirme komutları

```bash
# Çözümü geri yükle
dotnet restore Ticket.sln

# Çözümü derle
dotnet build Ticket.sln

# Web uygulamasını çalıştır
dotnet run --project Ticket.Web

# Release çıktısı üret
dotnet publish Ticket.Web/Ticket.Web.csproj -c Release -o ./artifacts/publish
```

Projede şu anda otomatik test projesi bulunmamaktadır. Değişikliklerden sonra en azından çözümün Release yapılandırmasında derlenmesi ve kritik kurulum/talep/e-posta akışlarının test ortamında doğrulanması önerilir.

## Kullanım ve telif

> “Viewing allowed”
>
> “No copying, modification, distribution, or commercial/non-commercial use without written permission”
>
> “All rights reserved”

Depoda ayrıca standart bir açık kaynak lisans dosyası bulunmamaktadır.
