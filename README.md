# Koreyscha — o‘zbeklar uchun koreys tili lug‘ati (PWA)

Koreyadagi va Koreyaga ketayotgan o‘zbeklar uchun koreys tili so‘zlarini **o‘zbek tilida**, eng ko‘p ishlatiladigan so‘zlardan boshlab, **25 talik bo‘limlarda ketma-ket** va haqiqatan yodlatib o‘rgatadigan PWA. Ilova nomi: "Koreyscha" (L-36); kodda `Lugat` prefiksi qoladi.

> **Holat (2026-10-26):** spec §13 dagi 0–8-bosqichlar kodda bor va production’da ishlayapti: https://koreyscha.com (Railway, Cloudflare orqali). Qolgan ishlar — haqiqiy qurilmalardagi qabul tekshiruvlari va egasining dashboard sozlamalari (pastda). Batafsil: [docs/CHECKLIST.md](docs/CHECKLIST.md).

## Nima bor

| Qism | Kodda | Avtomatik test | Real muhitda tasdiqlangan |
|---|---|---|---|
| Kirish: Telegram Login Widget, Google, hisoblarni bog‘lash, hamma qurilmadan chiqish | ✓ | ✓ | ✓ Windows Chrome, iPhone Safari (2026-10-23) |
| Katalog: 국립국어원 ro‘yxati importi, AI qoralama, tekshiruv, nashr (`/kontent`) | ✓ | ✓ | qisman: kontent bosqichma-bosqich yuklanmoqda |
| Bo‘limlar (25 so‘z), kartalar, takrorlash, qadamlar (Jadval → Karta → Mashq → Test) | ✓ | ✓ | ochiq: to‘liq oqim real telefonda |
| Bo‘lim testi (yozma va eshitish savollari bilan), "Bilaman" testi va kunlik chegarasi | ✓ | ✓ | ochiq |
| Mashqlar: yozma, "Qaysi so‘z to‘g‘ri?", gap zinapoyasi, tinglash, talaffuz, xatolar daftari | ✓ | ✓ | ochiq: iPhone audio va koreyscha klaviatura |
| Premium, qo‘lda to‘lov, promo-kodlar | ✓ | ✓ | ochiq |
| PWA: o‘rnatish, kesh, yangilanish xabari | ✓ | ✓ (Chromium) | qisman: iPhone’da ikonka yangilandi; arzon Android/sekin internet o‘lchovi ochiq |
| O‘qituvchi paneli, guruhlar, komissiya; admin panel, ko‘rsatkichlar, reklama kanallari | ✓ | ✓ | ochiq: ikki haqiqiy foydalanuvchi bilan |
| Telegram eslatmalari (vaqt zonasi: Seul yoki Toshkent) | ✓ | ✓ | ochiq: haqiqiy 20:00 xabari |
| Kechki zaxira + tiklab tekshirish, haftalik Telegram nusxasi | ✓ | ✓ | ✓ kechki nusxalar `verified` (Railway loglari); to‘liq tiklash mashqi ochiq |
| AI va xarajat chegarasi | ✓ | ✓ | ochiq: real tariflar kiritilmagan |

**Ochiq qabul tekshiruvlari** (dalilsiz bajarildi deb belgilanmaydi):
- Real iPhone va Android’da: kirish → bo‘lim → test → keyingi bo‘lim → ertasi kuni takrorlash; iPhone audio va koreyscha klaviatura; arzon Android/sekin internetda birinchi yuklanish.
- Haqiqiy Telegram eslatmasi (belgilangan vaqtda).
- Production nusxasidan yangi muhitga to‘liq tiklash mashqi.
- Railway: hard spend limit (dashboard). "Wait for CI" yoqilgan (2026-10-26, servis sozlamasida tekshirildi).
- AI tariflari (`Ai__Budget__Prices__*`) kiritilmagan: hozir AI chaqiruvlari dollar bilan emas, faqat oylik son bilan cheklanadi.
- Telegram’dagi bir oydan eski zaxira nusxalarini egasi qo‘lda o‘chiradi (bot o‘chira olmaydi; Johnny A variantini tanladi, L-46) — doimiy jarayon.

## Asosiy hujjat

To‘liq mahsulot va texnik talablar: **[docs/SPECIFICATION.md](docs/SPECIFICATION.md)** (v1.0, 2026-10-22). Spetsifikatsiya yagona manba hisoblanadi.

Agentlar va dasturchilar uchun qoidalar: [AGENTS.md](AGENTS.md). Texnik qarorlar: [docs/DECISIONS.md](docs/DECISIONS.md). Hosting: [docs/HOSTING.md](docs/HOSTING.md). Sozlamalar namunasi: [.env.example](.env.example).

## Ishga tushirish

.NET 10 SDK va PostgreSQL kerak (sozlash: [AGENTS.md](AGENTS.md)).

```bash
dotnet build
dotnet test                       # PostgreSQL kerak
node --test tests/web/*.test.mjs  # JS testlari
dotnet ef migrations has-pending-model-changes --project src/Lugat.Infrastructure   # CI ham tekshiradi
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

0–8-bosqichlarning hammasi kodda bor (Repo → Kirish → Katalog → Bo‘limlar → Test va "Bilaman" → Premium → PWA → O‘qituvchilar va admin → Eslatmalar va ishga tushirish). Har birining tekshiruvi va ochiq qolgan bandlari: [docs/CHECKLIST.md](docs/CHECKLIST.md).

Kontent manbai: 국립국어원 「한국어 학습용 어휘 목록」 (KOGL 1-tur). Ilovada manba albatta ko‘rsatiladi.
