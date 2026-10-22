# AGENTS.md — dasturchi va AI agentlar uchun

Ishni boshlashdan oldin o‘qing:

1. [docs/SPECIFICATION.md](docs/SPECIFICATION.md). Mahsulot talablarining yagona manbasi. Ayniqsa §0 "Agent uchun eng muhim 10 qoida".
2. [docs/DECISIONS.md](docs/DECISIONS.md). Qabul qilingan texnik qarorlar (KoreanAiTeacher’dan olinganlari va `L-xx`).
3. [docs/CHECKLIST.md](docs/CHECKLIST.md). Nima tayyor, nima tekshirilgan va nima qolgan.

Asos: [KoreanAiTeacher](https://github.com/JohnnySenior/KoreanAiTeacher) (faqat o‘qish uchun; unga hech narsa yozilmaydi). Shubha bo‘lsa, o‘sha repo qanday qilgan bo‘lsa, shunday qiling.

## Qoidalar

- **KELISHILGAN** talabni o‘zicha o‘zgartirmang. O‘zgartirish kerak bo‘lsa, sabab va ta’sirini yozing va Johnny bilan kelishing.
- **TEXNIK ASOS** o‘zgarsa, `docs/DECISIONS.md` ga yangi `L-xx` yozuv qo‘shing.
- Maqsaddan og‘ishmang: v1 — lug‘at yodlash (spec §3). Grammatika, PDF import, TOPIK insho, kitob testlari, dars kalendari v1 da **yo‘q**.
- Barcha o‘quv mantiqi serverda (`Application` xizmatlari). Blazor sahifasida biznes qoida bo‘lmaydi (spec §14).
- C# va .NET 10 (KELISHILGAN). Mock ma’lumotni real integratsiya deb ko‘rsatmang.
- Har bosqichda nima o‘zgardi, nima tekshirildi va nima ishlamayotganini `docs/CHECKLIST.md` ga yozing.
- Maxfiy qiymatlar (bot tokeni, AI/Azure kalitlari, Google client secret) repo, log va brauzerga tushmaydi. Nomlari `.env.example` da.
- Foydalanuvchi interfeysi o‘zbekcha (lotin). Koreyscha matn Hangul bilan.
- Har bosqich (spec §13) alohida branch/PR; `dotnet build`, `dotnet test` va CI yashil bo‘lgach `main` ga.

## Tuzilma

```
src/
  Lugat.Domain          domen modeli va qoidalari (takrorlash, sessiya rejasi, sinov baholash), tashqi bog‘liqliksiz
  Lugat.Contracts       API va klient uchun umumiy DTO'lar
  Lugat.Application     xizmatlar (sessiya, so‘zlar, sinov, AI, ovoz, eslatma), interfeyslar
  Lugat.Infrastructure  EF Core/PostgreSQL, fayl ombori, AI/Azure/Telegram adapterlari, zaxira nusxa
  Lugat.Api             ASP.NET Core: endpoint'lar, auth, fon worker, Blazor klientni tarqatadi
  Lugat.Web             Blazor WebAssembly klient + kichik JS interop (audio, mikrofon, localStorage)
tests/
  Lugat.Domain.Tests
  Lugat.Application.Tests
  Lugat.Api.Tests          WebApplicationFactory + real PostgreSQL
  web/                     JS (audio.js) testlari, node --test
content/samples/           namuna kontent (lugat_A_namuna_50.json — A darajaning 1–2-bo‘limi)
```

## Buyruqlar

```bash
dotnet build                     # butun solution
dotnet test                      # barcha testlar (PostgreSQL kerak, pastga qarang)
node --test tests/web/*.test.mjs # JS testlari
dotnet run --project src/Lugat.Api   # http://localhost:5248, Development
dotnet tool restore && dotnet ef migrations add <Nomi> --project src/Lugat.Infrastructure --output-dir Persistence/Migrations
```

PostgreSQL: Development va testlar standart holatda `lugat`/`lugat` foydalanuvchisi bilan `localhost` ga ulanadi (`CREATEDB` huquqi kerak). Boshqa server uchun `LUGAT_TEST_POSTGRES` ni o‘rnating.

Kirish: Telegram Login Widget (`POST /api/auth/telegram-login`) va Google (`POST /api/auth/google`), sozlamalari `.env.example` da. Development muhitida qo‘shimcha `POST /api/auth/dev` bor (`Auth:DevLoginUserId`); u boshqa muhitlarda umuman ro‘yxatdan o‘tmaydi va buni test tekshiradi. Lokal ishlab chiqishda kirish sahifasidagi "Dev sifatida kirish" tugmasi shu. API testlari `ApiFactory` ichidagi test endpoint’lari (`/__test/sign-in`, `/__test/words`) orqali kiradi va so‘z qo‘shadi; Telegram va Google tekshiruvining o‘zi `Auth/LoginValidatorTests` va `Auth/AuthEndpointsTests` da sinaladi.

## Kod uslubi

- `Nullable` yoqilgan, ogohlantirishlar xato sifatida (`Directory.Build.props`).
- Paket versiyalari faqat `Directory.Packages.props` da.
- Vaqt `TimeProvider` orqali olinadi. Testlarda `FakeTimeProvider` ishlatiladi.
- API xatolari `ProblemDetails` ko‘rinishida, `detail` maydoni o‘zbekcha.
- Mutatsiyalar idempotent (`clientEventId`/klient tanlagan `Id`), optimistic concurrency (xmin, 409).
