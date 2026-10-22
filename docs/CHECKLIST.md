# Bajarish checklisti

Belgilar: `[x]` bajarildi va tekshirildi · `[~]` kod bor, real muhitda hali tekshirilmagan · `[ ]` qilinmagan.
Bosqichlar spec §13 bo‘yicha.

## Bosqich 0 · Repo va asos (2026-10-22)

- [x] KoreanAiTeacher `main` (`679c5b8`) git tarixisiz ko‘chirildi; manba SHA birinchi commit izohida (L-01).
- [x] `KoreanAiTeacher.*` → `Lugat.*`: loyihalar, namespace’lar, solution, `LUGAT_*` o‘zgaruvchilar, `lugat` baza foydalanuvchisi.
- [x] v1 ga kirmaydigan modullar olib tashlandi (pastdagi ro‘yxat, L-02).
- [x] Eski 20 ta migratsiya o‘chirildi, yangi `Initial` migratsiya (15 jadval). `dotnet ef migrations has-pending-model-changes` — o‘zgarish yo‘q (L-12).
- [x] `docs/SPECIFICATION.md` (shu repo uchun), yangi `docs/DECISIONS.md` (KAT qarorlariga havola + L-01…L-12), shu checklist, `docs/HOSTING.md`.
- [x] Yangi `AGENTS.md`, `README.md`, `.env.example`. CI (`.github/workflows/ci.yml`) va Dockerfile yangi nomlar bilan.
- [x] Namuna kontent `content/samples/lugat_A_namuna_50.json` (50 so‘z, A daraja 1–2-bo‘lim). Hali import qilinmaydi (2-bosqich).
- [x] `dotnet build -c Release`: 0 xato, 0 ogohlantirish.
- [x] `dotnet test`: 199 test yashil (Domain 82, Application 6, Api 111), real PostgreSQL 16 bilan. `node --test tests/web/*.test.mjs` yashil.
- [x] Ilova lokal ishga tushdi (Development): `/health` → ready, `POST /api/auth/dev` → token, `/api/me`, `/api/today` ishlaydi; `/api/auth/telegram` va `/api/listening` → 404.
- [x] Chromium 360 px: Bugun, Lug‘atlar, Mashq, Sinov, Sozlamalar, Qurilma tekshiruvi sahifalari ochiladi, konsolda xato yo‘q, yonlama scroll yo‘q.
- [x] CI GitHub’da yashil (run #1, `bosqich-0`): build, migratsiya tekshiruvi, barcha testlar.
- [x] Docker image CI’da build qilindi, `pg_dump` image ichida ishlaydi (`docker-image` job).

### Olib tashlangan modullar (spec §3.1 "Yo‘q")

| Modul | Nima olib tashlandi |
|---|---|
| PDF import, lug‘at parseri (D-09, D-10) | `Domain/Imports`, `Application/Imports`, `Infrastructure/Pdf`, `Api/Imports` (+ `ImportWorker`), PdfPig paketi, `/api/imports/*`, `/api/entries/*`, `ImportReview` sahifasi, import va parser testlari, real PDF testi |
| Grammatika (D-12, D-24, D-26–28) | `Domain/Grammar`, `Application/Grammar`, `Api/Grammar` (+ `GrammarWorker`), `/api/grammar/*` (kitob yuklash bilan), 7 ta grammatika sahifasi, grammatika sinovi va aralash sinov, gap tekshiruvi/zinapoya/xatolar daftari/zaif joylardagi grammatika qismlari, testlar |
| Kitob testlari, imtihon rejimi (D-33, D-35) | `BookTestService`, `BookTestWorker`, `BookCategories`, `/api/book-tests/*`, 4 ta sahifa, `BookChoice` savol turi, taymer va javoblar varaqasi, testlar |
| Imtihon sanasi (D-34) | `ExamPlanner`, `ExamService`, `/api/exam`, `User.ExamDate/TargetLevel`, `ExamCard`, eslatmadagi kun sanog‘i, testlar |
| TOPIK yozish (D-49) | `Domain/Essays`, `EssayService`, `/api/essays/*`, `Essays` sahifasi, testlar |
| Dars topshiriqlari va kalendar (D-15, D-18, D-43, D-44) | `LessonAssignment`, `LessonCalendar`, `LessonService`, `/api/assignments/*`, `User.TablesPerLesson/LessonDays`, `LessonPlanner`, Sozlamalardagi "Lug‘at rejasi", shanba dars eslatmasi, testlar |
| Telegram Mini App (D-04) | `TelegramInitDataValidator`, `/api/auth/telegram`, `Telegram:AllowedUserIds`, `telegram-web-app.js`, `telegram.js`, Mini App tugmasi, testlar |

### Saqlangan, lekin o‘zgargan

- Sessiya: dars o‘rniga "fokus" (jadval drill, xatolar daftari) — L-05.
- So‘zlar: manbasiz oraliq `WordSense` + `VocabularyService` — L-04. API jadval raqami bo‘yicha.
- Eslatmalar: dushanba–juma, tugma oddiy havola — L-06, L-07.
- Zaxira: qabul qiluvchi faqat `Backup:TelegramChatId`, paket PDF’siz — L-08.
- Tinglash: `Features:Listening=false` bilan yashirin — L-09.
- Sinov: faqat so‘z savollari va og‘zaki tayyorlik — L-10.

### Bosqich 0 dan keyin ochiq qolganlar (keyingi bosqichlarga)

- ~~Production’da kirish yo‘q~~ — 1-bosqichda yopildi (L-13).
- ~~Zaxira va AI sozlamalari endpoint’lari hammaga ochiq~~ — 1-bosqichda `Admin` ostiga o‘tdi (L-14).
- Production’da so‘z qo‘shish yo‘li yo‘q: katalog, import va seed 2-bosqichda (L-04).
- AI promptlari hali KAT auditoriyasi (TOPIK II, 3–4-daraja) uchun yozilgan; Lug‘at foydalanuvchisi boshlang‘ich daraja. Kontent va mashqlar bosqichlarida qayta ko‘rib chiqiladi.
- ~~Navigatsiya hali KAT’niki~~ — 3-bosqichda spec §4.7 paneli (L-25).
- Sozlamalar sahifasidagi zaxira va AI tekshiruvi bo‘limlari endi faqat adminga ko‘rinadi; admin paneliga 7-bosqichda ko‘chadi.

### Namuna kontent bo‘yicha kuzatuvlar (2-bosqich uchun)

`content/samples/lugat_A_namuna_50.json` ko‘rib chiqildi:
- UZ→KO testida bir nechta to‘g‘ri javob chiqishi mumkin bo‘lgan sinonimlar: 또 "yana" / 더 "ko‘proq, yana" (ikkalasi 1-bo‘limda), 나 / 저 "men", 좋다 / 잘 "yaxshi". Distraktor tanlashda `translation_uz` ma’nolari kesishmasi tekshirilishi kerak (spec §4.4).
- `요`-shakl qoidasidan istisnolar: A-015 (`실패했다`, kitob uslubi), A-003 va A-026 (반말), A-017 va A-036 (`-습니다`). Ataylab, lekin §2.3 qoidasida istisno sifatida yozilishi kerak.
- ~~Apostrof: faylda ASCII `'`~~ — seed va tahrir `TextNormalizer.Uzbek` bilan bitta shaklga keltiradi (L-17).
- ~~`pos` o‘zbekcha~~ — seed rasmiy kodga o‘giradi (`fe'l` → `동`), `IsStudyable` kod bo‘yicha (L-17).
- "Qaysi so‘z to‘g‘ri?" savollari (`CatalogUsage`) namunada yo‘q.
- A-050 `trap_uz` matnida mayda xato ("전 저는 ning qisqasi").

## Bosqich 1 · Kirish va foydalanuvchilar (2026-10-22)

- [x] `User` + `UserLogin`, migratsiya `Logins` (L-13).
- [x] Telegram Login Widget: `POST /api/auth/telegram-login`, hash va `auth_date` tekshiruvi (L-13).
- [x] Google: `POST /api/auth/google`, ID token imzosi, `aud`, `iss`, muddat (L-13).
- [x] Refresh 30 kun; refresh token qurilmada, access faqat xotirada (L-15).
- [x] Chiqish barcha qurilmalarda (`POST /api/auth/logout`, sessiya versiyasi) (L-13).
- [x] Hisob bog‘lash (`POST /api/me/logins`), boshqa foydalanuvchining logini 409.
- [x] Profil: ism, romanizatsiya, mavzu (`PATCH /api/me`); Sozlamalar → "Profil".
- [x] Hisobni o‘chirish (`DELETE /api/me`) — ma’lumotlar o‘chadi, tokenlar to‘xtaydi (L-13).
- [x] Rollar va `Admin` / `Editor` siyosatlari; birinchi admin `Auth:Admins` dan (L-14).
- [x] Rate limit: kirish 10/daqiqa/IP, sinov boshlash 20/soat/foydalanuvchi (L-16).
- [x] Dev kirish faqat Development’da (endpoint boshqa muhitda umuman yo‘q).
- [x] Eslatmalar faqat Telegram logini bor foydalanuvchilarga.

**Tekshirildi:**
- `dotnet build -warnaserror` toza; testlar: 82 (Domain) + 6 (Application) + 139 (Api) = 227, hammasi o‘tdi.
- Qabul mezonlari testlarda: noto‘g‘ri va eskirgan Telegram hash, noto‘g‘ri imzo / `aud` / `iss` / muddati o‘tgan Google token rad etiladi (unit + HTTP); 5 ta parallel kirish → bitta foydalanuvchi; boshqa foydalanuvchining so‘zi, sinovi va sessiyasi 404; chiqishdan keyin access va refresh 401; 29 kunlik refresh ishlaydi, 31 kunlik 401; o‘chirilgan hisob ma’lumotlari bazada qolmaydi; admin bo‘lmagan 403; 11-kirish va 21-sinov 429.
- Chromium, 360px: kirish sahifasi → dev kirish → Bugun; sahifa yangilanganda sessiya saqlanadi; Sozlamalar → Profil; chiqish → kirish sahifasi. Konsolda xato yo‘q, gorizontal scroll yo‘q.

**Ochiq qoldi:**
- Haqiqiy Telegram widget va Google tugmasi brauzerda sinalmadi: bot domeni (BotFather `/setdomain`) va Google Client ID kerak; deploy domeni 8-bosqichda (HOSTING.md → "Kirish").
- Railway proksi ortida rate limit uchun `ForwardedHeaders` sozlash — deployda (L-16).
- Haqiqiy telefonlarda (iOS Safari, Android Chrome) kirish sinovi — 6–8-bosqich.

## Bosqich 2 · Katalog va kontent jarayoni (2026-10-22)

- [x] `CatalogWord`, `CatalogUsage`, `Unit`, migratsiya `Catalog` (L-17).
- [x] Rasmiy ro‘yxat importi `POST /api/admin/catalog/import` (faqat admin): .xls/.xlsx/matn, ustunlar sarlavha bo‘yicha, idempotent, yaroqsiz qatorlar sabab bilan (L-17).
- [x] Bo‘lish `POST /api/admin/catalog/levels/{level}/split` (faqat admin): chastota bo‘yicha 25 tadan, `보/의/관` kirmaydi, nashr qilingan bo‘lim o‘zgarmaydi (L-18).
- [x] AI qoralama `POST /api/admin/units/{id}/draft` (muharrir): 10 tadan so‘z, server tekshiruvi, yaroqsiz javob saqlanmaydi (L-19).
- [x] Tahrir `PUT /api/admin/catalog/words/{id}`, "Tekshirildi" `POST …/review`, nashr `POST /api/admin/units/{id}/publish` — hammasi tekshirilgan va tartib bilan (L-18, L-19).
- [x] O‘quvchi API: `GET /api/levels`, `/api/levels/{level}/units`, `/api/units/{id}` — faqat nashr qilingan (L-20).
- [x] Namuna seed (Development): A daraja 1–2-bo‘lim nashrda (L-21).
- [x] Kontent sahifasi `/kontent` (Sozlamalar → Kontent, muharrir va admin): import, bo‘lish, bo‘limlar, AI qoralama, so‘z tahriri, savollarni olib tashlash, tekshirish, nashr.

**Tekshirildi:**
- `dotnet build` toza; testlar 95 (Domain) + 22 (Application) + 149 (Api) = 266, hammasi o‘tdi.
- Qabul mezonlari testlarda: real hajmdagi 5,965 qatorli fayl (A 982 · B 2,111 · C 2,872) to‘liq saqlanadi, qayta import 0 yangi / 5,965 o‘zgarmagan; A daraja bo‘limlari chastota tartibida, 25 tadan (oxirgisi 10–34), `보/의/관` hech bir bo‘limda yo‘q; nashr qilinmagan bo‘lim o‘quvchi ro‘yxatida yo‘q va `GET /api/units/{id}` 404; fake AI provayderning kirill tarjimali, 8 so‘zdan uzun misolli javobi saqlanmaydi, butunlay yaroqsiz javob — 503 va bazada o‘zgarish yo‘q.
- Qo‘shimcha: CP949 matnli fayl importi; sarlavhasiz / buzilgan / .pdf fayl — 422 va sabab; tartibsiz nashr — 422; eski versiya bilan tahrir — 409; muharrir import qila olmaydi (403), o‘quvchi admin endpoint’iga kira olmaydi (403); seed ikki marta ishlasa ham 50 so‘z.
- Chromium, 360px (Development): Sozlamalar → Kontent → A daraja (2/2 bo‘lim nashrda) → 1-bo‘lim → so‘z tarjimasini tahrirlab saqlash; brauzerdan .xlsx yuklash (40 qator → 39 mos so‘z, 25 + 14 so‘zli yangi bo‘limlar). Gorizontal scroll yo‘q, sahifa xatosi yo‘q.

**Ochiq qoldi:**
- ~~Haqiqiy rasmiy fayl sinalmadi~~ — 3-bosqichda asl fayl bilan sinaldi va 순위siz 68 so‘z uchun tuzatildi (L-27).
- Haqiqiy AI bilan qoralama sifati sinalmadi (testlar fake provayder bilan); birinchi bo‘limlarni Johnny ko‘rib chiqishi kerak.
- `Editor` rolini berish yo‘li yo‘q (admin paneli — 7-bosqich); hozircha faqat `Auth:Admins` dagi admin kontent bilan ishlaydi.
- Bo‘lim qulfi, ochish, kartalar va `LearningItem` — 3-bosqich (L-20); o‘quvchi uchun bo‘limlar sahifasi ham 3-bosqichda.
- UZ→KO sinonimlar (또/더, 나/저) bo‘yicha distraktor tekshiruvi — 4-bosqich (bo‘lim testi).

## Bosqich 3 · Bo‘limlar, kartalar, takrorlash (2026-10-22)

- [x] `LearningItem` (umumiy, katalog so‘ziga bog‘langan) `WordSense` o‘rniga; karta noyobligi `(OwnerId, LearningItemId, Direction)`; migratsiya `LearningItems` (L-22).
- [x] Nashr va tahrirda elementlar sinxron; ishga tushishda yetishmaganlari to‘ldiriladi (L-22).
- [x] `UserUnitProgress`: yangi foydalanuvchida A-1 ochiq, o‘tilgan bo‘limdan keyingisi ochiladi, qulflangan bo‘lim 403 (L-23).
- [x] Bo‘limni ochish `POST /api/units/{id}/open` — kartalar bir marta yaratiladi (L-23).
- [x] Kunlik takrorlash: barcha ochilgan bo‘limlardan muddati kelganlar, yangi so‘zlar faqat joriy bo‘limdan; bo‘lim drill’i (L-24).
- [x] Sahifalar: Bosh sahifa (bugungi takrorlash + joriy bo‘lim), Bo‘limlar, Bo‘lim (4 qadam: Jadval, Kartochka, Mashqlar, Test — 4-bosqichda), Takrorlash; navigatsiya spec §4.7 (L-25).
- [x] Jadval/yozma/sinov/qo‘llanish/xatolar daftari "jadval" o‘rniga bo‘lim bilan (L-22).
- [x] Matnli rasmiy fayl: UTF-16/UTF-8/CP949, bo‘shliq bilan ajratilgan, sarlavhasiz (L-26).
- [x] **Asl rasmiy fayl bilan sinaldi** (`content/official`, L-27): 5,965 qator to‘liq saqlanadi (68 ta 순위siz so‘z ham), qayta import dublikatsiz, A daraja — 36 bo‘lim, chastota tartibida, `보/의/관` siz.

**Tekshirildi:**
- `dotnet build` toza; testlar 95 (Domain) + 27 (Application) + 155 (Api) = 277, hammasi o‘tdi; KAT’dan kelgan scheduler va sessiya testlari yashil.
- Qabul mezonlari testlarda (`Catalog/UnitProgressTests`): yangi foydalanuvchida faqat A-1 ochiq, A-2 so‘zlari, ochilishi va drill’i — 403; bo‘limni bir necha marta (parallel ham) ochish 50 ta karta (25 × 2) beradi; 3-bo‘limdagi foydalanuvchining kunlik takrorlashida 1-bo‘limning muddati kelgan 6 kartasi va faqat 3-bo‘limning yangi so‘zlari chiqadi; bo‘limni o‘tish keyingisini ochadi, boshqa foydalanuvchiga ta’sir qilmaydi; nashrdan keyingi tahrir elementning matni va versiyasini yangilaydi.
- Chromium, 360px (Development, namuna seed): Bosh sahifa → joriy bo‘lim A-1; Bo‘limlar (A-1 ochiq, A-2 qulflangan, "Avval 1-bo‘limni tugating"); A-1 sahifasi — 25 so‘z jadvalda, so‘z bosilganda misol va tuzoq; Kartochka → 50 kartali drill; Takrorlash tabi. Gorizontal scroll yo‘q; konsolda faqat TTS 503 (dev’da ovoz kaliti yo‘q).

**Ochiq qoldi:**
- Bo‘lim testi va "Bilaman" — 4-bosqich (tugmalar hozir o‘chiq). Hozircha o‘quvchi faqat A-1 da ishlay oladi.
- "Qaysi so‘z to‘g‘ri?" mashqi hali har foydalanuvchining AI qo‘llanishidan; katalogning tekshirilgan savollariga (`CatalogUsage`) o‘tkazish kerak (spec §3.1: umumiy, bepul) — 4-bosqich test generatori bilan birga.
- Onboarding (spec §4.1, 2–3 ekran) va "Ilovani o‘rnatish" — 6-bosqich (PWA).

## Bosqich 4 · Bo‘lim testi va "Bilaman"

- [ ] Savol generatori (distraktor qoidalari), `WordListening`, ochilish, "Bilaman" limiti.

## Bosqich 5 · Premium va to‘lov

- [ ] `Subscription`, `Payment`, `PromoCode`, entitlement, 403 `premium-required`, admin to‘lov qayd etish.

## Bosqich 6 · PWA

- [ ] Manifest, ikonkalar, service worker, yangilanish xabari, o‘rnatish oqimi, yuklanish vaqti o‘lchovi.

## Bosqich 7 · O‘qituvchilar va admin

- [ ] Guruhlar, o‘qituvchi paneli, komissiya hisoboti, admin panel.

## Bosqich 8 · Eslatmalar, ishga tushirish

- [ ] Telegram eslatmalari (spec §8), maxfiylik siyosati, Railway alohida loyiha, backup, hard spend limit, real qurilmada to‘liq oqim.
