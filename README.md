# NetCoreBackend

Northwind senaryosu üzerinden hazırlanmış, JWT tabanlı kimlik doğrulama içeren
bir ASP.NET Core Web API örneği. Proje; ürün, kategori ve kullanıcı işlemlerini
katmanlı bir yapı içinde ele alıyor. Ortak işlemler Core katmanında tutuluyor,
veri erişimi ve iş kuralları birbirinden ayrılıyor.

Bu sürüm .NET 10 üzerinde çalışacak şekilde güncellendi. Autofac ile dependency
injection ve interceptor tabanlı aspect yapısı kullanılıyor. Validasyon,
önbellekleme, loglama, transaction ve yetkilendirme gibi ortak ihtiyaçlar da bu
yapı üzerinden yönetiliyor.

## Gereksinimler

- .NET SDK `10.0.401` veya .NET 10 SDK
- SQL Server ve `Northwind` veritabanı
- Geliştirme ortamında Swagger için tarayıcı

SDK sürümü `global.json` ile tanımlı. Bilgisayarında bu sürüm bulunmuyorsa .NET
10 SDK’nın kurulu olması ve Rider’ın bu SDK’yı kullanması yeterli.

Veri erişimi şu an `DataAccess/Concrete/Entityframework/Contexts/NorthwindContext.cs`
içindeki SQL Server LocalDB bağlantısını kullanıyor. Kullanılan Northwind örnek
veritabanı, [W3Schools Northwind sample database](https://www.w3schools.com/sql/sql_intro.asp)
üzerindeki örnek veri modelinden alınmıştır:

```text
Server=(localdb)\mssqllocaldb;
Database=Northwind;
Trusted_Connection=true;
```

LocalDB yerine başka bir SQL Server kullanıyorsan bağlantı bilgisini bu sınıfta
ortamına göre değiştirmen gerekir.

## Çalıştırma

Projeyi aldıktan sonra kök dizinde:

```bash
dotnet restore
dotnet build NetCoreBackend.sln --configuration Release
dotnet run --project WebAPI/WebAPI.csproj
```

Varsayılan geliştirme adresleri:

- HTTPS: `https://localhost:7182`
- HTTP: `http://localhost:5052`
- Swagger: `https://localhost:7182/swagger`

Rider kullanıyorsan `WebAPI` projesini başlangıç projesi olarak seçip doğrudan
çalıştırabilirsin. Uygulama açıldığında Swagger üzerinden endpoint’leri ve
beklenen istek modellerini görebilirsin.

## API

Tüm endpoint’lerin temel adresi `api` yoludur.

### Kimlik doğrulama

| Method | Endpoint | Açıklama |
|---|---|---|
| `POST` | `/api/Auth/register` | Yeni kullanıcı oluşturur ve access token üretir. |
| `POST` | `/api/Auth/login` | Kullanıcı girişi yapar ve access token üretir. |

Kayıt isteği:

```json
{
  "email": "user@example.com",
  "password": "Password123!",
  "firstName": "Tahsin",
  "lastName": "Cr"
}
```

Giriş isteği:

```json
{
  "email": "user@example.com",
  "password": "Password123!"
}
```

Başarılı girişten sonra dönen token’ı korumalı isteklerde şu header ile gönder:

```text
Authorization: Bearer <access-token>
```

### Ürünler

| Method | Endpoint | Açıklama |
|---|---|---|
| `GET` | `/api/Products/getall` | Tüm ürünleri getirir. |
| `GET` | `/api/Products/getbyid?id=1` | Id’ye göre ürün getirir. |
| `GET` | `/api/Products/getallbycategoryid?id=1` | Kategoriye ait ürünleri getirir. |
| `POST` | `/api/Products/add` | Yeni ürün ekler. |
| `GET` | `/api/Products/update` | Ürün günceller. |
| `POST` | `/api/Products/delete` | Ürün siler. |

Ürün modeli:

```json
{
  "productId": 1,
  "categoryId": 1,
  "productName": "Chai",
  "unitsInStock": 39,
  "unitPrice": 18.0
}
```

Ürün ekleme, güncelleme ve silme işlemleri aspect tabanlı yetkilendirme ile
korunur. Kullanıcının uygun operation claim’e sahip olması gerekir.

### Kategoriler

| Method | Endpoint | Açıklama |
|---|---|---|
| `GET` | `/api/Categorys/getall` | Tüm kategorileri getirir. |
| `GET` | `/api/Categorys/getbyid?id=1` | Id’ye göre kategori getirir. |
| `POST` | `/api/Categorys/add` | Yeni kategori ekler. |
| `GET` | `/api/Categorys/update` | Kategori günceller. |
| `POST` | `/api/Categorys/delete` | Kategori siler. |

## Yapı hakkında

Uygulamada ortak sözleşmeler, sonuç modelleri, JWT yardımcıları, EF repository
temeli, caching, logging, validation ve aspect altyapısı Core içinde tutuluyor.
Entities veri modellerini, DataAccess veritabanı erişimini, Business iş
kurallarını ve servisleri, WebAPI ise HTTP katmanını üstleniyor. ConsoleUI da
aynı servis ve iş katmanlarını kullanabilen ayrı bir giriş noktası olarak
çözümde yer alıyor.

Bu ayrımın amacı, iş kurallarını controller’lara taşımadan API ve konsol gibi
farklı giriş noktalarından tekrar kullanabilmek.

## Yapılandırma

JWT ayarları `WebAPI/appsettings.json` içindeki `TokenOptions` bölümünde bulunur:

```json
{
  "TokenOptions": {
    "Audience": "your-audience",
    "Issuer": "your-issuer",
    "AccessTokenExpiration": 10,
    "SecurityKey": "your-long-secret-key"
  }
}
```

Gerçek ortamlarda güvenlik anahtarını kaynak kodunda veya commit edilmiş
ayar dosyasında tutma. User Secrets, ortam değişkenleri veya kullandığın
deployment platformunun secret yönetimini tercih et.

`log4net.config` dosyası loglama yapılandırmasını belirler. Geliştirme ortamında
uygulamayı çalıştırdığın çalışma dizininde bu dosyanın erişilebilir olduğundan
emin olmalısın.

## Geliştirme ve doğrulama

Release build’i kontrol etmek için:

```bash
dotnet restore NetCoreBackend.sln
dotnet build NetCoreBackend.sln --configuration Release
```

Şu an çözümde otomatik test projesi bulunmuyor. Bu nedenle API davranışını
Swagger veya kendi istemcin üzerinden veritabanı bağlantısı ile birlikte
kontrol etmek gerekiyor.

## Sürüm ve lisans

Mevcut sürüm `v1.0` tag’i ile yayınlandı. Değişikliklerin tamamı
[CHANGELOG.md](CHANGELOG.md) dosyasında tutuluyor.

Bu proje [MIT License](LICENSE) ile lisanslanmıştır.
