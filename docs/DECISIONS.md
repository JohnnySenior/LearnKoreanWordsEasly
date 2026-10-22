# Texnik qarorlar jurnali

Har bir yozuv: sana, qaror, sabab, spec havolasi. Yangi yozuv oxiriga qo‘shiladi. Eski yozuv o‘chirilmaydi, faqat "o‘rniga: L-xx" deb belgilanadi.

Bu repo [KoreanAiTeacher](https://github.com/JohnnySenior/KoreanAiTeacher) (keyinda **KAT**) asosida qurilgan. Uning qarorlari `D-xx` raqami bilan KAT’ning [docs/DECISIONS.md](https://github.com/JohnnySenior/KoreanAiTeacher/blob/main/docs/DECISIONS.md) faylida to‘liq yozilgan. Bu yerda faqat qisqa havola qoldiriladi. Lug‘at’ning o‘z qarorlari `L-01` dan boshlanadi.

## KAT’dan olingan qarorlar

Quyidagilar kodda saqlangan va amal qiladi. Matni KAT’da; bu yerda faqat Lug‘at uchun farqi yoziladi.

| KAT | Mavzu | Lug‘at’dagi holati |
|---|---|---|
| D-01 | Bitta deploy: Api Blazor klientni ham tarqatadi | KAT D-01 dan olingan |
| D-02 | index.html da fingerprint placeholder yo‘q | KAT D-02 dan olingan |
| D-03 | Sessiya: bearer access 1 soat + refresh | KAT D-03 dan olingan. Telegram initData almashinuvi olib tashlandi (L-03). PWA uchun refresh 30 kun — 1-bosqichda (spec §6.3) |
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

## L-03 · 2026-10-22 · Kirish: 1-bosqichgacha oraliq holat

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
