# Render.com ile CAKA API – Adım Adım Kurulum

Bu rehber, API’yi ve veritabanını **ücretsiz** Render hesabında çalıştırmanız için yazıldı.

---

## Adım 1: GitHub hesabı ve proje

1. **GitHub.com**’a giriş yapın (hesabınız yoksa ücretsiz açın).
2. Yeni bir **repository** oluşturun:
   - **New repository**
   - İsim: örn. `caka-app`
   - Public seçin, **Create repository** deyin.
3. Bilgisayarınızda proje klasörünü açın:
   - `c:\Users\izcif\Desktop\CAKA v1-14.02`
4. Bu klasörde **Git** başlatıp GitHub’a gönderin. (İlk kez yapacaksanız aşağıdaki komutları **PowerShell** veya **Git Bash** ile çalıştırın.)

```powershell
cd "c:\Users\izcif\Desktop\CAKA v1-14.02"
git init
git add .
git commit -m "CAKA API ve masaüstü uygulaması"
git branch -M main
git remote add origin https://github.com/KULLANICI_ADINIZ/caka-app.git
git push -u origin main
```

`KULLANICI_ADINIZ` yerine kendi GitHub kullanıcı adınızı yazın. GitHub’da repo oluştururken “Add a README” seçmediyseniz bu komutlar sorunsuz çalışır.

---

## Adım 2: Render hesabı

1. **https://render.com** adresine gidin.
2. **Get Started for Free** ile kayıt olun (GitHub ile giriş yapabilirsiniz).
3. Giriş yaptıktan sonra **Dashboard**’a düşeceksiniz.

---

## Adım 3: PostgreSQL veritabanı (Render’da)

1. Render Dashboard’da **New +** → **PostgreSQL**.
2. Ayarlar:
   - **Name:** `caka-db` (veya istediğiniz isim)
   - **Region:** Frankfurt veya size yakın bölge
   - **Plan:** **Free**
3. **Create Database** deyin.
4. Birkaç dakika bekleyin; veritabanı hazır olunca **Info** sekmesine girin.
5. **Internal Database URL** satırını kopyalayın (veya **External Database URL**).  
   Bu adresi **sadece Render’daki API servisiniz** kullanacak; dışarıya vermeyin.  
   Sonraki adımda **Internal Database URL**’i environment variable olarak ekleyeceğiz.

---

## Adım 4: Web Service (API) oluşturma

1. Render Dashboard’da **New +** → **Web Service**.
2. **Connect a repository** bölümünden GitHub repo’nuzu seçin (`caka-app` veya ne ad verdyseniz).  
   Repo görünmüyorsa **Configure account** ile Render’a GitHub erişimi verin.
3. Repo’yu seçtikten sonra ayarlar:

   - **Name:** `caka-api` (veya istediğiniz isim)
   - **Region:** Veritabanı ile aynı (örn. Frankfurt)
   - **Branch:** `main`
   - **Root Directory:** Boş bırakın (proje kökünde olduğu için)
   - **Runtime:** **Docker** seçmeyin; **Native** kalacak (veya sadece “Build” alanını aşağıdaki gibi doldurun).
   - **Build Command:**
     ```bash
     dotnet publish CAKA.Api/CAKA.Api.csproj -c Release -o out
     ```
   - **Start Command:**
     ```bash
     dotnet out/CAKA.Api.dll
     ```
   - **Plan:** **Free**

4. **Advanced**’e tıklayın, **Environment Variables** bölümüne gidin:
   - **Add Environment Variable**
   - **Key:** `DATABASE_URL`
   - **Value:** Adım 3’te kopyaladığınız **Internal Database URL** (PostgreSQL bağlantı adresi).
   - (İsteğe bağlı) JWT için:
     - **Key:** `Jwt__Key`
     - **Value:** En az 32 karakter uzunluğunda rastgele bir metin (örn. `Caka-Guvenli-Jwt-Anahtari-32-Karakter!!`)

5. **Create Web Service** deyin.

6. İlk deploy birkaç dakika sürebilir. Log’da hata yoksa **Open** ile servisi açın; adres şöyle olacaktır:
   - `https://caka-api.onrender.com` (veya verdiğiniz isme göre)

7. Tarayıcıda sadece API adresini açarsanız bazen “Cannot GET /” benzeri bir mesaj görürsünüz; bu normaldir. Asıl kullanım masaüstü uygulaması üzerinden giriş ve API istekleriyle olacak.

---

## Adım 5: Masaüstü uygulamasını API’ye bağlama

1. Bilgisayarınızda proje klasöründe **CAKA.config.json** dosyasını açın (EXE ile aynı klasörde olacak şekilde kullanıyorsanız oradaki dosyayı açın).
2. **ApiBaseUrl** değerini Render’daki API adresinizle değiştirin:

```json
{
  "ApiBaseUrl": "https://caka-api.onrender.com",
  "ApiTimeoutSeconds": 30
}
```

3. `caka-api` kısmını Render’da verdiğiniz Web Service adına göre düzenleyin (Dashboard’daki servis sayfasında tam URL yazar).
4. Uygulamayı çalıştırıp giriş yapın:
   - Admin: **oguzturunc** / **1234**  
   (İlk çalıştırmada API bu kullanıcıyı otomatik oluşturur.)

---

## Özet kontrol listesi

- [ ] GitHub’da repo oluşturuldu ve proje push edildi.
- [ ] Render’da hesap açıldı.
- [ ] Render’da **PostgreSQL** (Free) oluşturuldu.
- [ ] **Internal Database URL** kopyalandı.
- [ ] Render’da **Web Service** (Free) oluşturuldu.
- [ ] Build: `dotnet publish CAKA.Api/CAKA.Api.csproj -c Release -o out`
- [ ] Start: `dotnet out/CAKA.Api.dll`
- [ ] Environment variable: `DATABASE_URL` = Internal Database URL
- [ ] Masaüstü uygulamasında **CAKA.config.json** içinde `ApiBaseUrl` = Render API URL’iniz.

---

## Sık karşılaşılan durumlar

- **Free tier’da uygulama uyur:** 15 dakika istek gelmezse uyur; ilk istekte 30–60 saniye içinde uyanır. Normal davranış.
- **Giriş yapamıyorum:** API URL’inin `https://` ile bittiğinden ve sonunda `/` olmadığından emin olun. CAKA.config.json’da tam örnek: `https://caka-api.onrender.com`
- **Veritabanı hatası:** `DATABASE_URL`’in Web Service’in **Environment** kısmına doğru yapıştırıldığını kontrol edin; boşluk veya eksik karakter olmamalı.

Bu adımları tamamladığınızda API ve verileriniz Render’da ücretsiz çalışır. Takıldığınız adımı yazarsanız, o adımı birlikte netleştirebiliriz.
