# Adım 4: Web Service (API) – Tek Tek Yapılacaklar

Render’da ekran ekran ilerleyeceğiz. Hangi adımda takıldığını yazarsan oradan devam ederiz.

---

## 4.1 – Web Service sayfasını aç

1. Render Dashboard’da sağ üstte **New +** (veya **Add New**) butonuna tıkla.
2. Açılan listeden **Web Service**’i seç.

---

## 4.2 – GitHub repo’yu bağla

1. **Connect a repository** veya **Connect account** bölümü gelecek.
2. **GitHub** ile bağlanacaksın. “Connect GitHub” / “Configure account” varsa tıkla ve GitHub hesabını seç / yetki ver.
3. Bağlandıktan sonra repo listesi çıkar. **caka-app** (veya projeyi attığın repo adı) görünmeli.
4. **caka-app**’in yanındaki **Connect** (veya **Select**) butonuna tıkla.

Repo görünmüyorsa: Render’ın GitHub’a erişim izni verdiğinden ve repo’nun “Public” olduğundan emin ol.

---

## 4.3 – Temel ayarlar (Name, Region, Branch)

Açılan formda şunları doldur:

| Alan | Ne yazacaksın |
|------|----------------|
| **Name** | `caka-api` (veya istediğin isim; boşluk kullanma) |
| **Region** | PostgreSQL’i oluşturduğun bölge (örn. **Frankfurt (EU Central)**). Aynı bölge olsun. |
| **Branch** | `main` (zaten seçili olabilir) |

**Root Directory** varsa boş bırak (proje repo’nun kökünde).

---

## 4.4 – Runtime / Environment

- **Runtime** veya **Environment** seçeneği varsa:
  - **Docker** seçme.
  - **Native** veya **.NET** / **Node** gibi bir seçenek varsa ve .NET görünüyorsa onu seç; yoksa **Native** veya varsayılan kalsın.

Bazen Render otomatik “.NET” veya “Docker” tespit eder. **Docker** seçiliyse değiştirip **Native** (veya .NET) yapmaya çalış; değiştiremiyorsan bir sonraki adımda Build/Start komutlarını doğru yazmak yeterli.

---

## 4.5 – Build ayarları

**Build Command** (veya “Build command”, “Build”) kutusunu bul ve **tam olarak** şunu yaz:

```bash
dotnet publish CAKA.Api/CAKA.Api.csproj -c Release -o out
```

- Bu alan yoksa: **Advanced** veya **Build & Deploy** / **Build Settings** gibi bir sekme veya “Show advanced options” aç; Build Command orada olur.
- Bazı sayfalarda önce “Build command” alanını gösteren bir toggle (örn. “Override build command”) açman gerekebilir; açıp yukarıdaki komutu yaz.

**Start Command** (veya “Start command”, “Start”) kutusunu bul ve **tam olarak** şunu yaz:

```bash
dotnet out/CAKA.Api.dll
```

Bu iki satırı kopyalayıp yapıştır; başında/sonunda fazladan boşluk olmasın.

---

## 4.6 – Plan

- **Instance Type** veya **Plan** bölümünde **Free** seçili olsun.

---

## 4.7 – Environment Variables (DATABASE_URL)

1. **Environment** veya **Environment Variables** bölümünü bul (bazen **Advanced** açınca çıkar).
2. **Add Environment Variable** veya **+ Add** / **Key**–**Value** ekle.
3. İlk değişken:
   - **Key:** `DATABASE_URL`
   - **Value:** Daha önce kopyaladığın PostgreSQL adresi (postgresql:// ile başlayan, tek satır).
   - Örnek:  
     `postgresql://caka_db_user:ŞifreBuraya@dpg-xxxxx-a/caka_db`  
     Kendi kopyaladığın tam metni yapıştır.
4. **Save** / **Add** ile ekle.

İsteğe bağlı (şimdilik atlayabilirsin):  
- **Key:** `Jwt__Key`  
- **Value:** En az 32 karakter rastgele metin (örn. `Caka-Guvenli-Jwt-Anahtari-32-Karakter!!`)

---

## 4.8 – Servisi oluştur

1. Sayfanın altında **Create Web Service** (veya **Deploy**) butonuna tıkla.
2. İlk deploy 3–5 dakika sürebilir. **Logs** sekmesinde build adımlarını izleyebilirsin.
3. Hata çıkarsa log’daki **kırmızı hata mesajını** kopyalayıp yaz; birlikte bakarız.

---

## 4.9 – API adresini al

1. Deploy başarıyla bitince servis sayfasında üstte **URL** çıkar (örn. `https://caka-api.onrender.com`).
2. Bu adresi kopyala; masaüstü uygulamasındaki **CAKA.config.json** içinde **ApiBaseUrl** olarak kullanacaksın.

---

## Hangi adımda takıldın?

Lütfen şunu yaz:

- “4.2’deyim, repo listesinde caka-app yok.”
- “4.5’te Build Command kutusu yok.”
- “4.7’de Environment Variables nerede?”
- “4.8’den sonra log’da şu hata var: …”

Buna göre bir sonraki adımı net söylerim.
