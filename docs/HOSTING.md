# Hosting

**Holat (2026-10-22):** hali deploy qilinmagan. Railway’da Lug‘at uchun **alohida loyiha** (KoreanAiTeacher’dan alohida servis va baza) 8-bosqichda ochiladi (spec §6.1, §13.9). Quyidagilar KoreanAiTeacher’ning ishlab turgan sozlamasiga asoslangan reja.

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
- Oylik byudjet va tariflar: `Ai__Budget__*` (KAT D-50). Tarif kiritilmasa, sarf "noma’lum" deb ko‘rsatiladi va limit to‘liq ishlamaydi.
