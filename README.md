# ZaneTask

Ekipler için proje ve görev yönetimi: projeler, ekip üyeleri, Kanban board, yorumlar ve etiketler.
*Team project management with a kanban board — English summary at the bottom.*

## Masaüstü uygulaması (günlük kullanım)
Gereksinim: [.NET 10 SDK](https://dotnet.microsoft.com/download). Docker **gerekmez**.

1. Proje klasöründeki **`install.cmd`** dosyasına çift tıkla (ilk kurulum 1-2 dakika sürer).
2. Masaüstündeki (veya Başlat menüsündeki) **ZaneTask** ikonuna çift tıkla.

- Uygulama kendi penceresinde açılır; pencereyi kapatınca arka planda hiçbir şey çalışmaz.
- Veriler tek bir dosyada tutulur: `%LOCALAPPDATA%\ZaneTask\zanetask.db`.
  Yedek almak için uygulama kapalıyken bu dosyayı kopyalaman yeterli.
- Bir kez giriş yaparsın; 30 gün boyunca tekrar sormaz.
- **Güncellemek:** kodda değişiklik olunca `install.cmd`'yi tekrar çalıştır. Verilerin silinmez; kurulum önce
  `%LOCALAPPDATA%\ZaneTask\backups\TARİH` klasörüne otomatik yedek alır (son 5 yedek saklanır).
- **Yedekten geri dönmek:** uygulamayı kapat, `%LOCALAPPDATA%\ZaneTask` içindeki `zanetask.db`, `zanetask.db-wal` ve
  `zanetask.db-shm` dosyalarını sil, istediğin yedek klasöründeki dosyaları oraya kopyala. (Yedek daha eski bir
  sürümden ise o sürümü kurmak için `git checkout <eski-commit>` ve `install.cmd`.)
- Sorun olursa sunucu günlüğü: `%LOCALAPPDATA%\ZaneTask\logs\server.log`.

Program dosyaları `%LOCALAPPDATA%\Programs\ZaneTask` klasörüne kurulur. Kaldırmak için bu klasörü ve
masaüstü/Başlat menüsü kısayollarını silmen yeterli (verilerin ayrı klasörde durur).

## Geliştirici modu (PostgreSQL + Docker)
İleride sunucuya taşınacak yapı budur; kod üzerinde çalışırken kullanılır.

Gereksinimler: .NET 10 SDK ve [Docker Desktop](https://www.docker.com/products/docker-desktop/).

1. Docker Desktop'ı aç.
2. **`run.cmd`** dosyasına çift tıkla. Bu dosya PostgreSQL'i Docker'da, API'yi (`http://localhost:5299`)
   ve Web arayüzünü (`http://localhost:5046`) ayrı pencerelerde başlatır.

**Durdurmak:** API ve Web pencerelerini kapat; veritabanı için `docker compose stop`.

### Elle çalıştırma
```bash
docker compose up -d
dotnet run --project src/ZaneTask.Api --launch-profile http
dotnet run --project src/ZaneTask.Web --launch-profile http
```

## Testler
```bash
dotnet test
```
Testler Docker gerektirmez (SQLite kullanır).

## Proje yapısı
| Klasör | İçerik |
|---|---|
| `src/ZaneTask.Domain` | İş kuralları: proje, üye, görev, etiket, yorum, Kanban sıralaması |
| `src/ZaneTask.Application` | Kullanım senaryoları (servisler) ve yetki kontrolleri |
| `src/ZaneTask.Contracts` | API ile arayüzün paylaştığı veri tipleri |
| `src/ZaneTask.Infrastructure` | Veritabanı (EF Core; PostgreSQL veya SQLite), kullanıcı hesapları, JWT |
| `src/ZaneTask.Infrastructure.Sqlite` | SQLite (masaüstü) veritabanı migration'ları |
| `src/ZaneTask.Api` | REST API — örnek istekler: `ZaneTask.Api.http` |
| `src/ZaneTask.Web` | Blazor WebAssembly arayüzü |
| `src/ZaneTask.Desktop` | Masaüstü uygulaması: sunucuyu başlatır, arayüzü kendi penceresinde (WebView2) gösterir |
| `tests/` | Domain, servis ve uçtan uca API testleri |
| `design-system/` | Arayüz tasarım kuralları (renkler, tipografi, erişilebilirlik) |

## Yapılandırma
- Veritabanı türü: `Database:Provider` = `Postgres` (varsayılan) veya `Sqlite` (masaüstü uygulaması).
- Geliştirme ayarları: `src/ZaneTask.Api/appsettings.Development.json`
  (veritabanı bağlantısı, JWT anahtarı, izin verilen web adresleri).
- Buradaki JWT anahtarı **yalnızca geliştirme içindir**. Canlı ortamda `Jwt__SigningKey` ortam değişkeni
  ile en az 32 karakterlik gizli bir anahtar verilmelidir; verilmezse API bilerek açılmaz.
- Arayüzün bağlandığı API adresi: `ApiBaseUrl` (boşsa arayüzü sunan adres kullanılır; geliştirici modunda
  `src/ZaneTask.Web/wwwroot/appsettings.Development.json` içinde `http://localhost:5299`).

---

## English summary
ZaneTask is a .NET 10 Clean Architecture app (ASP.NET Core API + Blazor WebAssembly) for team
project management. **Desktop app:** run `install.cmd` (needs only the .NET 10 SDK), then start ZaneTask
from the Desktop shortcut; it runs the server on 127.0.0.1 with a local SQLite file in
`%LOCALAPPDATA%\ZaneTask` and shows the UI in a WebView2 window. **Developer mode:** `run.cmd`
(PostgreSQL in Docker, API on :5299, web on :5046).
Run tests with `dotnet test` (no Docker needed).
