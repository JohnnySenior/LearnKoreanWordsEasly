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
| D-13 | O‘rganish birligi va ikki yo‘nalishli kartochkalar | KAT D-13 dan olingan. O‘rganish birligi — umumiy `LearningItem` (L-22) |
| D-14 | Sessiya rejasi, idempotent javob, undo | KAT D-14 dan olingan. Dars ustuvorligi o‘rniga "fokus" — joriy bo‘lim (L-05, L-24) |
| D-16 | Telegram eslatmalari, outbox | KAT D-16 dan olingan. Shanba dars sloti yo‘q (L-06), tugma oddiy havola (L-07) |
| D-17 | Tashqi tekshiruvdan keyingi tuzatishlar (undo, oflayn javob REL03) | KAT D-17 dan olingan. Import tahriri bandi olib tashlandi |
| D-19 | CI/CD: deploy faqat `main` dan | KAT D-19 dan olingan. Railway loyihasi hali yo‘q (8-bosqich) |
| D-21 | AI: Claude va ChatGPT, almashtirsa bo‘ladigan | KAT D-21 dan olingan |
| D-22 | Zaxira nusxa: har kecha, tiklab tekshiriladi | KAT D-22 dan olingan. Paketda PDF yo‘q: L-08 |
| D-23 | Yozma mashq kartochkadan alohida | KAT D-23 dan olingan. Dars o‘rniga bo‘lim bo‘yicha tanlash |
| D-25 | "Modernist" dizayn | KAT D-25 dan olingan. Telegram mavzusi o‘rniga qurilma sozlamasi: L-11 |
| D-29 | Talaffuz: OpenAI ovozi, serverda kesh | KAT D-29 dan olingan (Azure’dan keyin zaxira) |
| D-30 | Sinov tizimi (TEST01–TEST04) | KAT D-30 dan olingan. Faqat so‘z savollari: L-10. Bo‘lim testi shu dvigatelda: L-28 |
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

## L-04 · 2026-10-22 · Oraliq so‘z modeli: `WordSense` manbasiz (o‘rniga: L-22)

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
- **Haqiqiy fayl:** avval korean.go.kr ochilmagani uchun tavsif bo‘yicha yozilgan; keyin Johnny asl faylni berdi va u bilan sinaldi (L-27).
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

## L-22 · 2026-10-22 · `LearningItem`: umumiy o‘rganish birligi

Spec §5.2, §13.4. Oraliq `WordSense` (L-04) `LearningItem` ga aylantirildi (`learning_items`).
- **Shakl:** `Kind` (`Catalog`/`Personal`), `CatalogWordId?` (unikal), `OwnerId?`, `UnitId?`, `Order`, `Korean`, `KoreanNote`, `Uzbek`, `IsActive`, `ContentVersion`. Check constraint: katalog elementida so‘z bor va egasi yo‘q, shaxsiyda aksincha (spec §5.2 dagi "ikki nullable FK + check" varianti). V1 da faqat `Catalog` yaratiladi.
- **Katalog elementi umumiy:** bitta katalog so‘zi — bitta element, hamma foydalanuvchi uchun. Kartalar, javoblar, sessiyalar, yozma mashq, gap tekshiruvi `LearningItemId` ga bog‘lanadi. "Foydalanuvchining so‘zlari" — unda kartasi bor faol elementlar (`LearnerItems.Of`). Karta noyobligi spec’dagidek: `(OwnerId, LearningItemId, Direction)`; shaxsiy "qo‘llanish" (`WordUsage`) — `(OwnerId, LearningItemId)`.
- **Sinxronlash** (`LearningItemSync`): bo‘lim nashr qilinganda elementlar yaratiladi; nashrdan keyin so‘z tahrirlansa elementning matni yangilanadi va `ContentVersion` oshadi (kartalar va statistika saqlanadi, spec §5.3). Bitta bo‘limda bir xil yozilgan so‘zlarga `KoreanNote` (izoh yoki turkum) qo‘yiladi (spec §2.2). Ishga tushishda elementlari to‘liq bo‘lmagan nashr qilingan bo‘limlar to‘ldiriladi (2-bosqichda nashr qilinganlar uchun).
- **Tartib:** `Order = daraja × 1 000 000 + bo‘lim × 1 000 + o‘rni` — kurs tartibi, yangi so‘zlar shu tartibda.
- **"Jadval" → "bo‘lim":** API va sahifalarda jadval raqami o‘rniga `UnitId` va "A-3" yorlig‘i (`/api/vocabulary/units/{unitId}/usage`, `/api/writing/queue?unit=`, `CreateTestRequest.UnitIds`, `StartSessionRequest.UnitId`). `/api/vocabulary/tables*` va `VocabularyService` olib tashlandi.
- **Migratsiya `LearningItems`:** `word_senses` o‘chiriladi; unga bog‘langan oraliq mashq ma’lumoti (kartalar, javoblar, sessiyalar, yozma, gap tekshiruvlari, shaxsiy qo‘llanish) ham tozalanadi — u production’ga hech qachon chiqmagan, umumiy elementlarga ko‘chirib bo‘lmaydi.
- Kartochkadagi tuzoq: avval katalogning tekshirilgan `TrapUz`, bo‘lmasa foydalanuvchining AI qo‘llanishi (D-46).

## L-23 · 2026-10-22 · Bo‘lim progressi va qulf

Spec §4.2, §5.1, §5.3. `UserUnitProgress` (`user_unit_progress`, `(UserId, UnitId)` unikal): `Open`/`Passed`, yozuv yo‘q — qulflangan.
- **Faqat server ochadi** (`UnitProgressService.SyncAsync`, har o‘qishda): yangi foydalanuvchiga A-1; o‘tilgan bo‘limdan keyingisi — nashr qilinishi bilan (keyinroq nashr qilinsa ham, keyingi kirishda ochiladi). Darajaning oxirgi bo‘limidan keyin — keyingi darajaning 1-bo‘limi, agar o‘sha darajaning barcha bo‘limlari nashr qilingan bo‘lsa. Parallel so‘rovlar unikal indeks bilan himoyalangan.
- **Qulflangan bo‘lim:** `GET /api/units/{id}`, `POST /api/units/{id}/open` va bo‘lim drill’i — **403** ("Avval A-1-bo‘limni tugating."); nashr qilinmagan — 404 (L-20). Ro‘yxatda qulflangan bo‘lim ko‘rinadi (raqami, so‘zlar soni, mavzulari), so‘zlari emas.
- **Bo‘limni ochish** (`POST /api/units/{id}/open`, idempotent, foydalanuvchi bo‘yicha lock): yo‘q kartalar yaratiladi, `StartedAt` qo‘yiladi. Sahifa birinchi ochilganda klient shuni chaqiradi; bo‘lim drill’i ham shu orqali o‘tadi.
- `PassAsync(user, unit, by, score)` — 4-bosqichdagi test uchun: `Passed`, eng yaxshi ball, keyingi bo‘lim ochiladi. Hozir faqat testlarda chaqiriladi; klient bo‘limni "o‘tdi" deb yubora olmaydi.

## L-24 · 2026-10-22 · Kunlik takrorlash va bo‘lim drill’i

Spec §4.3, §4.6.
- **Kunlik takrorlash** (`POST /api/sessions` bo‘limsiz): barcha ochilgan bo‘limlarning muddati kelgan kartalari teng huquqli (bo‘lim ustuvorligi yo‘q); yangi so‘zlar faqat joriy (`Open`) bo‘limlardan. Planner’da faqat yangi kartalar "fokus"da, shuning uchun takrorlash bo‘limga bog‘lanmaydi.
- **Bo‘lim drill’i** (`StartSessionRequest.UnitId`): shu bo‘limning barcha kartalari, muddati kelmaganlari ham (D-36 dagidek); erta javob zinapoyada surmaydi.
- `GET /api/today`: muddati kelganlar (barcha bo‘limlar), joriy bo‘limdagi yangi kartalar va davom ettiriladigan bo‘lim (`CurrentUnit` — eng uzoqdagi ochiq bo‘lim).

## L-25 · 2026-10-22 · Navigatsiya va sahifalar

Spec §4.2, §4.3, §4.7. Pastki panel: **Bosh sahifa · Bo‘limlar · Takrorlash · Mashq · Profil**.
- Bo‘limlar (`/bolimlar`): daraja tablari, qulf/ochiq ("12/25 boshlangan")/tugallangan (eng yaxshi ball), 3 ta mavzu tegi. Qulflangan bo‘lim bosilsa: "Avval N-1-bo‘limni tugating"; navbatdagi qulflangan bo‘limda "Bilaman" tugmasi o‘chiq holda (4-bosqich).
- Bo‘lim sahifasi (`/bolimlar/{id}`): 4 qadam. Jadval — KAT D-36 jadvali (ustunni yopish, taymer), so‘z bosilsa talaffuz, misol, tuzoq, turkum; lotincha o‘qilishi profil sozlamasiga qarab. Kartochka — bo‘lim drill’i. Mashqlar — yozma (shu bo‘lim tanlangan holda), "Qaysi so‘z to‘g‘ri?". Test — 4-bosqichda (tugma o‘chiq).
- Takrorlash tabi `/takrorlash` (kartochka sahifasi): faol mashg‘ulot bo‘lmasa "Takrorlashni boshlash".
- "Sinov" tabdan chiqdi: Mashq sahifasidan ("Sinovlar"). Profil — Sozlamalar sahifasi.
- "Qaysi so‘z to‘g‘ri?" mashqi hali foydalanuvchining AI qo‘llanishidan (D-46); katalogning tekshirilgan savollariga (`CatalogUsage`) o‘tkazish — keyingi bosqichlarda (CHECKLIST).

## L-26 · 2026-10-22 · Rasmiy ro‘yxat — matnli fayl

Johnny: rasmiy hujjat `.txt` ko‘rinishida. Matnli o‘qish kengaytirildi (L-17 ga qo‘shimcha):
- Kodlash: BOM bo‘yicha UTF-16 (Excel "Unicode text") yoki UTF-8, aks holda UTF-8, bo‘lmasa CP949.
- Ajratuvchi: tab, vergul yoki (ikkalasi bo‘lmasa) bo‘shliqlar. Bo‘shliq bilan ajratilganda izohdagi bo‘shliqlar qayta birlashtiriladi, izohi yo‘q qator ham to‘g‘ri o‘qiladi (izoh ustuni turkum va daraja orasida bo‘lsa).
- Sarlavha bo‘lmasa: qatorlarning ≥ 80% raqam bilan boshlanib A/B/C bilan tugasa, rasmiy tartib (순위 단어 품사 풀이 등급) deb o‘qiladi; aks holda 422.
- Asl fayl (CP949, tab, sarlavhali) bilan sinaldi: L-27.

## L-27 · 2026-10-22 · Asl rasmiy fayl: 순위siz so‘zlar

Johnny asl faylni berdi; repoda `content/official/eohwi-mokrok-2003.txt` (KOGL 1, manbasi `content/official/README.md` da). Format: CP949, tab, sarlavha `순위 단어 품사 풀이 등급`, 5,965 qator (A 982 · B 2,111 · C 2,872). Test (`Catalog/OfficialListTests`) aynan shu fayl bilan.
- **68 so‘zda 순위 bo‘sh** (63 tasi atoqli ot `고`: 서울, 경주, 경복궁…; qolgani 구십, 깍두기, 그렇게…). Ular ro‘yxatning qismi, tashlanmaydi: `FreqRank` endi ixtiyoriy (migratsiya `OptionalRank`), bo‘lishda daraja ichida chastotali so‘zlardan keyin turadi. Bo‘sh emas, lekin son bo‘lmagan 순위 — avvalgidek xato.
- **Yangi turkumlar:** `고` (atoqli ot) va `불` (tasniflanmagan: 걔, 그래서, 이렇게 — so‘zlashuv shakllari). Ikkalasi kartaga mos (spec §2.2 faqat `보/의/관` ni chiqaradi); o‘zbekcha nomlari qo‘shildi.
- Bir xil yozilgan omonimlar haqiqatda bor (A: `가다01 동` va `가다01 보`) — alohida yozuv, yordamchisi kartaga kirmaydi (spec §2.2).
- **Natija:** kartaga mos so‘zlar A 904 → **36 bo‘lim** (oxirgisi 29 so‘z; spec’dagi "≈ 33" taxmin edi), B 2,043 → 82, C 2,786 → 112.

## L-28 · 2026-10-23 · Bo‘lim testi va "Bilaman"

Spec §4.4, §4.5, §13.5.
- **Model:** alohida `UnitTestAttempt` jadvali o‘rniga mavjud `TestRun` (D-30): `Purpose` (`Practice`/`UnitFinal`/`UnitSkip`), `UnitId`, `PassMark` (test tuzilganda `ceil(savollar × Units:PassPercent)`), `Passed`, `OpenedUnitId`. Javob berish, yakunlash, natijani yashirish (TEST03), xatolar daftari — umumiy. Eski sinovlar migratsiyada `Practice`.
- **API:** `POST /api/units/{id}/tests` (`kind: Final|Skip`, klient tanlagan `Id` — idempotent), `POST /api/unit-tests/{id}/answers`, `POST /api/unit-tests/{id}/finish` (spec §11); `/api/tests/{id}/…` ham ishlaydi.
- **Savollar** (`UnitTestGenerator`): har so‘zdan bittadan, aralash tartibda; 25 so‘zda 10 `WordMeaning`, 10 `WordKorean`, 5 `WordListening`, boshqa hajmlarda shu nisbatda (5 dan 1 tasi tinglash). Har urinish tasodifiy (yangi tartib, tur taqsimoti va distraktorlar); D-45 dagi "vaqtdan seed" qayta topshirishni bir xil qilib qo‘yardi, shuning uchun tasodifiy seed.
- **Distraktorlar:** shu va oldingi nashr qilingan bo‘limlardan, avval bir xil turkumdan. Hech qachon: bir xil yozilgan so‘z (omonim) va ma’nolaridan biri mos keladigan so‘z (sinonim) — ma’nolar `;` va `,` bo‘yicha bo‘linadi, qavs ichi hisobga olinmaydi ("men (do‘stona)" = "men"). Variantlar serverda aralashtiriladi.
- **Tinglash:** savolda so‘z matni yo‘q; yozuv `GET /api/tests/{id}/questions/{index}/audio` dan (server `BaseForm` dagi matnni o‘qiydi, u klientga chiqmaydi). Ovoz xizmati bo‘lmasa tinglash savollari ma’no savollariga almashtiriladi va testda ogohlantirish yoziladi.
- **Yakunlash** (`TestService.FinishAsync`): test bo‘yicha advisory lock + bitta tranzaksiya — `Finished` va natija birga. Qayta yuborilsa yoki parallel kelsa natija bir marta qo‘llanadi (`Passed` allaqachon bor). Yakuniy test: urinish soni va eng yaxshi ball; o‘tsa bo‘lim `Passed`, keyingisi ochiladi. Test kartochka jadvalini o‘zgartirmaydi (TEST04); xatolar daftarida manba "bo‘lim testi".
- **"Bilaman":** faqat foydalanuvchining ochiq bo‘limidan keyingi qulflangan, nashr qilingan bo‘lim (`UnitSummaryDto.CanSkip`); boshqasiga 403. Bir bo‘lim uchun kuniga `Units:SkipAttemptsPerDay` (3) — foydalanuvchining vaqt zonasidagi kun, 4-chisi 429. O‘tmasa bo‘lim qulflangan qoladi.
- **"Bilaman" o‘tilganda kartalar:** bo‘lim ochiladi, kartalar yaratiladi. To‘g‘ri javob berilgan so‘zning ikkala kartasi `Review`, 1-bosqich (1 kun), `DueAt = hozir + 1 kun` — yangi kartaga "Oson" bergandagi holat; `IndependentSuccessDays` 0, chunki variant tanlash mustaqil eslash emas (TEST04). Xato javob berilganlari `New`. Bo‘lim `Passed` (`SkipTest`), keyingisi ochiladi.
- UI: bo‘lim sahifasining 4-qadami, Bo‘limlar sahifasidagi "Bilaman", test natijasida o‘tdi/o‘tmadi, "Keyingi bo‘limga o‘tish", "Xato qilgan N ta so‘zni takrorlash" (shu so‘zlar bilan drill), "Testni qayta topshirish". Tabriklash sokin, konfettisiz.

## L-29 · 2026-10-23 · "Qaysi so‘z to‘g‘ri?" — katalog savollari

Spec §3.1 ("kontent katalog darajasida, umumiy, bepul"). Mashq va qo‘llanish testi endi avval katalogning tekshirilgan savollarini (`CatalogUsage`) ishlatadi; foydalanuvchining AI qo‘llanishi (D-46) faqat katalogda savoli yo‘q so‘zlar uchun qoladi ("Tayyorlash" ularni so‘raydi). Katalog savoli bor so‘z uchun AI chaqirilmaydi (BUD04).

## L-30 · 2026-10-23 · Premium, qo‘lda to‘lov, promo-kodlar

Spec §7, §13.6.
- **Model:** `Subscription` (`Plan`, `StartsAt`, `EndsAt`, `Source`, `PaymentId`), `Payment` (klient tanlagan `Id`, summa, kutilgan narx, chegirma, komissiya, `Confirmed`/`Refunded`), `PromoCode` (katta harfda saqlanadi — katta-kichik harf farqsiz unikal), `User.PaymentCode` ("L-7K3Q": 0/O, 1/I/L siz 4 belgi, unikal, Premium sahifasi birinchi ochilganda yaratiladi). Hisob o‘chirilsa to‘lov va obunalar buxgalteriya uchun qoladi (foydalanuvchi qatori anonim), to‘lov kodi tozalanadi.
- **Premium = hozir amal qilayotgan obuna** (`StartsAt ≤ now < EndsAt`), faqat serverda (`PremiumService`). Admin avtomatik premium emas.
- **Premium funksiyalar** (spec §7.1) endpoint filtri `RequirePremium()` bilan 403, `type: "premium-required"`, `detail: "Bu funksiya Premium’da."`: talaffuz baholash (`SpeakingService` ichida ham — og‘zaki sinov javoblari shu orqali), og‘zaki sinov yaratish, xatolar daftari, zaif joylar, gap zinapoyasi, tuzoqlar daftari, AI gap tekshiruvi (`/api/practice`), tinglash (v1.1). Bepul: bo‘limlar, jadval, kartochka, takrorlash, bo‘lim testlari, "Bilaman", yozma mashq, "Qaysi so‘z to‘g‘ri?" (katalog savollari), TTS, statistika; katalogdagi tuzoq izohi bepul (`Premium:TrapNotesFree`).
- **AI faqat Premium’da** (spec §7.4): `AiGateway` har chaqiruvdan oldin tekshiradi — Premium o‘quvchi, muharrir yoki admin (katalog qoralamasi va ulanishni tekshirish uchun). Bepul o‘quvchida AI xarajati 0: yozma mashq AI’siz tekshiradi ("Premium’da" izohi bilan), mashq sinovi gapli savollar o‘rniga variantli savollar tuzadi, shaxsiy "Tayyorlash" 403. Kunlik limit: Premium 60 (`Ai:MaxCallsPerUserPerDay`), muharrir/admin 300 (`Ai:MaxCallsPerEditorPerDay`), talaffuz 60 (`Ai:Tts:MaxAssessPerUserPerDay`).
- **Narxlar** konfiguratsiyada (`Premium:Plans`, standart oylik ₩5,000, 3 oylik ₩12,000), sinov muddati `Premium:TrialDays` (standart 0) — yangi foydalanuvchiga `Trial` obuna.
- **To‘lovni qayd etish** (`POST /api/admin/payments`, faqat admin): foydalanuvchi kod/ism/username/email bo‘yicha topiladi (`GET /api/admin/users?q=`); yangi muddat hozirgi Premium tugagan joydan boshlanadi (ikki to‘lov qo‘shiladi). Kutilgan narx = tarif × (1 − chegirma), komissiya = to‘langan summa × komissiya % (faqat o‘qituvchi promo-kodida), butun vonga yaxlitlanadi. Bir xil `Id` qayta yuborilsa — o‘sha to‘lov. Telegram logini bo‘lsa bot "Premium faollashdi" deb yozadi (xato bo‘lsa jim).
- **Qaytarish** (`POST /api/admin/payments/{id}/refund`): shu to‘lovning muddati bugun tugaydi (boshlanmagan bo‘lsa — umuman yo‘q), undan keyin navbatda turgan muddatlar olib tashlangan vaqtga oldinga suriladi — o‘quvchi qolgan to‘lovlari uchun to‘liq muddat oladi. Qayta qaytarish o‘zgarish qilmaydi.
- `IPaymentGateway` — faqat interfeys (v1 da implementatsiya yo‘q; Toss/PortOne keyin shu `Payment`/`Subscription` ni yaratadi).
- **UI:** Premium sahifasi (`/premium`: holat, tariflar, promo-kod tekshiruvi, rekvizitlar yoki "Tez orada", to‘lov kodi, chek uchun Telegram havolasi), Mashq menyusida premium funksiyalar "Premium" belgisi bilan va Premium sahifasiga olib boradi, istalgan `premium-required` javobida klient Premium sahifasini ochadi. Admin: `/admin/tolovlar` (qidirish, qayd etish, ro‘yxat, qaytarish, promo-kodlar). To‘liq admin panel — 7-bosqich.


## L-31 · 2026-10-23 · PWA: manifest, service worker, yangilanish, o‘rnatish, onboarding

Spec §4.1, §6.2, §13.7.
- **Ikonkalar:** vaqtinchalik brend belgisi — qizil (`#ec3013`) fon, oq geometrik "L" va apostrof (shriftsiz, 0 radius): 192, 512, maskable 512 (belgi markaziy 60% ichida), apple-touch-icon 180, favicon 32. Blazor’ning standart ikonkasi olib tashlandi. Yakuniy dizayn — Johnny’dan.
- **Manifest** (`manifest.webmanifest`): `name`, `short_name` "Lug‘at", `id`/`start_url`/`scope` "/", `display: standalone`, `lang: uz`, `theme_color`/`background_color` `#f3f2f2` (ilova foni). `index.html`: manifest, apple-touch-icon, iOS meta, qorong‘i mavzu uchun alohida `theme-color`.
- **Service worker** Blazor PWA shabloni asosida (`ServiceWorkerAssetsManifest`; publish’da `service-worker.published.js`, dev’da kesh yo‘q). Kesh nomi `lugat-shell-{versiya}` — versiya har publish’da asset xeshlaridan. O‘rnatishda ilova qobig‘i va `_framework` xesh (`integrity`) bilan keshlanadi; uch ICU faylidan qaysi biri kerakligi brauzer tiliga bog‘liq, shuning uchun ular oldindan emas, birinchi so‘ralganda keshlanadi (~400 KB tejaladi). Navigatsiya (`/bolimlar` va h.k.) keshdagi `index.html`. **`/api/*` va `/health` ga service worker umuman tegmaydi** (shaxsiy ma’lumot keshlanmaydi), boshqa domenlar (Google Fonts, Telegram, Google) ham. Aktivlashganda eski `lugat-shell-*` keshlar o‘chiriladi.
- **Ro‘yxatdan o‘tkazish** `Blazor.start()` tugagandan keyin (`autostart="false"`): birinchi kirishda service worker yuklab olishi ilovaning o‘z yuklanishi bilan tarmoq uchun raqobatlashmaydi, fayllarni HTTP keshidan qayta tekshiradi. `updateViaCache: 'none'`; server `service-worker.js` ni `no-cache` bilan beradi.
- **Yangilanish:** yangi versiya o‘rnatilib kutib turadi (o‘zi `skipWaiting` qilmaydi — ochiq sahifa eski fayllar bilan buzilmasligi uchun). Ilova buni sezsa pastda "Yangilanish tayyor · Qayta yuklash" chiqadi; bosilganda `SKIP_WAITING` → yangi versiya boshqaruvni oladi → sahifa bir marta qayta yuklanadi. O‘rnatilgan ilova kunlab ochiq qolishi mumkin, shuning uchun ekranga qaytganda yangilanish tekshiriladi (ko‘pi bilan 30 daqiqada bir).
- **O‘rnatish** (`InstallGuide`, `/ornatish`, Profil → "Ilovani o‘rnatish"): `beforeinstallprompt` `index.html` da Blazor’dan oldin ushlanadi; bo‘lsa "O‘rnatish" tugmasi (Chrome oynasi, bir marta). iPhone/iPad (iPadOS o‘zini Mac deb ko‘rsatadi — sensor bo‘yicha aniqlanadi): Safari → Ulashish → "Bosh ekranga qo‘shish" → "Qo‘shish", belgilar bilan 3 qadam. Boshqa holatda brauzer menyusi ko‘rsatmasi. Telegram ichidagi brauzerdan o‘rnatib bo‘lmaydi — avval Safari/Chrome’da ochish so‘raladi. O‘rnatilgan ilovada "Ilova o‘rnatilgan".
- **Onboarding** (spec §4.1): kirishdan keyin 3 qisqa ekran (25 talik bo‘limlar, test keyingi bo‘limni ochadi va "Bilaman", har kungi takrorlash) + o‘rnatish ekrani; "O‘tkazib yuborish" har ekranda. Belgi qurilmada (`localStorage` `lugat.onboarded`), serverda emas: o‘rnatish ham qurilmaga bog‘liq, yangi telefonda qayta ko‘rsatilishi to‘g‘ri. Mavjud foydalanuvchilar ham bir marta ko‘radi.
- **Offline:** to‘liq oflayn ishlash va’da qilinmaydi. Internet yo‘qligida ilova qobig‘i keshdan ochiladi va "Server bilan aloqa yo‘q" deydi; mashqda yuborilmagan javob mavjud REL03 navbatida (`lugat.pending-answer`) saqlanib, o‘sha `eventId` bilan qayta yuboriladi — service worker POST so‘rovlarga tegmaydi.
- **Hajm:** `InvariantGlobalization` o‘chirilmadi (ICU ~150–250 KB) — o‘zbek/koreys matnlari bilan madaniyatga bog‘liq ishlar xavfi foydadan katta; AOT yo‘q (spec §6.2). Trimming va Brotli publish’da yoqilgan.

## L-32 · 2026-10-23 · O‘qituvchilar, guruhlar va admin panel

Spec §9, §10, §13.8.
- **Model** (migratsiya `Teachers`): `Group` (`InviteCode` — 8 belgi, 0/O, 1/I/L siz, unikal; `ClosedAt`), `GroupMember` (guruh+o‘quvchi juftligi unikal; chiqish `LeftAt`, qayta qo‘shilish o‘sha qatorni tiklaydi), `CommissionPayout` (o‘qituvchi + oy unikal), `AdminAuditEntry`, `UserActivityDay` (foydalanuvchi + Koreya kuni), `User.PromoCodeId`.
- **Rozilik:** havolani ochish rozilik emas — `/join/{code}` sahifasi o‘qituvchi nimani ko‘rishini aytadi (bo‘lim natijalari, faollik, xato so‘zlar; javoblar tarixi, to‘lov va login emas) va "Roziman, qo‘shilish" bosilgandagina a’zo bo‘ladi. O‘quvchi Profil → "Guruhlarim" dan chiqadi; o‘qituvchi uni darhol ko‘rmaydi.
- **Ko‘rinish chegarasi** (`TeacherService`): o‘qituvchi faqat o‘zining yopilmagan guruhidagi, chiqmagan va o‘chirilmagan a’zoni ko‘radi; boshqa har qanday o‘quvchi, boshqa o‘qituvchining guruhi — 404 (mavjud emasdek). Endpoint’lar `Teacher` roli bilan (`Policies.Teacher`).
- **Guruh jadvali:** ism, joriy bo‘lim (eng oxirgi ochiq bo‘lim, hammasi o‘tilgan bo‘lsa oxirgi o‘tilgani), tugallangan bo‘limlar, oxirgi bo‘lim testi foizi, so‘nggi 7 kunda faol kunlar, oxirgi kirish. **"Eng qiyin 10 so‘z"** va o‘quvchining "eng ko‘p xato qilgan 10 so‘zi" — mavjud xatolar daftari (D-41, 60 kun) dan: guruhda xatolar yig‘indisi, keyin nechta o‘quvchi xato qilgani bo‘yicha.
- **Faollik:** "faol" = shu Koreya kuni ilovani ochgan (`UserActivityDay`). Har so‘rovda yozilmaydi: `SessionGuard` `LastSeenAt` bir soatdan eski yoki boshqa kunniki bo‘lsa yangilaydi va kunni yozadi (kirishda ham). Shu bilan "oxirgi kirish" ham to‘g‘ri bo‘ldi (avval faqat kirishda yangilanardi).
- **Promo-kod biriktirish:** guruhga qo‘shilganda o‘qituvchining amaldagi oxirgi promo-kodi, `/?ref=KOD` da o‘qituvchiga tegishli faol kod `User.PromoCodeId` ga yoziladi — **faqat birinchisi** (o‘quvchini birinchi olib kelgan o‘qituvchi komissiyani oladi). Premium sahifasi uni o‘zi to‘ldiradi va chegirmani ko‘rsatadi; admin to‘lov formasi ham uni oldindan qo‘yadi. O‘qituvchiga tegishli bo‘lmagan yoki noma’lum kod jim e’tiborsiz qoldiriladi.
- **Komissiya hisoboti** (`/api/teacher/commission`): o‘qituvchining kodlari, tasdiqlangan to‘lovlar soni va summasi, Koreya oyi bo‘yicha komissiya, "To‘landi" holati — to‘lovchilarning ismi yoki id’si yo‘q. Qaytarilgan to‘lov hisobga olinmaydi. Admin oyni "To‘landi" deb belgilaydi (`CommissionPayout`, o‘sha paytdagi summa bilan; qayta bosish o‘zgartirmaydi).
- **Admin panel** (`/admin`, faqat `Admin`): foydalanuvchi kartasi (loginlar, rollar, obunalar, progress, guruhlar, o‘zgarishlar tarixi); rol berish/olish (o‘zidan Admin’ni olib bo‘lmaydi); **Premium’ni qo‘lda o‘zgartirish faqat sabab bilan** — `+N` kun hozirgi muddatdan keyin `Gift` obuna, `−N` kun oxiridan boshlab olib tashlanadi (bugundan oldinga emas; to‘liq ishlatilgan davr nol uzunlikka tushadi, keyingi to‘lov to‘g‘ri joydan boshlansin); promo-kod o‘chiriladi/yoqiladi (o‘chirilmaydi — to‘lovlar unga bog‘langan); komissiya; sarf (`ai_usage` kun va funksiya bo‘yicha, shu oy byudjeti); ko‘rsatkichlar. Barcha qo‘lda o‘zgarishlar `admin_audit` ga yoziladi. `Editor` faqat kontent qismiga kiradi.
- **Ko‘rsatkichlar** (Koreya kunlari): foydalanuvchilar, 7/30 kunda yangilar, 14 kunlik kunlik faol, D1/D7 retention (oxirgi 30 kunda ro‘yxatdan o‘tganlardan aynan 1/7 kundan keyin ochganlar), hozir Premium, 6 oylik tushum, bo‘lim yakuniy testidan o‘tish foizi, **pilot ko‘rsatkichi** — guruh bo‘yicha: 14+ kun oldin qo‘shilganlardan 8–14-kunlarida faol bo‘lganlar.
- **Limitlar:** taklif kodi va referral so‘rovlari IP emas, **foydalanuvchi** bo‘yicha (30/soat) — bitta Wi-Fi’dagi butun sinf bir vaqtda qo‘shila oladi, kod taxmin qilish sekin qoladi. Shu sababdan token yangilash (`/api/auth/refresh`) ham kirish limitidan ajratildi: 60/daq/IP (`Auth:RefreshPerMinute`); kirish 10/daq/IP (spec §6.4) o‘zgarmadi.
- Hisob o‘chirilsa: a’zoliklar va faollik kunlari o‘chadi, promo biriktiruvi tozalanadi; o‘qituvchi hisobini o‘chirsa uning guruhlari yopiladi (o‘quvchilarning o‘z ma’lumoti o‘zida qoladi).
