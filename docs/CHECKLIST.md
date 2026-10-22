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

- **Production’da kirish yo‘q** (faqat Development dev-kirish). 1-bosqich: Telegram Login Widget, Google, `UserLogin` (L-03).
- **Zaxira va AI sozlamalari endpoint’lari har qanday kirgan foydalanuvchiga ochiq** (KAT’da 2 kishi edi). 1-bosqichdagi rollar bilan `Admin` ostiga o‘tadi (L-03).
- Production’da so‘z qo‘shish yo‘li yo‘q: katalog, import va seed 2-bosqichda (L-04).
- AI promptlari hali KAT auditoriyasi (TOPIK II, 3–4-daraja) uchun yozilgan; Lug‘at foydalanuvchisi boshlang‘ich daraja. Kontent va mashqlar bosqichlarida qayta ko‘rib chiqiladi.
- Navigatsiya hali KAT’niki (Bugun · Lug‘atlar · Mashq · Sinov). Spec §4.7 dagi panel (Bosh sahifa · Bo‘limlar · Takrorlash · Mashq · Profil) 3-bosqichda.
- Sozlamalar sahifasida zaxira va AI xizmati bo‘limlari hali hammaga ko‘rinadi (admin paneliga 7-bosqichda ko‘chadi).

### Namuna kontent bo‘yicha kuzatuvlar (2-bosqich uchun)

`content/samples/lugat_A_namuna_50.json` ko‘rib chiqildi:
- UZ→KO testida bir nechta to‘g‘ri javob chiqishi mumkin bo‘lgan sinonimlar: 또 "yana" / 더 "ko‘proq, yana" (ikkalasi 1-bo‘limda), 나 / 저 "men", 좋다 / 잘 "yaxshi". Distraktor tanlashda `translation_uz` ma’nolari kesishmasi tekshirilishi kerak (spec §4.4).
- `요`-shakl qoidasidan istisnolar: A-015 (`실패했다`, kitob uslubi), A-003 va A-026 (반말), A-017 va A-036 (`-습니다`). Ataylab, lekin §2.3 qoidasida istisno sifatida yozilishi kerak.
- Apostrof: faylda ASCII `'`, spetsifikatsiyada `‘`. Import kanonik shaklga keltiradi (`TextNormalizer`).
- `pos` o‘zbekcha ("fe'l", "ot"…), rasmiy ro‘yxatda koreyscha kod (동/명/보/의/관); `IsStudyable` filtri rasmiy kod bo‘yicha.
- "Qaysi so‘z to‘g‘ri?" savollari (`CatalogUsage`) namunada yo‘q.
- A-050 `trap_uz` matnida mayda xato ("전 저는 ning qisqasi").

## Bosqich 1 · Kirish va foydalanuvchilar

- [ ] `User` + `UserLogin`, Telegram Login Widget, Google, refresh (PWA 30 kun), logout, hisobni bog‘lash, o‘chirish, rollar, rate limit.

## Bosqich 2 · Katalog va kontent jarayoni

- [ ] `CatalogWord`, `CatalogUsage`, `Unit`; Excel import; bo‘lish; AI qoralama; tekshiruv va nashr; namuna JSON seed.

## Bosqich 3 · Bo‘limlar, kartalar, takrorlash

- [ ] `UserUnitProgress`, `LearningItem`, Bo‘limlar va Bo‘lim sahifalari, kunlik takrorlash, Bosh sahifa.

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
