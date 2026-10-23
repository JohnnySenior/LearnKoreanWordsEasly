# Hosting

**Holat (2026-10-23):** hali deploy qilinmagan. Kod deployga tayyor (8-bosqich); Railway’da Lug‘at uchun **alohida loyiha** (KoreanAiTeacher’dan alohida servis va baza) Railway connector ulangan sessiyada ochiladi (spec §6.1, §13.9).

## Deploy qadamlari (Railway)

1. Yangi loyiha "Lugat" → **PostgreSQL** servisi (18).
2. **app** servisi: GitHub `JohnnySenior/LearnKoreanWordsEasly`, branch `main`, Dockerfile bo‘yicha build. Settings → Source → "Wait for CI" yoqiladi.
3. **Volume** `/data` ga ulanadi. Image root bo‘lmagan foydalanuvchi bilan ishlaydi, Railway volume’i esa root’niki: `RAILWAY_RUN_UID=0` o‘zgaruvchisi qo‘yiladi, aks holda ovoz keshi, sessiya kalitlari va zaxira yozilmaydi.
4. Healthcheck path `/health`; Serverless (uxlash) o‘chiq.
5. **Birinchi deploydan oldin shart bo‘lgan o‘zgaruvchilar:**
   - `ConnectionStrings__Default=Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}};Gss Encryption Mode=Disable`
   - `Storage__Root=/data/files`, `DataProtection__KeysPath=/data/keys`, `Backup__Directory=/data/backups`, `ASPNETCORE_ENVIRONMENT=Production`.
6. Public domain yaratiladi (`*.up.railway.app`). `Telegram__AppUrl` bo‘sh bo‘lsa `https://$RAILWAY_PUBLIC_DOMAIN` olinadi.
7. **Kirish uchun kamida bittasi** (keyin qo‘shsa ham bo‘ladi, o‘zgaruvchi o‘zgarsa servis qayta deploy bo‘ladi): Telegram (`Telegram__BotToken`, `Telegram__BotUsername`, BotFather `/setdomain`) yoki Google (`Google__ClientId`, origin’ga domen). `Auth__Admins__TelegramUserIds__0` — Johnny admin bo‘lishi uchun.
8. Keyin: AI kalitlari, Azure ovoz, `Ai__Budget__*` tariflari, `Payments__*`, `Backup__TelegramChatId`. Ularsiz ilova ishlaydi: tegishli funksiya "ulanmagan" yoki "Tez orada" deydi.
9. Workspace → Usage → hard spend limit (masalan $10/oy).
10. Tekshiruv: `/health` → `ready`; kirish; admin; `/admin/korsatkichlar`; Sozlamalar → "Sinov xabari"; ertasi kuni zaxira yozilganini Sozlamalar → "Zaxira nusxa" da ko‘rish.

Proksi: Railway’da `RAILWAY_ENVIRONMENT_NAME` bor bo‘lsa `X-Forwarded-For` ning oxirgi (proksi qo‘shgan) qiymati mijoz IP’si hisoblanadi (L-33); boshqa joyda `ForwardedHeaders__Enabled=true` bilan yoqiladi.

Quyidagilar KoreanAiTeacher’ning ishlab turgan sozlamasiga asoslangan.

## Rejadagi tuzilma

- Servislar: `app` (GitHub `JohnnySenior/LearnKoreanWordsEasly`, branch `main`, Dockerfile) va `Postgres` (Railway PostgreSQL 18).
- `app` volume’i `/data` ga ulanadi: `/data/files` (ovoz keshi), `/data/keys` (sessiya kalitlari), `/data/backups` (zaxira). Healthcheck `/health`. Uxlash rejimi o‘chiq: eslatma va zaxira worker’lari doim ishlashi kerak.
- O‘zgaruvchilar: [.env.example](../.env.example) dagi nomlar. Maxfiy qiymatlarni faqat Johnny kiritadi.
- **CI/CD (KAT D-19):** faqat `main` deploy qilinadi. Railway → `app` → Settings → Source → "Wait for CI" yoqiladi.
- Workspace’da hard spend limit qo‘yiladi (spec §13.9).

Narx taxmini (KAT tajribasi, Hobby tarifi): ilova + PostgreSQL oyiga taxminan $5–7. Ochiq ro‘yxatdan o‘tish bilan yuklama oshadi; deploydan keyin Railway metrikalari bilan qayta hisoblanadi (spec BUD02).

## Zaxira nusxa

Har kecha 04:00 KST `/data/backups/lugat-YYYYMMDD-HHMMSS.dump` va `.dump.zip` yoziladi (KAT D-22, D-50). Dump vaqtinchalik bazaga tiklanadi; bir xil snapshot bo‘yicha migratsiya va barcha jadval countlari solishtiriladi. ZIP ichida `database.dump` va `manifest.json` (countlar) bor (L-08). 7 kunlik va 4 haftalik nusxa saqlanadi.

Haftalik tekshirilgan nusxa bot orqali `Backup__TelegramChatId` ga yuboriladi (KAT D-37, D-51). Bot chatida `/start` bosilgan bo‘lishi kerak. Bot API chegarasi 50 MiB: paket sig‘masa faqat `.dump` yuboriladi. Nusxa barcha foydalanuvchilarning shaxsiy ma’lumotini o‘z ichiga oladi; kirishni egasi bilan cheklang.

### Tiklash

1. Ilovani to‘xtating; yangi PostgreSQL baza tayyorlang. Avval alohida muhitda tiklab tekshiring.
2. ZIP’ni xavfsiz katalogga oching.
3. PostgreSQL 18 klienti bilan: `pg_restore --no-owner --no-privileges --exit-on-error -h <PGHOST> -p <PGPORT> -U <PGUSER> -d <YANGI_BAZA> database.dump`. Parolni `PGPASSWORD` orqali bering, terminalga chiqarmang.
4. Har bir jadval countini `manifest.json` bilan solishtiring; ilovani yangi bazaga ulang (`ConnectionStrings__Default`, Npgsql formatida).
5. AI, ovoz va Telegram maxfiy sozlamalarini server konfiguratsiyasidan tiklang; ular paketda yo‘q. Data Protection kalitlari ham yo‘q: hamma qaytadan kiradi. Ovoz keshi qayta hosil bo‘ladi.

Real nusxadan yangi Railway muhitiga tiklash mashqi hali bajarilmagan.

## AI va ovoz kalitlari

- `Ai__Anthropic__ApiKey` va/yoki `Ai__OpenAi__ApiKey` (KAT D-21). Ilovada Sozlamalar → "AI xizmati" → "Ulanishni tekshirish".
- Azure Speech: `Ai__Tts__Azure__Key`, `Ai__Tts__Azure__Region` (KAT D-31, D-32).
- Oylik byudjet va tariflar: `Ai__Budget__*` (KAT D-50). `MonthlyLimitUsd` (standart $25) — qattiq chegara: oshsa AI va ovoz chaqiruvlari to‘xtaydi, tayyor kontent ishlayveradi. Tarif kiritilmagan model sarfi "noma’lum" deb ko‘rsatiladi va dollar chegarasiga qo‘shilmaydi; bunday chaqiruvlar oyiga `Ai__Budget__MonthlyUnpricedCalls` (standart 2000) ta bilan to‘xtatiladi (L-33). Aniq hisob uchun tariflarni kiriting. Ovoz (Azure, OpenAI TTS) ham shu hisobga kiradi: butun katalog ovozini oldindan yaratishdan oldin Azure uchun tarif yozing (Free F0 bo‘lsa narx `0`), aks holda 2000 ta chaqiruvdan keyin to‘xtaydi.

## Kirish (Telegram va Google)

Ikkalasi ham ixtiyoriy: sozlanmagani kirish sahifasida ko‘rinmaydi (`GET /api/auth/config`). Kalit qiymatlarini faqat Johnny kiritadi.

**Telegram Login Widget** (L-13):
1. BotFather → bot → `/setdomain` → ilova domeni (masalan `lugat.up.railway.app`). Domen mos kelmasa widget "Bot domain invalid" deydi.
2. `Telegram__BotToken` (maxfiy) va `Telegram__BotUsername` (`@` siz). Ikkalasi bo‘lmasa Telegram tugmasi chiqmaydi.
3. Widget "xabar yuborishga ruxsat" so‘raydi — eslatmalar shu chatga keladi.

**Google** (L-13):
1. Google Cloud Console → APIs & Services → Credentials → OAuth client ID → "Web application".
2. "Authorized JavaScript origins" ga ilova domeni (`https://...`) va lokal sinov uchun `http://localhost:5248`. Redirect URI kerak emas (GIS ID token).
3. OAuth consent screen: ilova nomi "Lug‘at", scope’lar faqat `openid`, `email`, `profile`.
4. `Google__ClientId` — maxfiy emas (brauzerga ham beriladi). Client secret kerak emas va saqlanmaydi.

**Birinchi admin** (L-14): `Auth__Admins__TelegramUserIds__0=<Telegram ID>` yoki `Auth__Admins__GoogleSubjects__0=<Google sub>`. Shu hisob bilan kirilganda `Admin` roli beriladi.

**Proksi:** Railway ortida haqiqiy IP `X-Forwarded-For` da; `ForwardedHeaders` Railway’da avtomatik yoqiladi (L-33), rate limit mijoz IP’si bo‘yicha.

