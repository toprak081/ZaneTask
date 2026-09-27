# ZaneTask

Ekipler için proje ve görev yönetimi: projeler, ekip üyeleri, Kanban board, yorumlar ve etiketler.
*Team project management with a kanban board — English summary at the bottom.*

## Gereksinimler
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (PostgreSQL veritabanı için)

## Çalıştırma
1. Docker Desktop'ı aç.
2. Proje klasöründeki **`run.cmd`** dosyasına çift tıkla.

Bu dosya sırasıyla:
- PostgreSQL veritabanını Docker'da başlatır,
- API'yi açar (`http://localhost:5299`) — ilk açılışta veritabanı tablolarını kendisi oluşturur,
- Web arayüzünü açar ve tarayıcıda `http://localhost:5046` adresini açar.

İlk açılışta **"Create an account"** ile kayıt ol. Ekip arkadaşlarını proje ayarlarından
(*Settings → Members*) e-postalarıyla ekleyebilirsin; önce onların da kayıt olması gerekir.

**Durdurmak:** API ve Web pencerelerini kapat. Veritabanını da durdurmak için:
```bash
docker compose stop
```
Verilerin silinmez; bir sonraki `run.cmd` ile kaldığın yerden devam edersin.

### Elle çalıştırma (isteğe bağlı)
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
| `src/ZaneTask.Infrastructure` | Veritabanı (EF Core + PostgreSQL), kullanıcı hesapları, JWT |
| `src/ZaneTask.Api` | REST API — örnek istekler: `ZaneTask.Api.http` |
| `src/ZaneTask.Web` | Blazor WebAssembly arayüzü |
| `tests/` | Domain, servis ve uçtan uca API testleri |
| `design-system/` | Arayüz tasarım kuralları (renkler, tipografi, erişilebilirlik) |

## Yapılandırma
- Geliştirme ayarları: `src/ZaneTask.Api/appsettings.Development.json`
  (veritabanı bağlantısı, JWT anahtarı, izin verilen web adresleri).
- Buradaki JWT anahtarı **yalnızca geliştirme içindir**. Canlı ortamda `Jwt__SigningKey` ortam değişkeni
  ile en az 32 karakterlik gizli bir anahtar verilmelidir; verilmezse API bilerek açılmaz.
- Arayüzün bağlandığı API adresi: `src/ZaneTask.Web/wwwroot/appsettings.json` → `ApiBaseUrl`.

---

## English summary
ZaneTask is a .NET 10 Clean Architecture app (ASP.NET Core API + Blazor WebAssembly) for team
project management. Requirements: .NET 10 SDK and Docker Desktop. Run `run.cmd` (or
`docker compose up -d` and `dotnet run` for `src/ZaneTask.Api` and `src/ZaneTask.Web`), then open
http://localhost:5046. The API applies EF Core migrations on startup in Development.
Run tests with `dotnet test` (no Docker needed).
