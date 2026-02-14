# CAKA Web API

Masaüstü uygulamasının kullanıcı ve iş kayıtları bu API üzerinden tutulur.

## Yerel çalıştırma

```bash
cd CAKA.Api
dotnet run
```

Varsayılan adres: **https://localhost:5001** ve **http://localhost:5000**

İlk çalıştırmada SQLite veritabanı (`caka.db`) ve varsayılan admin hesabı oluşturulur:
- **Kullanıcı adı:** oguzturunc
- **Şifre:** 1234

## Masaüstü uygulamasını bağlama

Masaüstü uygulaması (EXE) ile aynı klasörde `CAKA.config.json` dosyası bulunur. API adresini burada belirtin:

```json
{
  "ApiBaseUrl": "https://localhost:5001",
  "ApiTimeoutSeconds": 30
}
```

Canlı sunucu kullanıyorsanız `ApiBaseUrl` değerini sunucu adresinizle değiştirin (örn: `https://api.sirketiniz.com`).

## Sunucuya yayımlama

1. **dotnet publish** ile yayımlayın:
   ```bash
   dotnet publish -c Release -o ./publish
   ```
2. `publish` klasörünü sunucuya kopyalayın.
3. Sunucuda çalıştırın: `dotnet CAKA.Api.dll`
4. IIS veya nginx ile reverse proxy kurabilirsiniz; HTTPS kullanın.

Veritabanı dosyası (`caka.db`) API’nin çalıştığı dizinde oluşur. Yedek almayı unutmayın.
