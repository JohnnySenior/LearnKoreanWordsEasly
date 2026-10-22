# Texnik qarorlar jurnali

Har bir yozuv: sana, qaror, sabab, spec havolasi. Yangi yozuv oxiriga qo‘shiladi. Eski yozuv o‘chirilmaydi, faqat "o‘rniga: L-xx" deb belgilanadi.

Bu repo [KoreanAiTeacher](https://github.com/JohnnySenior/KoreanAiTeacher) (keyinda **KAT**) asosida qurilgan. Uning qarorlari `D-xx` raqami bilan KAT’ning [docs/DECISIONS.md](https://github.com/JohnnySenior/KoreanAiTeacher/blob/main/docs/DECISIONS.md) faylida to‘liq yozilgan. Bu yerda faqat qisqa havola qoldiriladi. Lug‘at’ning o‘z qarorlari `L-01` dan boshlanadi.

## KAT’dan olingan qarorlar

Quyidagilar kodda saqlangan va amal qiladi. Matni KAT’da; bu yerda faqat Lug‘at uchun farqi yoziladi.

| KAT | Mavzu | Lug‘at’dagi holati |
|---|---|---|
| D-01 | Bitta deploy: Api Blazor klientni ham tarqatadi | KAT D-01 dan olingan |
| D-02 | index.html da fingerprint placeholder yo‘q | KAT D-02 dan olingan |
| D-03 | Sessiya: bearer access 1 soat + refresh | KAT D-03 dan olingan. Telegram initData o‘rniga Login Widget va Google; refresh 30 kun; sessiya versiyasi (L-13, L-15) |
| D-05 | Takrorlash algoritmi `ladder-v1` | KAT D-05 dan olingan, o‘zgarishsiz |
| D-06 | Paket lock fayllari ishlatilmaydi | KAT D-06 dan olingan |
| D-07 | Qatlamlar: Contracts, Application, Infrastructure | KAT D-07 dan olingan |
| D-08 | Migratsiyalar ilova ishga tushganda | KAT D-08 dan olingan. Migratsiyalar yangidan: L-12 |
| D-11 | Testlar: xunit v3, real PostgreSQL | KAT D-11 dan olingan (`LUGAT_TEST_POSTGRES`) |
| D-13 | O‘rganish birligi va ikki yo‘nalishli kartochkalar | KAT D-13 dan olingan. Manba qatori o‘rniga oraliq so‘z modeli: L-04 |
| D-14 | Sessiya rejasi, idempotent javob, undo | KAT D-14 dan olingan. Dars ustuvorligi o‘rniga "fokus": L-05 |
| D-16 | Telegram eslatmalari, outbox | KAT D-16 dan olingan. Shanba dars sloti yo‘q (L-06), tugma oddiy havola (L-07) |
| D-17 | Tashqi tekshiruvdan keyingi tuzatishlar (undo, oflayn javob REL03) | KAT D-17 dan olingan. Import tahriri bandi olib tashlandi |
| D-19 | CI/CD: deploy faqat `main` dan | KAT D-19 dan olingan. Railway loyihasi hali yo‘q (8-bosqich) |
| D-21 | AI: Claude va ChatGPT, almashtirsa bo‘ladigan | KAT D-21 dan olingan |
| D-22 | Zaxira nusxa: har kecha, tiklab tekshiriladi | KAT D-22 dan olingan. Paketda PDF yo‘q: L-08 |
| D-23 | Yozma mashq kartochkadan alohida | KAT D-23 dan olingan. Dars o‘rniga jadval bo‘yicha tanlash |
| D-25 | "Modernist" dizayn | KAT D-25 dan olingan. Telegram mavzusi o‘rniga qurilma sozlamasi: L-11 |
| D-29 | Talaffuz: OpenAI ovozi, serverda kesh | KAT D-29 dan olingan (Azure’dan keyin zaxira) |
| D-30 | Sinov tizimi (TEST01–TEST04) | KAT D-30 dan olingan. Faqat so‘z savollari: L-10 |
| D-31 | Azure Speech TTS | KAT D-31 dan olingan |
| D-32 | Talaffuz mashqi va Azure baholash | KAT D-32 dan olingan |
| D-36 | Jadvalni yodlash, ustunni yopish | KAT D-36 dan olingan. Kitob o‘rniga jadval raqami bo‘yicha |
| D-37 | Railway’dan tashqari zaxira: haftalik Telegram fayli | KAT D-37 dan olingan. Qabul qiluvchi: L-08 |
| D-38 | Og‘zaki tayyorlik sinovi (TEST02) | KAT D-38 dan olingan. Darsga emas, tanlangan jadvallarga |
| D-39, D-40 | Sinov menyusi, tarix va grafiklar | KAT D-39/D-40 dan olingan. Grammatika, aralash va kitob bandlari olib tashlandi |
| D-41 | Xatolar daftari | KAT D-41 dan olingan. Grammatika bo‘limi olib tashlandi |
| D-42 | Bugun sahifasi faqat hozirgi ish | KAT D-42 dan olingan. Imtihon, dars va grammatika kartalari olib tashlandi |
| D-44 | Eslatma vaqti ro‘yxatdan tanlanadi | KAT D-44 dan olingan (vaqt tanlash qismi). Darslarni qayta taqsimlash olib tashlandi |
| D-45 | Teskari savol kechroq va aralash | KAT D-45 dan olingan |
| D-46 | So‘z qo‘llanishi, o‘zbekcha tuzoqlar, "Qaysi so‘z to‘g‘ri?" | KAT D-46 dan olingan. Hozircha har foydalanuvchiga; katalog darajasiga 2-bosqichda o‘tadi (spec §5.1 `CatalogUsage`) |
| D-47 | Gap zinapoyasi va Tuzoqlar daftari | KAT D-47 dan olingan. Grammatika bandlari promptdan olib tashlandi |
| D-48 | Testlarda soxta soat qat’iy sanadan | KAT D-48 dan olingan |
| D-50 | Ishonchlilik va sarf nazorati | KAT D-50 dan olingan |
| D-51 | Telegram’ga zaxira: katta paket bo‘lsa faqat baza | KAT D-51 dan olingan |
| D-52 | Gap tekshiruvi: hamma xato birdan, izchil baho | KAT D-52 dan olingan. Grammatika maqsadi olib tashlandi |
| D-53 | Tinglash | KAT D-53 dan olingan, lekin v1 da yashirin: L-09 |
| D-54 | Zaif joylar | KAT D-54 dan olingan. Grammatika bandlari olib tashlandi |

**Olib tashlangan va ko‘chirilmagan** (spec §3.1 "Yo‘q"): D-04 (Mini App initData), D-09, D-10 (lug‘at PDF parseri), D-12, D-20, D-24, D-26, D-27, D-28 (grammatika), D-15, D-18, D-43 (dars topshiriqlari va reja), D-33, D-35 (kitob testlari), D-34 (imtihon sanasi), D-49 (TOPIK yozish).

## L-01 · 2026-10-22 · Bosqich 0: KAT’dan nusxa, `Lugat.*` nomlari

KAT `main` (commit `679c5b855853e3823791d2cc96db88a027c4fc65`) git tarixisiz ko‘chirildi (spec §13.1). Loyihalar va namespace’lar `KoreanAiTeacher.*` → `Lugat.*`. `KAT_*` muhit o‘zgaruvchilari `LUGAT_*`, baza foydalanuvchisi `kat` → `lugat`. Ishchi nom "Lug‘at" (spec boshi, Q1); yakuniy brend Johnny tanlaydi.
KAT’ning `docs/` papkasi ko‘chirilmadi: bu repoda o‘z spetsifikatsiyasi va qarorlar jurnali bor.

## L-02 · 2026-10-22 · v1 ga kirmaydigan modullar olib tashlandi

Spec §3.1 dagi "Yo‘q" qismlari kodi, endpoint’lari, sahifalari, testlari va jadvallari bilan olib tashlandi: PDF import va lug‘at parseri (PdfPig paketi ham), grammatika (kitob, bandlar, to‘plamlar, savol-javob), kitobdagi TOPIK testlari va imtihon rejimi, imtihon sanasi va sanog‘i, TOPIK yozish (insho), dars topshiriqlari va dars kalendari, Telegram Mini App kirishi va allow-list. To‘liq ro‘yxat: [CHECKLIST.md](CHECKLIST.md).

## L-03 · 2026-10-22 · Kirish: 1-bosqichgacha oraliq holat (o‘rniga: L-13, L-14)

Mini App initData kirishi (`POST /api/auth/telegram`) va `Telegram:AllowedUserIds` olib tashlandi: Lug‘at Mini App emas va ro‘yxatdan o‘tish ochiq (spec §3.1, §6.3).
- Telegram Login Widget va Google 1-bosqichda qo‘shiladi. Ungacha **production’da kirish yo‘q**; faqat Development’dagi `POST /api/auth/dev` ishlaydi. Klient 404 olsa "Kirish hozircha yopiq" deydi.
- `POST /api/auth/refresh` endi allow-list’ni tekshirmaydi, faqat token muddati va imzosini. O‘chirilgan hisob (`DeletedAt`) tekshiruvi 1-bosqichda qo‘shiladi.
- API testlari faqat `ApiFactory` ichida ro‘yxatdan o‘tadigan `/__test/sign-in` orqali kiradi. U production kodida yo‘q.
- **Ochiq xavf (1-bosqichda yopiladi):** zaxira (`/api/backups/*`) va AI sozlamalari (`/api/ai/provider`, `/api/ai/ping`) KAT’da ikki kishilik ilova uchun edi va har qanday kirgan foydalanuvchiga ochiq. Rollar (spec §5.1, §6.4) kelganda ular `Admin` policy ostiga o‘tadi.

## L-04 · 2026-10-22 · Oraliq so‘z modeli: `WordSense` manbasiz

Import olib tashlangani uchun `WordSense` endi `SourceEntry`/`SourceDocument` ga bog‘lanmaydi: `OwnerId`, `TableNumber`, `Order` (ro‘yxatdagi o‘rni), matn, `IsActive`, `ContentVersion`. `(OwnerId, TableNumber, Order)` unikal.
- So‘zlar `VocabularyService` orqali qo‘shiladi (`AddAsync` — idempotent, har so‘zga ikki karta; `SetActiveAsync`; `UpdateTextAsync` — matn o‘zgarsa `ContentVersion++`, statistika saqlanadi).
- API’da "kitob + jadval" o‘rniga faqat jadval raqami: `/api/vocabulary/tables/{table}`, `StartSessionRequest.TableNumber`, `/api/writing/queue?table=`.
- Production’da hozircha so‘z qo‘shadigan yo‘l yo‘q (katalog 2-bosqichda). Bu model 2–3-bosqichda `CatalogWord` + `LearningItem` bilan almashtiriladi (spec §5.2); "jadval" o‘rnini "bo‘lim" egallaydi.

## L-05 · 2026-10-22 · Sessiya rejasi: dars o‘rniga "fokus"

KAT’da sessiya yaqin darsning so‘zlarini birinchi qo‘yardi (D-14, D-18). Darslar yo‘q, shuning uchun `SessionPlanner` umumiy "fokus" tushunchasini oldi: `PlanCandidate.InFocus`, `PlanRequest.NewOnlyFromFocus` va `Drill`, `SessionItemKind.FocusReview`.
- Hozir fokus — mashq qilinayotgan jadval yoki xatolar daftaridagi so‘zlar (drill: hamma kartasi, muddati kelmaganlari ham, oldinroq javob zinapoyada surmaydi).
- Oddiy sessiyada yangi so‘zlar ro‘yxat tartibida, takrorlash barcha so‘zlardan.
- 3-bosqichda fokus = joriy ochiq bo‘lim: yangi so‘zlar faqat undan, takrorlash barcha ochilgan bo‘limlardan (spec §4.6).

## L-06 · 2026-10-22 · Eslatmalar: shanba dars sloti olib tashlandi

KAT D-16 da shanba 22:00 eslatmasi faqat shu kuni dars bo‘lsa yuborilardi. Darslar yo‘q, shuning uchun slot olib tashlandi: hozircha dushanba–juma 09:30 (foydalanuvchi o‘zgartiradi), dam olish kunlari yo‘q. Xabarda dars va imtihon qatorlari yo‘q.
Spec §8 (har kuni 20:00, kunlarni tanlash, "~M daqiqa") 8-bosqichda qilinadi.

## L-07 · 2026-10-22 · Eslatma tugmasi oddiy havola

Mini App bo‘lmagani uchun bot xabaridagi tugma `web_app` emas, `url` (`Telegram:AppUrl`, bo‘sh bo‘lsa `https://$RAILWAY_PUBLIC_DOMAIN`). U ilovani brauzerda yoki o‘rnatilgan PWA’da ochadi. Test tugmada `web_app` yo‘qligini tekshiradi.

## L-08 · 2026-10-22 · Zaxira: aniq qabul qiluvchi, PDF’siz paket

- Haftalik Telegram nusxasi (D-37) faqat `Backup:TelegramChatId` ga boradi. Allow-list yo‘q, shuning uchun "birinchi foydalanuvchi" zaxira yo‘li ham yo‘q: ochiq ro‘yxatdan o‘tishda bu boshqa odamga butun bazani yuborishi mumkin edi.
- `.dump.zip` paketi endi `database.dump` va `manifest.json` (jadval countlari) dan iborat; asl PDF fayllar yo‘q.

## L-09 · 2026-10-22 · Tinglash yashirin (`Features:Listening=false`)

Spec §3.1: tinglash v1.1 da. Kod saqlandi, lekin `Features:Listening` yoqilmaguncha `/api/listening/*` umuman ro‘yxatdan o‘tmaydi (404) va Mashq menyusida yo‘q. Testlar bayroqni yoqib ishlaydi; o‘chiq holat uchun alohida test bor.

## L-10 · 2026-10-22 · Sinov: faqat so‘z savollari

`TestQuestionKind` dan `GrammarChoice`, `GrammarMeaning`, `BookChoice` olib tashlandi; `TestCategory` = `Vocabulary` | `Oral`. Aralash sinov, kitob testlari, imtihon taymeri va javoblar varaqasi yo‘q. Og‘zaki tayyorlik (D-38) tanlangan jadvallar bilan saqlandi.
Bo‘lim yakuniy testi (spec §4.4, `WordListening` turi bilan) 4-bosqichda shu dvigatelda quriladi.

## L-11 · 2026-10-22 · Klient: Telegram SDK yo‘q

`telegram-web-app.js` yuklanmaydi (spec §6.2). `TelegramInterop` → `BrowserInterop`, `telegram.js` → `app.js` (qurilma ma’lumoti, tebranish, localStorage, nusxalash). Mavzu `prefers-color-scheme` bo‘yicha; `data-theme` ni Sozlamalardagi tanlov keyinroq qo‘yadi. Qurilma tekshiruvi sahifasi Telegram o‘rniga "o‘rnatilgan ilova" va mavzuni ko‘rsatadi. Sarlavha "Lug‘at".

## L-12 · 2026-10-22 · Yangi `Initial` migratsiya

KAT’ning 20 ta migratsiyasi ko‘chirilmadi (spec §5: yangi baza, eski ma’lumot ko‘chirilmaydi). Bitta `Initial` migratsiya: 15 jadval. CI `has-pending-model-changes` bilan tekshiradi.

## L-13 · 2026-10-22 · Kirish: Telegram Login Widget, Google, `UserLogin`, sessiya versiyasi

Spec §5.1, §6.3, §13.2.
- **Model.** `User` endi provayder ID’sini saqlamaydi: `DisplayName`, `Roles` (flags), `ShowRomanization`, `Theme`, `SessionVersion`, `DeletedAt`. Kirish usullari `user_logins` jadvalida: `(Provider, ProviderUserId)` unikal, bitta foydalanuvchida Telegram va Google bo‘lishi mumkin. Telegram ID — `ProviderUserId` (shaxsiy chat ID’si ham shu), Google — `sub` (email emas: u o‘zgarishi mumkin).
- **Telegram Login Widget** (`POST /api/auth/telegram-login`): widget qaytargan barcha maydonlar satr ko‘rinishida (`Dictionary<string,string>`), shuning uchun Telegram yangi maydon qo‘shsa ham hash tekshiruvi buzilmaydi. `secret = SHA256(bot_token)`, `hash = hex(HMAC_SHA256(secret, data_check_string))`, constant-time taqqoslash, `auth_date` ≤ 24 soat va 1 daqiqadan ortiq kelajakda emas. Widget `data-request-access="write"` bilan: bot eslatma yubora olishi uchun alohida /start shart emas (NOT02).
- **Google** (`POST /api/auth/google`): GIS ID token serverda `Microsoft.IdentityModel.JsonWebTokens` bilan tekshiriladi: RS256, Google kalitlari (JWKS, Cache-Control bo‘yicha keshlanadi, noma’lum `kid` bo‘lsa bir marta qayta olinadi), `aud` = `Google:ClientId`, `iss` = accounts.google.com, muddat ilova soati (`TimeProvider`) bo‘yicha. Email faqat `email_verified` bo‘lsa ko‘rsatiladi.
- **Bitta hisob — bitta foydalanuvchi.** Kirish va bog‘lash `login:{provider}:{id}` advisory lock ostida; unikal indeks ham bor. Parallel 5 ta kirish bitta foydalanuvchi yaratadi (test).
- **Hisob bog‘lash** (`POST /api/me/logins`): xuddi kirishdagidek isbot. Boshqa foydalanuvchining logini birlashtirilmaydi (409); shu provayderdan ikkinchi login ham 409.
- **Sessiya versiyasi.** Token faqat `uid` va `sv` (sessiya versiyasi) ni olib yuradi. `SessionGuard` middleware har so‘rovda foydalanuvchini bazadan oladi: o‘chirilgan bo‘lsa yoki `sv` mos kelmasa, token yo‘qdek (401). Chiqish (`POST /api/auth/logout`) va hisobni o‘chirish `SessionVersion++` qiladi — barcha qurilmadagi access va refresh tokenlar darhol to‘xtaydi. Narxi: har so‘rovda bitta PK so‘rovi.
- **Rollar** ham shu yerda bazadan qo‘shiladi, shuning uchun berilgan yoki olingan rol keyingi so‘rovdayoq amal qiladi.
- **Hisobni o‘chirish** (`DELETE /api/me`, spec §5.3): loginlar, so‘zlar, kartalar, javoblar, sessiyalar, sinovlar, mashqlar va eslatma yozuvlari bitta tranzaksiyada o‘chiriladi. `users` qatori anonim holda qoladi (`DisplayName` va rollar tozalanadi, `DeletedAt`), chunki `ai_usage` (keyinchalik to‘lovlar) unga bog‘langan. Xuddi shu Telegram bilan qayta kirilsa — yangi, bo‘sh foydalanuvchi.
- **Eslatmalar** faqat Telegram logini bor foydalanuvchilarga, chat ID loginidan olinadi. Faqat Google bilan kirganlarga Sozlamalarda Telegram’ni bog‘lash taklif qilinadi.

## L-14 · 2026-10-22 · Rollar va birinchi admin

`UserRoles` flags: `Learner`, `Teacher`, `Editor`, `Admin` (spec §5.1). Siyosatlar: `Admin` va `Editor` (admin ham kiradi). Hozir `Admin` ostida: `/api/backups/*`, `/api/ai/budget`, `/api/ai/ping/*` (L-03 dagi ochiq xavf yopildi). `/api/ai/status` va `/api/ai/provider` o‘quvchining o‘ziniki, ochiq qoladi.
Admin paneli (7-bosqich) bo‘lmaguncha birinchi admin konfiguratsiyadan: `Auth:Admins:TelegramUserIds` yoki `Auth:Admins:GoogleSubjects`. Ro‘yxatdagi login kirganda yoki bog‘langanda `Admin` rolini oladi; bu yerda rol hech qachon olib tashlanmaydi.

## L-15 · 2026-10-22 · Klientda refresh token qurilmada saqlanadi

Mini App’da har ochilishda initData bor edi, shuning uchun KAT tokenlarni faqat xotirada saqlardi (D-03). PWA’da bunday manba yo‘q: har ochilishda qayta kirish noqulay. Access token (1 soat) faqat xotirada; refresh token (30 kun, spec §6.3) `localStorage` da (`lugat.refresh`). Xavf: shu qurilmadagi XSS tokenni o‘qishi mumkin. Yumshatish: refresh token chiqishda va hisob o‘chirilganda serverda darhol bekor bo‘ladi (L-13), ilova tashqi skript sifatida faqat Telegram widget va Google GIS’ni yuklaydi. Kerak bo‘lsa keyinroq HttpOnly cookie’ga o‘tish mumkin (bir origin, D-01).

## L-16 · 2026-10-22 · Rate limit

ASP.NET Core RateLimiter (spec §6.4): kirish endpoint’lari (`telegram-login`, `google`, `refresh`, `dev`, `me/logins`) IP bo‘yicha daqiqasiga 10 (`Auth:SignInPerMinute`), sinov boshlash (`POST /api/tests`) foydalanuvchi bo‘yicha soatiga 20 (`Auth:TestStartsPerHour`). Rad etilganda 429 va o‘zbekcha ProblemDetails. AI chegaralari tarif bo‘yicha — 5-bosqich (spec §7.4). Bir nechta instansga o‘tilsa limiter umumiy omborga ko‘chishi kerak (hozir bitta instans, D-08).
Railway proksi ortida `RemoteIpAddress` proksi manzili bo‘lishi mumkin: deployda `ForwardedHeaders` sozlanishi kerak (8-bosqich, CHECKLIST’da).

## L-17 · 2026-10-22 · Katalog modeli va rasmiy ro‘yxat importi

Spec §2, §5.1, §13.3.
- **Modellar:** `CatalogWord` (rasmiy maydonlar + kontent + `Status`, `ContentVersion`, `ReviewedBy/At`, `AiProvider/Model`, `SourceKey`), `CatalogUsage` ("Qaysi so‘z to‘g‘ri?" savollari, so‘z bilan birga tekshiriladi), `Unit` (`Level`, `Number` unikal, `Status`, `WordCount`). Katalog umumiy: egasi yo‘q.
- **Kalit:** `SourceKey = "{daraja}:{so‘z}{omonim}:{품사}"` (spec §2.4: so‘z + omonim + turkum + daraja), unikal indeks. Qayta import faqat chastota o‘rni (`순위`) va izohni (`풀이`) yangilaydi; kontent, tekshiruv holati va bo‘lim o‘zgarmaydi. Faylda yo‘q so‘z o‘chirilmaydi.
- **Fayl:** ustunlar sarlavha bo‘yicha topiladi (`순위`, `단어`/`어휘`, `품사`, `풀이`, `등급`), shuning uchun tartib va qo‘shimcha ustunlar muhim emas; `가다01` dan omonim raqami ajratiladi. `.xls` (2003) va `.xlsx` — ExcelDataReader 3.9.0 (MIT), matnli versiya — tab yoki vergul bilan, UTF-8 yoki CP949. Yaroqsiz qatorlar sabab bilan o‘tkazib yuboriladi (javobda birinchi 20 tasi).
- **Haqiqiy fayl sinalmadi:** korean.go.kr bu ishlab chiqish muhitidan ochilmadi (proksi 403). Format ochiq manbalardagi tavsif va spec §2.1 bo‘yicha; sinovlar shu tuzilishdagi 5,965 qatorli fayl bilan. Johnny haqiqiy faylni yuklaganda natija (qatorlar, o‘tkazilganlar) tekshirilishi kerak — CHECKLIST’da ochiq punkt.
- **Kartaga mos emas:** `보`, `의`, `관` (spec §2.2). Bir nechta turkumli yozuv (`명/의`) — hech bo‘lmasa bittasi mos bo‘lsa, o‘rganiladi.
- **Turkum** bazada rasmiy kod bilan (`동`, `명`…); o‘zbekcha nomi (`fe’l`, `ot`…) DTO’da `PosUz`. Namuna fayldagi o‘zbekcha turkumlar kodga o‘giriladi (`fe'l` → `동`), shuning uchun keyingi rasmiy import o‘sha so‘zlarni takrorlamaydi.
- **Apostrof** (spec §2.3): saqlashda `o‘`, `g‘` uchun U+2018, tutuq belgisi uchun U+2019 (`TextNormalizer.Uzbek`); KAT interfeysidagi shakl. Solishtirish avvalgidek `TextNormalizer.Key`.

## L-18 · 2026-10-22 · Bo‘limlarga bo‘lish

Spec §2.2. Admin "Bo‘limlarga bo‘lish" ni bosadi (import avtomatik bo‘lmaydi: admin avval import natijasini ko‘radi).
- Nashr qilingan bo‘limlar va ularning so‘zlari **hech qachon** o‘zgarmaydi. Qolgan barcha kartaga mos so‘zlar chastota bo‘yicha (teng bo‘lsa `SourceKey` bo‘yicha) oxirgi nashr qilingan bo‘limdan keyin 25 tadan qayta joylanadi; qoralama bo‘limlar raqami bo‘yicha qayta ishlatiladi, ortiqchasi o‘chiriladi. Shuning uchun bo‘lishni importdan keyin takrorlash xavfsiz.
- Oxirgi bo‘limda 10 tadan kam so‘z qolsa, oldingi **yangi** bo‘limga qo‘shiladi (u 34 tagacha bo‘lishi mumkin). Oldingi bo‘lim nashr qilingan bo‘lsa, bu so‘zlar bo‘limsiz kutadi ("bo‘limga kirmagan" soni ko‘rsatiladi). To‘liq rasmiy ro‘yxat bir martada import qilinganda bu holat bo‘lmaydi.
- **Nashr tartib bilan:** n-bo‘lim faqat 1…n−1 nashr qilingan bo‘lsa (darajada bo‘shliq qolmasin, ochish ketma-ket — spec §4.2).

## L-19 · 2026-10-22 · AI qoralama, tahrir va tekshirish

Spec §2.3, §2.4.
- **Qoidalar bir joyda** (`CatalogContent`): tarjima — lotin, 1–3 ma’no `; ` bilan; o‘qilishi — faqat lotin harflari; misol — Hangul, ≤ 8 어절, tarjimasi bilan birga; tuzoq va mavzu ixtiyoriy. Muharrir ham, AI ham shu qoidadan o‘tadi (muharrirga 422 va sabab).
- **AI qoralama** (`POST /api/admin/units/{id}/draft`): bir chaqiruvda 10 so‘z (D-46). Prompt’ga so‘zning omonim raqami, turkumi va izohi (ma’noni aniq tanlash uchun) hamda shu va oldingi bo‘limlarning so‘zlari (≤ 400, misollar shulardan bo‘lishi uchun) beriladi. Tarjima, o‘qilish yoki misoli yaroqsiz so‘z **umuman saqlanmaydi** va keyingi chaqiruvda qayta so‘raladi; yaroqsiz tuzoq, mavzu yoki savol alohida tashlanadi. Hech narsa o‘tmasa — 503, bazada o‘zgarish yo‘q. Sahifa endpoint’ni so‘z qolmaguncha qayta chaqiradi (bitta so‘rov daqiqalab ishlamaydi). Bir bo‘lim uchun bir vaqtda bitta qoralama (advisory lock).
- AI sarfi qoralamani boshlagan muharrirning hisobiga yoziladi (`ai_usage`, kunlik limit `Ai:MaxCallsPerUserPerDay`, standart 150). A daraja ≈ 100 chaqiruv. "Iboralar" (`Phrases`) katalogda yozdirilmaydi: spec §2.4 ularni so‘ramaydi.
- **Tekshirish:** qoralama `Draft` bo‘lib qoladi; "Tekshirildi" faqat tarjima, o‘qilish va misol bor bo‘lsa. Tekshirilgan so‘z tahrirlansa ham `Reviewed` qoladi, tekshiruvchi — tahrirlagan muharrir. Nashr qilingan bo‘limdagi so‘zdan majburiy maydonni o‘chirib bo‘lmaydi.
- **`ContentVersion`** har kontent o‘zgarishida oshadi (spec §5.3). Savollar (`CatalogUsage`) so‘zning rasmiy shakli haqida, u esa o‘zgarmaydi; shuning uchun tahrirda savollar eskirgan deb tashlanmaydi — muharrir ularni so‘z bilan birga ko‘radi va keragini olib tashlaydi. `CatalogUsage.ContentVersion` qaysi versiya bilan tekshirilganini saqlaydi.
- Bir vaqtda tahrir: `xmin` versiyasi, eski versiya bilan saqlash — 409.

## L-20 · 2026-10-22 · O‘quvchi API’si 2-bosqichda

`GET /api/levels`, `GET /api/levels/{level}/units`, `GET /api/units/{id}` — faqat nashr qilingan bo‘limlar. Nashr qilinmaganlari faqat son sifatida ("Tez orada", spec §2.4), ularning id’si va so‘zlari serverdan chiqmaydi; `GET /api/units/{draftId}` — 404 (spec §13.3). Bo‘lim qulfi, ochish va progress — 3-bosqich; hozircha nashr qilingan bo‘lim so‘zlari har bir kirgan foydalanuvchiga ko‘rinadi.
Takrorlash hali oraliq `WordSense` modelida (L-04); `CatalogWord` → `LearningItem` → `Card` bog‘lanishi 3-bosqichda.

## L-21 · 2026-10-22 · Namuna seed

`Catalog:SeedSample` (Development’da `true`, production’da yo‘q): ishga tushganda namuna fayl (`content/samples/lugat_A_namuna_50.json`) yuklanadi. Uning kontentini Johnny yozgan va tekshirgan, shuning uchun so‘zlar `Reviewed` ("Namuna fayl"), bo‘lingach to‘liq bo‘limlar tartib bilan nashr qilinadi. Qayta ishga tushirish hech narsani o‘zgartirmaydi; muharrir tegingan so‘zga seed tegmaydi. Development’da dev foydalanuvchi (Telegram ID 1) admin (`Auth:Admins`), kontent sahifasini sinash uchun.

