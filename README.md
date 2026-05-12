# HakkımVar — Türk İş Hukuku AI Asistanı

**HakkımVar**, işçilerin haklarını kolayca öğrenebilmesi için geliştirilmiş bir yapay zeka destekli hukuki bilgi platformudur.

## Özellikler

- İş Kanunu'na dayalı sorulara anında yanıt
- Gerçek zamanlı web araması ile güncel mevzuat ve rakamlar (asgari ücret, kıdem tazminatı tavanı vb.)
- Yargıtay emsal kararı arama ve listeleme
- Konu bazlı sohbet geçmişi (İşten Çıkarma, Maaş & Ücret, İzinler vb.)

## Teknolojiler

### Backend
| Teknoloji | Versiyon | Kullanım Amacı |
|-----------|----------|----------------|
| ASP.NET Core | 8.0 | Web API sunucusu |
| C# | 12 | Backend dili |
| Anthropic C# SDK | 12.20.1 | Claude AI entegrasyonu |
| Claude Opus | 4.7 | Dil modeli |

### AI & Araçlar
| Özellik | Açıklama |
|---------|----------|
| `web_search_20250305` | Anthropic'in sunucu taraflı web arama aracı |
| Prompt Caching | İş Kanunu metni için maliyet optimizasyonu |
| Agentic Loop | Çoklu araç çağrısı desteği (max 5 tur) |

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
| GitHub | Kaynak kod yönetimi |
| Docker | Container ile deploy |

## Canlı Demo

[https://hakkimvar.onrender.com](https://hakkimvar.onrender.com)

## Kurulum

```bash
git clone https://github.com/sevilaydin/hakkimvar-.git
cd hakkimvar-
```

`appsettings.Production.json` dosyası oluşturun:

```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-..."
  }
}
```

```bash
dotnet run
```

## Lisans

Bu proje eğitim amaçlı geliştirilmiştir. Verilen bilgiler hukuki tavsiye niteliği taşımaz.
