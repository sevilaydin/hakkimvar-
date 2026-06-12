# HakkımVar — Türk İş Hukuku AI Asistanı

**HakkımVar**, işçilerin haklarını kolayca öğrenebilmesi için geliştirilmiş yapay zeka destekli bir hukuki bilgi platformudur. Sorularınızı sade Türkçeyle yazın; iş kanununa dayalı, adım adım açıklamalı yanıtlar alın.

**Canlı Demo:** [https://hakkimvar.onrender.com](https://hakkimvar.onrender.com)

---

## Özellikler

- 4857 Sayılı İş Kanunu'na dayalı sorulara anında yanıt
- Kıdem ve ihbar tazminatı hesaplamaları (güncel tavan dahil)
- Yargıtay emsal kararı arama ve listeleme
- 10 kategori otomatik tespiti (kıdem, ihbar, fazla mesai, yıllık izin, mobbing vb.)
- İstek başına kaynak gösterimi (Yargıtay kararları ile desteklenmiş)
- In-memory istatistik paneli (`/stats`)

---

## Teknoloji Yığını

### Backend
| Teknoloji | Versiyon | Kullanım Amacı |
|-----------|----------|----------------|
| ASP.NET Core | 8.0 | Web API sunucusu |
| C# | 12 | Backend dili |
| Groq API | — | llama-3.3-70b-versatile modeli |
| HttpClient | — | Groq ve Yargıtay HTTP çağrıları |

### AI & Model
| Özellik | Açıklama |
|---------|----------|
| Model | llama-3.3-70b-versatile (Groq üzerinden) |
| Sıcaklık | 0.3 (tutarlı, düşük rastgelelik) |
| Max Token | 1500 |
| Timeout | 90 saniye |
| Sistem Prompt | Kıdem/ihbar formülleri, İş Kanunu tam metni, güncel tavan |

### Frontend
| Teknoloji | Kullanım Amacı |
|-----------|----------------|
| Vanilla JS | Sohbet arayüzü |
| HTML5 / CSS3 | UI tasarımı |
| Plus Jakarta Sans | Yazı tipi |

### Altyapı
| Servis | Kullanım Amacı |
|--------|----------------|
| Render.com | Ücretsiz cloud hosting |
| Docker | Container ile deploy |
| GitHub | Kaynak kod yönetimi |

---

## Proje Yapısı

```
HakkimvarBackend/
├── Controllers/
│   ├── ChatController.cs         # POST /api/chat — ana soru-cevap endpoint'i
│   ├── StatsController.cs        # GET  /stats    — istatistik paneli
│   ├── SitemapController.cs      # GET  /sitemap.xml
│   └── SubscriptionController.cs # Stripe entegrasyonu (henüz aktif değil)
├── Services/
│   ├── GroqService.cs            # Groq API çağrısı ve sistem prompt
│   ├── YargitayService.cs        # Yargıtay kararı scraping
│   ├── KanunService.cs           # İş Kanunu metin yükleyici
│   ├── AnalyticsService.cs       # In-memory istatistik
│   └── CategoryService.cs        # Soru kategorisi tespiti (static)
├── Models/
│   ├── ChatRequest.cs            # { Message }
│   ├── ChatResponse.cs           # { Reply, Success, Error, Sources[] }
│   ├── NewsletterSubscriber.cs   # (henüz kullanılmıyor)
│   ├── Question.cs               # (henüz kullanılmıyor)
│   └── Subscription.cs          # (Stripe için, henüz kullanılmıyor)
├── Data/
│   └── is_kanunu.txt             # 4857 Sayılı İş Kanunu tam metni
├── wwwroot/
│   ├── robots.txt
│   └── istatistik.html           # /stats görsel paneli
├── Program.cs                    # Uygulama başlangıcı, middleware, DI
├── appsettings.json              # Geliştirme konfigürasyonu
├── Dockerfile                    # Multi-stage Docker build
└── Hakkimvar.csproj
```

---

## API Endpoint'leri

| Metod | Endpoint | Auth | Rate Limit | Açıklama |
|-------|----------|------|-----------|----------|
| POST | `/api/chat` | Yok | 15 istek/dk/IP | İş hukuku sorusu sor |
| GET | `/stats` | Opsiyonel (`X-Stats-Key`) | Yok | Kullanım istatistikleri |
| GET | `/sitemap.xml` | Yok | Yok | SEO sitemap |
| GET | `/health` | Yok | Yok | Sağlık kontrolü |

### `/api/chat` İstek / Yanıt

```json
// İstek
POST /api/chat
Content-Type: application/json
{ "message": "3 yıl çalıştım, işten çıkarıldım. Kıdem tazminatım ne kadar?" }

// Yanıt
{
  "reply": "...",
  "success": true,
  "error": null,
  "sources": [
    {
      "title": "Yargıtay 9. HD - E.2024/1234 K.2024/5678",
      "summary": "...",
      "url": "https://..."
    }
  ]
}
```

---

## Soru Kategorileri

| Kategori | Tetikleyen Kelimeler |
|----------|----------------------|
| `kidem_tazminati` | kıdem, tazminat |
| `ihbar_tazminati` | ihbar, bildirim süresi |
| `fazla_mesai` | fazla mesai, mesai |
| `yillik_izin` | izin, yıllık |
| `mobbing` | mobbing, taciz, psikolojik |
| `is_kazasi` | iş kazası, kaza |
| `istifa` | istifa |
| `haksiz_fesih` | fesih, işten çıkar |
| `ucret_uyusmaz` | maaş, ücret |
| `emeklilik_sgk` | emekli, sgk, sigorta |

---

## Hesaplama Formülleri

### Kıdem Tazminatı
```
Giydirilmiş Brüt Aylık Ücret × Çalışma Yılı
(Her yıl için tavan: 64.948,77 TL — Ocak-Haziran 2026)
```

### İhbar Tazminatı
```
Günlük Brüt Ücret (Aylık / 30) × İhbar Günleri

İhbar Süreleri:
  0–6 ay   → 2 hafta (14 gün)
  6–18 ay  → 4 hafta (28 gün)
  18–36 ay → 6 hafta (42 gün)
  36 ay+   → 8 hafta (56 gün)
```

---

## Kurulum

```bash
git clone https://github.com/sevilaydin/hakkimvar-.git
cd hakkimvar-
```

`appsettings.json` veya ortam değişkeni olarak Groq API anahtarını ekleyin:

```json
{
  "Groq": {
    "ApiKey": "gsk_..."
  },
  "Stats": {
    "ApiKey": "isteğe-bağlı-güvenli-anahtar"
  },
  "KidemTazminati": {
    "Tavan": 64948.77,
    "Donem": "Ocak-Haziran 2026",
    "YururlukTarihi": "01.01.2026",
    "SonrakiGuncellemeTarihi": "2026-07-01"
  }
}
```

```bash
dotnet run
# http://localhost:5000 adresinde çalışır
```

### Docker ile Çalıştırma

```bash
docker build -t hakkimvar .
docker run -p 10000:10000 -e Groq__ApiKey=gsk_... hakkimvar
```

---

## Konfigürasyon

| Ortam Değişkeni | Açıklama | Varsayılan |
|-----------------|----------|-----------|
| `PORT` | Sunucu portu | `5000` |
| `Groq__ApiKey` | Groq API anahtarı | — |
| `Stats__ApiKey` | `/stats` endpoint koruma anahtarı | boş (korumasız) |
| `KidemTazminati__Tavan` | Güncel kıdem tazminatı tavanı (TL) | `64948.77` |
| `KidemTazminati__Donem` | Tavanın geçerli dönemi | `Ocak-Haziran 2026` |
| `KidemTazminati__YururlukTarihi` | Tavanın yürürlük tarihi | `01.01.2026` |

---

## İstatistik Paneli

`/stats` endpoint'i aşağıdaki metrikleri döner:

```json
{
  "total_questions": 1240,
  "total_errors": 3,
  "by_category": { "kidem_tazminati": 412, "ihbar_tazminati": 198, ... },
  "busiest_hour_utc": 14,
  "recent_questions": [ { "asked_at": "12.06 14:32", "category": "kidem_tazminati", "preview": "...", "response_ms": 1850 } ],
  "as_of_utc": "2026-06-12T14:45:00Z"
}
```

Header ekleyerek korumalı erişim: `X-Stats-Key: <anahtarınız>`

> Not: İstatistikler in-memory tutulur; uygulama yeniden başladığında sıfırlanır.

---

## Yol Haritası

- [ ] Stripe ödeme entegrasyonu (Premium plan)
- [ ] PostgreSQL ile kalıcı veri depolama
- [ ] Bülten aboneliği (e-posta)
- [ ] CORS kısıtlaması (production için)
- [ ] Kullanıcı geri bildirim sistemi (yıldız puanı)

---

## Lisans

Bu proje eğitim ve kamu yararı amaçlı geliştirilmiştir. Verilen bilgiler genel nitelikte olup hukuki tavsiye yerine geçmez. Hukuki işlemler için bir avukattan destek almanızı öneririz.
