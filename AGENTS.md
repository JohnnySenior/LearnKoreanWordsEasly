# AGENTS.md — dasturchi va AI agentlar uchun

Ishni boshlashdan oldin o‘qing:

1. [docs/SPECIFICATION.md](docs/SPECIFICATION.md). Mahsulot talablarining yagona manbasi.
2. [docs/DECISIONS.md](docs/DECISIONS.md). Qabul qilingan texnik qarorlar va sabablari.
3. [docs/CHECKLIST.md](docs/CHECKLIST.md). Nima tayyor, nima tekshirilgan va nima qolgan.

## Qoidalar (spec §17 dan)

- **KELISHILGAN** talabni o‘zicha o‘zgartirmang. O‘zgartirish kerak bo‘lsa, sabab va ta’sirini yozing va Johnny bilan kelishing (AG04).
- **TEXNIK ASOS** o‘zgarsa, `docs/DECISIONS.md` ga yangi yozuv qo‘shing.
- C# asosini saqlang (AG01). Mock ma’lumotni real integratsiya deb ko‘rsatmang.
- Har bosqichda nima o‘zgardi, nima tekshirildi va nima ishlamayotganini `docs/CHECKLIST.md` ga yozing (AG02).
- Maxfiy qiymatlar (bot tokeni, AI kalitlari) repo, log va brauzerga tushmaydi. Nomlari `.env.example` da (AG03, SEC05).
- Foydalanuvchi interfeysi o‘zbekcha (lotin). Koreyscha matn Hangul bilan.

## Tuzilma

```
src/
  Lugat.Domain          domen modeli va qoidalari (takrorlash, import yozuvlari), tashqi bog‘liqliksiz
  Lugat.Contracts       API va klient uchun umumiy DTO'lar
  Lugat.Application     xizmatlar (import, foydalanuvchi), parser, interfeyslar
  Lugat.Infrastructure  EF Core/PostgreSQL, fayl ombori, PdfPig
  Lugat.Api             ASP.NET Core: endpoint'lar, auth, fon worker, Blazor klientni tarqatadi
  Lugat.Web             Blazor WebAssembly klient + kichik JS interop (Telegram, audio)
tests/
  Lugat.Domain.Tests
  Lugat.Application.Tests  parser (sintetik sahifalar + ixtiyoriy real kitob)
  Lugat.Api.Tests          WebApplicationFactory + real PostgreSQL
```

## Buyruqlar

```bash
dotnet build                     # butun solution
dotnet test                      # barcha testlar (PostgreSQL kerak, pastga qarang)
dotnet run --project src/Lugat.Api   # http://localhost:5248, Development
dotnet tool restore && dotnet ef migrations add <Nomi> --project src/Lugat.Infrastructure --output-dir Persistence/Migrations
```

PostgreSQL: Development va testlar standart holatda `lugat`/`lugat` foydalanuvchisi bilan `localhost` ga ulanadi (`CREATEDB` huquqi kerak). Boshqa server uchun `LUGAT_TEST_POSTGRES` ni o‘rnating. Real lug‘at kitobi testi uchun `LUGAT_DICT_PDF=/yo‘l/kitob.pdf`.

Development muhitida Telegram tashqarisida brauzer orqali `POST /api/auth/dev` bilan kirish mumkin (`appsettings.Development.json`). Bu endpoint boshqa muhitlarda umuman ro‘yxatdan o‘tmaydi va buni test tekshiradi.

## Kod uslubi

- `Nullable` yoqilgan, ogohlantirishlar xato sifatida (`Directory.Build.props`).
- Paket versiyalari faqat `Directory.Packages.props` da.
- Vaqt `TimeProvider` orqali olinadi. Testlarda `FakeTimeProvider` ishlatiladi (AG02: fake clock bilan scheduler testlari).
- API xatolari `ProblemDetails` ko‘rinishida, `detail` maydoni o‘zbekcha (API03).
