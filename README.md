# Lug‘at — o‘zbeklar uchun koreys tili lug‘ati (PWA)

Koreyadagi va Koreyaga ketayotgan o‘zbeklar uchun koreys tili so‘zlarini **o‘zbek tilida**, eng ko‘p ishlatiladigan so‘zlardan boshlab, **25 talik bo‘limlarda ketma-ket** va haqiqatan yodlatib o‘rgatadigan PWA. Ishchi nom: "Lug‘at" (yakuniy brend keyinroq tanlanadi).

> **Holat (2026-10-22):** Bosqich 0 — repo va asos. Kod [KoreanAiTeacher](https://github.com/JohnnySenior/KoreanAiTeacher) dan ko‘chirildi, `Lugat.*` ga qayta nomlandi va v1 ga kirmaydigan modullar olib tashlandi. Kirish, katalog, bo‘limlar va PWA keyingi bosqichlarda. Batafsil: [docs/CHECKLIST.md](docs/CHECKLIST.md).

## Asosiy hujjat

To‘liq mahsulot va texnik talablar: **[docs/SPECIFICATION.md](docs/SPECIFICATION.md)** (v1.0, 2026-10-22). Spetsifikatsiya yagona manba hisoblanadi.

Agentlar va dasturchilar uchun qoidalar: [AGENTS.md](AGENTS.md). Texnik qarorlar: [docs/DECISIONS.md](docs/DECISIONS.md). Hosting: [docs/HOSTING.md](docs/HOSTING.md). Sozlamalar namunasi: [.env.example](.env.example).

## Ishga tushirish

.NET 10 SDK va PostgreSQL kerak (sozlash: [AGENTS.md](AGENTS.md)).

```bash
dotnet test
dotnet run --project src/Lugat.Api   # http://localhost:5248
```

## Texnik asos

| Qism | Tanlov |
|---|---|
| Backend | C#, .NET 10 LTS, ASP.NET Core minimal API (modular monolith) |
| Frontend | Blazor WebAssembly + kichik JS interop (audio, mikrofon) |
| Ma’lumot | PostgreSQL + EF Core |
| Fon ishlari | ASP.NET Core worker |
| AI/ovoz | Claude/OpenAI matn adapterlari, Azure Speech (TTS va talaffuz baholash) |
| Testlar | xUnit v3, real PostgreSQL, `FakeTimeProvider` |
| Hosting | Railway, alohida loyiha, Dockerfile |

## Bajarish bosqichlari (spec §13)

0. Repo va asos ← **hozir**
1. Kirish va foydalanuvchilar (Telegram Login Widget, Google)
2. Katalog va kontent jarayoni (국립국어원 ro‘yxati, AI qoralama, tekshiruv, nashr)
3. Bo‘limlar, kartalar, takrorlash
4. Bo‘lim testi va "Bilaman"
5. Premium va to‘lov
6. PWA
7. O‘qituvchilar va admin
8. Eslatmalar, ishga tushirish

Kontent manbai: 국립국어원 「한국어 학습용 어휘 목록」 (KOGL 1-tur). Ilovada manba albatta ko‘rsatiladi.
