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
- ~~Bo‘lim testi va "Bilaman"~~ — 4-bosqichda (L-28).
- ~~"Qaysi so‘z to‘g‘ri?" katalog savollariga~~ — 4-bosqichda (L-29).
- Onboarding (spec §4.1, 2–3 ekran) va "Ilovani o‘rnatish" — 6-bosqich (PWA).

## Bosqich 4 · Bo‘lim testi va "Bilaman" (2026-10-23)

- [x] Bo‘lim testi `TestRun` dvigatelida: `POST /api/units/{id}/tests`, `/api/unit-tests/{id}/answers`, `/finish` (L-28).
- [x] Savol generatori: har so‘zdan bittadan, 10/10/5 (`WordMeaning`/`WordKorean`/`WordListening`), distraktor qoidalari (omonim, sinonim yo‘q; bir xil turkum avval) (L-28).
- [x] `WordListening`: audio serverdan, matn klientga chiqmaydi; TTS bo‘lmasa ma’no savoliga almashadi va qayd etiladi.
- [x] Baholash serverda, ≥ 80% (`Units:PassPercent`) — o‘tdi, keyingi bo‘lim ochiladi; yakunlash bir marta qo‘llanadi.
- [x] "Bilaman": faqat navbatdagi qulflangan bo‘lim, kuniga 3 marta (`Units:SkipAttemptsPerDay`); o‘tsa to‘g‘ri so‘zlar 1 kunlik `Review`, xatolari `New` (L-28).
- [x] Xatolar daftari: manba "bo‘lim testi"; test kartochka jadvalini o‘zgartirmaydi.
- [x] "Qaysi so‘z to‘g‘ri?" — katalogning tekshirilgan savollari (L-29).
- [x] UI: bo‘lim sahifasida "Testni boshlash", Bo‘limlarda "Bilaman", natijada keyingi bo‘lim / xato so‘zlarni takrorlash / qayta topshirish.

**Tekshirildi:**
- `dotnet build` toza; testlar 95 (Domain) + 32 (Application) + 164 (Api) = 291, hammasi o‘tdi.
- Qabul mezonlari (`Catalog/UnitTestTests`, `UnitTestGeneratorTests`): 20/25 → o‘tdi, A-2 ochildi, eng yaxshi ball 80%; 19/25 → o‘tmadi, qayta topshirish boshqa savollar beradi; yakunlashni parallel 3 marta + yana 1 marta yuborish — urinish 1, natija bir marta; yakunlangandan keyin javob 409; "Bilaman" A-3 ga (navbatdagi emas) va ochiq bo‘limga 403, A-2 ga 3 marta OK, 4-chisi 429, ertasi kuni yana OK; o‘tilgan "Bilaman" — 42 karta `Review` (1 kun), 8 `New`, `IndependentSuccessDays` 0; distraktorlar orasida omonim va sinonim yo‘q (50 xil seed); test javobgacha `correct` qaytarmaydi, tinglash savolida matn yo‘q, audio faqat tinglash savoli uchun, boshqa foydalanuvchi testi 404; yakuniy test kartalarni o‘zgartirmaydi, xatolar daftariga "bo‘lim testi" bilan tushadi; TTS’siz 13 ma’no savoli va ogohlantirish.
- Chromium, 360px (Development): Bo‘limlar → A-2 qulflangan, "Bilaman" tugmasi; A-1 → 4. Test → 25 savol → yakunlash → "Hali o‘tilmadi", xato so‘zlar ro‘yxati, takrorlash va qayta topshirish tugmalari. Gorizontal scroll yo‘q, sahifa xatosi yo‘q.

**Ochiq qoldi:**
- Tinglash savoli haqiqiy ovoz bilan brauzerda eshitib ko‘rilmadi (dev’da TTS kaliti yo‘q; serverdagi yo‘l testlarda fake ovoz bilan sinalgan).
- Katalogda hali "Qaysi so‘z to‘g‘ri?" savollari yo‘q (namuna faylda yo‘q; AI qoralama bilan yoziladi va muharrir tekshiradi).

## Bosqich 5 · Premium va to‘lov (2026-10-23)

- [x] `Subscription`, `Payment`, `PromoCode`, `User.PaymentCode`; migratsiya `Premium` (L-30).
- [x] Entitlement (`PremiumService`), premium endpoint’larda 403 `premium-required` (L-30).
- [x] AI faqat Premium (muharrir/admin uchun ham), kunlik limitlar tarif bo‘yicha (spec §7.4).
- [x] `GET /api/me/premium`, `GET /api/premium/plans`, `POST /api/premium/promo/check`.
- [x] Admin: foydalanuvchi qidirish, to‘lovni qayd etish (muddatlar ketma-ket), qaytarish, promo-kodlar.
- [x] Premium sahifasi, Mashq menyusidagi Premium belgilari, `premium-required` da Premium sahifasiga o‘tish, admin to‘lovlar sahifasi.
- [x] Sinov muddati (`Premium:TrialDays`, standart 0), `IPaymentGateway` interfeysi.

**Tekshirildi:**
- `dotnet build` toza; testlar 104 (Domain) + 32 (Application) + 171 (Api) = 307, hammasi o‘tdi.
- Qabul mezonlari (`Premium/PremiumTests`): premiumsiz foydalanuvchi talaffuz, xatolar daftari, zinapoya (va zaif joylar, tuzoqlar, gap tekshiruvi, og‘zaki sinov, shaxsiy AI qo‘llanish) endpoint’larida 403 `premium-required`; bepul qismlar ishlaydi; AI bir marta ham chaqirilmaydi. To‘lov qayd etilgach darhol premium; 31 kundan keyin (FakeTimeProvider) yopiladi. Ikki to‘lov: 1 oy + 3 oy, ikkinchisi birinchisi tugagan kundan. Bir xil to‘lovni ikki marta yuborish — bitta to‘lov. Promo `ustoz10` (10%, o‘qituvchi, 30%): 12,000 → 10,800, komissiya 3,240; kod katta-kichik harfga qaramay unikal; o‘quvchi promo yarata olmaydi. Qaytarish: muddat bugun tugaydi, keyingi chorak shu kundan to‘liq 92 kun. To‘lov kodi bo‘yicha qidirish ("l7k3q" ham), rekvizitlarsiz `Bank = null`. Sinov muddati 7 kun.
- `PremiumMathTests`: chegirma, komissiya yaxlitlash, muddat boshlanishi.
- Chromium, 360px (Development): Mashq → 5 ta "Premium" belgisi → Xatolar daftari → Premium sahifasi ("Tez orada", kod `L-…`); `/xatolar` ga to‘g‘ridan-to‘g‘ri kirish ham Premium sahifasiga; admin `/admin/tolovlar` da kod bo‘yicha topib to‘lovni qayd etdi → Premium faol → Xatolar daftari ochildi. Gorizontal scroll yo‘q, sahifa xatosi yo‘q.

**Ochiq qoldi:**
- Bank rekvizitlari va kontakt Telegram (spec §15 Q4) — Johnny kiritadi (`Payments__*`).
- Telegram orqali "Premium faollashdi" xabari haqiqiy bot bilan sinalmadi (bot domeni/deploy 8-bosqich).
- Obuna muddatini qo‘lda o‘zgartirish (sabab bilan), promo-kodni o‘chirish, komissiya hisoboti — 7-bosqich (admin va o‘qituvchi paneli).

## Bosqich 6 · PWA (2026-10-23)

- [x] `manifest.webmanifest`, brend ikonkalari (192, 512, maskable, apple-touch, favicon), iOS meta (L-31).
- [x] Service worker (Blazor PWA shabloni): ilova qobig‘i va `_framework` keshi, `/api` keshlanmaydi, eski kesh o‘chiriladi.
- [x] "Yangilanish tayyor · Qayta yuklash" xabari.
- [x] O‘rnatish: Android "O‘rnatish" tugmasi (`beforeinstallprompt`), iPhone uchun 3 qadamli ko‘rsatma, `/ornatish`, Profil’da havola.
- [x] Onboarding: 3 ekran + o‘rnatish, o‘tkazib yuborsa bo‘ladi.
- [x] Offline javob navbati — mavjud REL03 mexanizmi (L-31); service worker unga tegmaydi.
- [x] Yuklanish vaqti o‘lchandi (Chromium, tarmoq/CPU cheklovi bilan) — quyida.

**Tekshirildi:**
- `dotnet build` toza; testlar 104 (Domain) + 32 (Application) + 174 (Api) = 310, JS testlar 10 — hammasi o‘tdi.
- `PwaTests`: manifest `application/manifest+json`, majburiy maydonlar, uchala ikonka bor; sahifada manifest va apple-touch-icon havolasi, Telegram SDK yo‘q; `service-worker.js` `no-cache`.
- `tests/web/service-worker.test.mjs`: o‘rnatishda xeshli kesh (service-worker.js va ICU fayllarisiz); `/api` GET, POST va navigatsiya hamda boshqa domenlarga javob bermaydi; istalgan sahifa keshdagi `index.html`; aktivlashganda faqat eski `lugat-shell-*` o‘chadi; `SKIP_WAITING` faqat so‘ralganda. `install.test.mjs`: installed/prompt/ios (iPadOS ham)/manual holatlari, Chrome taklifi bir marta ishlatiladi.
- **Publish natijasi** (`dotnet publish src/Lugat.Api -c Release`, Dockerfile bilan bir xil): trimming yoqilgan, har fayl `.br`/`.gz` bilan; `service-worker.js` (published varianti) va `service-worker-assets.js` (56 fayl, versiya) chiqishda bor.
- **Chromium (Playwright), published ilova:**
  - O‘rnatiladiganlik: CDP `Page.getInstallabilityErrors` → `[]`, `Page.getAppManifest` xatosiz (Lighthouse 12 da alohida "PWA" bo‘limi olib tashlangan — o‘sha tekshiruvning o‘zi shu CDP chaqiruvi).
  - Kesh: `lugat-shell-…` da 53 fayl. Internet o‘chirilib `/bolimlar` ochildi → ilova qobig‘i keshdan chiqdi, "Server bilan aloqa yo‘q" xabari, sahifa xatosi yo‘q.
  - **Yangi deploy:** eski versiya keshlangan profil bilan yangi publish (CSS o‘zgargan) ishga tushirildi → avval eski CSS keshdan → "Yangilanish tayyor" chiqdi → "Qayta yuklash" → keshlar `[eski, yangi]` dan `[yangi]` ga, yangi CSS, xabar yo‘qoldi, sahifa xatosi yo‘q.
  - Onboarding: kirishdan keyin 4 ekran, "Boshlash" → bosh sahifa; qayta yuklanganda qayta chiqmaydi. iPhone UA da `/ornatish` 3 qadam; `beforeinstallprompt` bo‘lganda "O‘rnatish" → "Ilova o‘rnatilgan".
- **Birinchi yuklanish o‘lchovi** (Chromium emulyatsiyasi, 360px, Google Fonts o‘chirilgan, kirish ekrani chiqquncha; uzatilgan hajm 2.6 MB Brotli):

  | Tarmoq va CPU | Birinchi | Keyingi (service worker keshidan) |
  |---|---|---|
  | 4G (9 Mbit/s, RTT 60 ms), CPU ×6 | 5.2 s | 2.4 s |
  | Slow 4G (1.6 Mbit/s, RTT 150 ms), CPU ×4 | 16.2 s | 1.6 s |
  | Slow 4G (1.6 Mbit/s, RTT 150 ms), CPU ×6 | 16.9 s | 2.6 s |

  Maqsad (spec §6.2: birinchi ≤ 6 s, keyingi ≤ 2 s) oddiy 4G’da birinchi yuklanish uchun bajarildi; sekin 4G’da 2.6 MB ning o‘zi ~13 s. Keyingi yuklanish tarmoqsiz, faqat CPU’ga bog‘liq: o‘rta telefon (×4) 1.6 s, juda zaif (×6) 2.4–2.6 s.

**Ochiq qoldi:**
- **Haqiqiy arzon Android telefonda 4G orqali o‘lchash** (spec §6.2 "TEKSHIRILADI") — deploy’dan keyin (8-bosqich) Johnny telefonida; natija shu jadvalga.
- **iPhone Safari’da bosh ekranga qo‘shib standalone ochilishi** — bu konteynerda iOS yo‘q; deploy’dan keyin haqiqiy iPhone’da tekshiriladi (manifest, apple-touch-icon va meta teglar tayyor).
- Android’dagi haqiqiy `beforeinstallprompt` oynasi ham deploy’dan keyin (HTTPS domen kerak).
- Yakuniy ikonka dizayni — Johnny.

## Bosqich 7 · O‘qituvchilar va admin (2026-10-23)

- [x] `Group`, `GroupMember`, `CommissionPayout`, `AdminAuditEntry`, `UserActivityDay`, `User.PromoCodeId`; migratsiya `Teachers` (L-32).
- [x] Guruhga qo‘shilish: `/join/{code}` rozilik sahifasi, `POST /api/groups/join/{code}`, Profil → "Guruhlarim" dan chiqish; `/?ref=KOD`.
- [x] O‘qituvchi paneli `/oqituvchi`: guruhlar, taklif havolasi (nusxa, yangilash), guruhni yopish, o‘quvchilar jadvali, eng qiyin 10 so‘z, o‘quvchi sahifasi (bo‘limlar, eng ko‘p xato qilgan 10 so‘z).
- [x] Komissiya hisoboti (o‘qituvchi) va admin "To‘landi".
- [x] Admin panel `/admin`: foydalanuvchilar (qidirish, progress, rollar, Premium’ni sabab bilan o‘zgartirish, tarix), to‘lovlar, promo-kodni o‘chirish/yoqish, komissiya, sarf, ko‘rsatkichlar (pilot ko‘rsatkichi bilan).

**Tekshirildi:**
- `dotnet build` toza; testlar 106 (Domain) + 32 (Application) + 187 (Api) = 325, JS testlar 10 — hammasi o‘tdi.
- **Qabul mezonlari** (`Teaching/TeachingTests`, `Teaching/AdminPanelTests`):
  - O‘qituvchi faqat o‘z guruhidagi **rozilik bergan** o‘quvchini ko‘radi: havolani ochish (taklifni ko‘rish) a’zolik emas; qo‘shilmagan o‘quvchi — 404; boshqa o‘qituvchi bu guruh va o‘quvchini — 404; rolsiz foydalanuvchi — 403.
  - Guruhdan chiqqan o‘quvchi darhol ko‘rinmaydi (jadvalda yo‘q, sahifasi 404); yopilgan guruh yoki yangilangan havoladan keyin eski havola 404; hisobini o‘chirgan o‘quvchi jadvaldan chiqadi; o‘chirilgan o‘qituvchining guruhlari yopiladi.
  - Komissiya hisobotida to‘lovchining ismi ham, id’si ham yo‘q; ikki to‘lov (4,500 + 10,800) → komissiya 4,590, qaytarilgani hisobga olinmaydi; admin "To‘landi" — bir marta, o‘qituvchida ham "To‘landi".
  - `Editor` kontentga kiradi, lekin to‘lovlar, foydalanuvchilar, promo-kodlar, komissiya, sarf va ko‘rsatkichlar (GET va POST/PUT) — 403.
- Rollar: berilgan `Teacher` keyingi so‘rovdan ishlaydi, olingani ham; o‘zidan Admin’ni olib bo‘lmaydi; tarixga o‘zbekcha yoziladi. Premium: sababsiz — 422; +30 kun sovg‘a to‘lov muddatidan keyin; −40 kun sovg‘ani va to‘lovning 10 kunini oladi; −365 → Premium yo‘q.
- Guruh jadvali: 2 kun mashq (har kuni "Eslay olmadim") → joriy A-1, 7 kunda 3 faol kun, eng qiyin so‘z 2 xato / 1 o‘quvchi; javobda login yo‘q.
- Ko‘rsatkichlar: 3 foydalanuvchi, ertasi kuni 2 tasi qaytdi → D1 67%, kunlik faol [2, 3], tushum, sarf (kun va funksiya, narxi noma’lum chaqiruv).
- Bitta tarmoqdagi 15 o‘quvchi ketma-ket qo‘shila oladi; kirish limiti tugagan IP’dan ham token yangilash ishlaydi.
- `KoreaTimeTests`: kun va oy Seul yarim tunida almashadi, kun boshi UTC’da (PostgreSQL faqat UTC qabul qiladi — brauzer sinovida topilgan xato).
- Chromium, 360px (Development, admin): Profil → Admin panel → Foydalanuvchilar (qidirish, o‘qituvchi rolini olib/qaytarish, +7 kun Premium, tarix) → O‘qituvchi paneli (guruh yaratish, havola) → `/join/…` (rozilik matni; o‘z guruhiga — tushunarli xato) → Komissiya, Sarf, Ko‘rsatkichlar, To‘lovlar. Gorizontal scroll yo‘q, sahifa xatosi yo‘q.

**Ochiq qoldi:**
- Ikkinchi haqiqiy foydalanuvchi bilan (o‘qituvchi va o‘quvchi alohida telefonlarda) to‘liq oqim — deploy’dan keyin; API testlarda sinalgan.
- Komissiya "To‘landi" deb belgilangandan keyin o‘sha oy to‘lovi qaytarilsa, hisobot yangi summani ko‘rsatadi, to‘langan summa `CommissionPayout.AmountKrw` da qoladi — farqni Johnny qo‘lda hal qiladi.

## Bosqich 8 · Eslatmalar, ishga tushirish

**Kod qismi (2026-10-23):**
- [x] Telegram eslatmalari spec §8 bo‘yicha: har kuni 20:00, kunlar tanlanadi, faqat muddati kelgan so‘z bo‘lsa, tugma `/takrorlash` ni ochadi; migratsiya `DailyReminders` (L-33).
- [x] Maxfiylik siyosati `/maxfiylik` (qoralama, kirishsiz ochiladi) va Profil → "Haqida" (manba, KOGL 1).
- [x] Qattiq xarajat chegarasi: tarifsiz chaqiruvlar ham oylik son bilan to‘xtaydi.
- [x] Railway proksisi ortida mijoz IP’si (`ForwardedHeaders`), deploy qadamlari `docs/HOSTING.md` da.

**Tekshirildi:**
- `dotnet build` toza; testlar 106 (Domain) + 32 (Application) + 191 (Api) = 329, JS testlar 10 — hammasi o‘tdi.
- **Qabul (fake clock):** `ReminderPlannerTests` — 20:00 dan bir daqiqa oldin yo‘q, shanba/yakshanba/dushanba 20:00 da bor; tanlanmagan kunda yo‘q; 30 daqiqadan kech — yo‘q. `RemindersTests` — 3 ta so‘z muddati kelganda dushanba 20:01 da bitta xabar "Bugun 3 ta so‘zni takrorlash vaqti. ~… daqiqa.", tugma yo‘li `takrorlash`, qayta ishga tushirilganda ikkinchi xabar yo‘q; faqat yangi so‘zlar bo‘lsa xabar yo‘q; seshanba va shanba 07:30 tanlanganda dushanba yo‘q, seshanba 07:30 bor, chorshanba yo‘q; bo‘sh kunlar — 422; o‘chirilgan eslatma yuborilmaydi; Google bilan kirganga yuborilmaydi.
- Xarajat: tarif kiritilmagan, chegara 1 ta → ikkinchi chaqiruv 429, provayderga bitta so‘rov.
- Proksi: `X-Forwarded-For` bo‘yicha limit har mijozga alohida; `198.51.100.7, 203.0.113.2` dagi birinchi (mijoz yozgan) qiymat hisobga olinmaydi.
- Chromium, 360px: kirish ekranida "Maxfiylik siyosati" → kirishsiz ochildi (6 bo‘lim); Sozlamalar → Eslatmalar: vaqt, 7 kun bir qatorda, Sh/Ya olib saqlandi; "Haqida" bor; gorizontal scroll yo‘q, sahifa xatosi yo‘q.

**Deploy (2026-10-23, Railway connector):**
- [x] Alohida loyiha `Lugat` (region `sfo`): `Postgres` 18, `app` (GitHub `main`, Dockerfile), volume `app-data` → `/data`, healthcheck `/health`, uxlash o‘chiq, domen `app-production-d5ca.up.railway.app` (`docs/HOSTING.md`).
- [x] Maxfiy bo‘lmagan o‘zgaruvchilar: connection string (Postgres reference), `/data/*` yo‘llari, `Production`, `RAILWAY_RUN_UID=0`, `PORT=8080`.
- [x] Kirish: `Google__ClientId`, `Telegram__BotUsername=KoreanLugatBot`, `Telegram__BotToken` (Johnny, dashboard’da), `Auth__Admins__TelegramUserIds__0`, `Backup__TelegramChatId`; BotFather `/setdomain`. Deploy `db8f488d` SUCCESS.

**Tekshirildi (Railway loglari):**
- Deploy `c9c6ab1a` (`863d1e5`) — SUCCESS: build ~3 daqiqa, 9 ta migratsiya bo‘sh bazaga qo‘llandi, `Now listening on http://[::]:8080`, `Hosting environment: Production`, healthcheck `/health` o‘tdi (u bazaga ulanishni tekshiradi).
- Volume yoziladi: Data Protection kaliti `/data/keys` da yaratildi; ishga tushganda zaxira `lugat-20261023-055928.dump` (72 534 bayt) yozildi va tiklab tekshirildi — `verified True` (pg_dump 18 ↔ Postgres 18 mos).
- **Haqiqiy qurilmalarda (Johnny, 2026-10-23):** Telegram bilan kirish Windows Chrome va iPhone Safari’da (iOS 18.7) ishladi — `POST /api/auth/telegram-login` 200, ikkalasi bitta hisob. Google bilan kirish (200) ham ishladi. Google birinchi marta alohida kirganda yangi hisob ochildi, uni Telegram’ga bog‘lash 409 "boshqa foydalanuvchiga bog‘langan" bilan rad etildi (spec §5.1: birlashtirilmaydi). Johnny Google hisobini o‘chirdi, Telegram bilan kirib Google’ni bog‘ladi. Zaxira nusxa bot orqali Telegram’ga yuborildi va keldi.
- Tashqi HTTP so‘rovni bu sessiyadan yuborib bo‘lmadi: agent proksisi `*.up.railway.app` ni bloklaydi (ishlab turgan KoreanAiTeacher domeni ham ochilmaydi). Brauzerdagi tekshiruv — Johnny.

**Ochiq qoldi:**
- [x] "Wait for CI" yoqilgan: servis sozlamasida `source.checkSuites: true` (Railway connector, 2026-10-26).
- [ ] **Johnny, dashboard’da:** Workspace → Usage → hard spend limit (masalan $10/oy) — connector’da ko‘rinmaydi, tasdiqlanmagan.
- [ ] **Johnny, o‘zgaruvchilar** (maxfiy qiymatlar, `docs/HOSTING.md` 7–8-band): AI va Azure kalitlari, `Ai__Budget__*` tariflari; `Payments__*`.
- [ ] Kontent: production’da `Catalog__SeedSample` o‘chiq — admin kirgach `/kontent` orqali rasmiy ro‘yxat importi va nashr.
- [ ] Production’da kechki (04:00 KST) zaxira va tiklash mashqi, haftalik Telegram nusxasi.
- [ ] Haqiqiy bot bilan eslatma: BotFather `/setdomain` va zaxira yuborish bajarildi; "Sinov xabari" va 20:00 dagi xabar qoldi.
- [ ] **Real iPhone va Android’da to‘liq oqim** (spec §13.9): kirish → 1-bo‘lim → test → 2-bo‘lim ochildi → ertasi kuni takrorlash; PWA o‘rnatish, birinchi yuklanish vaqti (6-bosqichdagi ochiq bandlar).
- [x] Maxfiylik siyosati matnini Johnny tasdiqladi (Q7, 2026-10-24).

**AI holati va kalitlar (L-34, 2026-10-23):**
- [x] Sozlamalar → AI xizmati endi kalit borligini emas, oxirgi haqiqiy chaqiruv natijasini ko‘rsatadi: "ishlayapti" / "ishlamayapti: sabab" / "hali tekshirilmagan". Sabab — production’da Anthropic kaliti ortiqcha belgi bilan kiritilganda ham "ulangan" deb ko‘rsatgani (Johnny topdi).
- [x] AI va Azure kalitlari, model va region chetidagi bo‘sh joy/qator oxiri/qo‘shtirnoq avtomatik olib tashlanadi; muvaffaqiyatsiz chaqiruv logida xato turi bor.
- Testlar: `Status_shows_how_the_last_call_ended_not_only_that_a_key_is_set` (kalit bor → tekshirilmagan; ping natijasi → ishlayapti/ishlamayapti sababi bilan; tuzatilgach keyingi chaqiruvdan so‘ng ishlayapti), `Keys_pasted_with_spaces_line_breaks_or_quotes_are_cleaned`.

**Bo‘lim jadvali uslubi (2026-10-23):**
- [x] Johnny iPhone’da topdi: bo‘limdagi so‘z jadvali chiziqsiz, ustunlar tekislanmagan, uzun koreyscha so‘z (아니다) ikki qatorga bo‘linardi. Sabab — `Unit.razor` jadvali KoreanAiTeacher’dan olingan, lekin `.word-table`, `.covered`, `.study-bar` uslublari ko‘chmay qolgan edi. KAT’dagi uslublar qo‘shildi (D-36).
- Chromium, 390px, qorong‘i va yorug‘ mavzu: ustunlar №/한국어/O‘zbekcha, sarlavha ostida qalin chiziq, qatorlar orasida ingichka chiziq, koreyscha so‘z bo‘linmaydi; "한국어 yopiq" — chiziqli yopiq katak, bosilgan so‘z ochiladi va ostida ovoz va qo‘llanish chiqadi. Gorizontal scroll yo‘q, sahifa xatosi yo‘q.

**Ovoz birinchi bosishda (L-35, 2026-10-23):**
- [x] Birinchi bosishda telefonning sun’iy ovozi, ikkinchisida Azure chiqishi tuzatildi: yozuv kelguncha kutiladi va bosish boshlagan elementda o‘ynatiladi.
- Chromium (`--autoplay-policy=user-gesture-required`, `/api/tts` 1.5 s kechiktirilgan): eski kod — birinchi bosish `speechSynthesis`, ikkinchisi yozuv (xato takrorlandi); yangi kod — birinchi bosishda "Ovoz yuklanmoqda…", keyin yozuv, telefon ovozi yo‘q; server 503 qaytarsa telefon ovozi zaxira bo‘lib qoladi. JS test: tayyorlangan element yozuvni o‘ynatadi (11/11).
- [ ] Haqiqiy iPhone’da tasdiqlash — Johnny (bu konteynerda iOS yo‘q).

**Koreyscha ikonkasi va dizayn fayllari (2026-10-23):**
- [x] Johnny yuborgan `koreyscha-icons.zip`: yangi logo (ko‘k #0047A0, oq belgi, qizil nuqta) — `icons/` dagi PNG’lar, `icon-maskable-192.png`, `favicon.ico`, `favicon.svg`, `icon.svg`. Manifest: `name`/`short_name` "Koreyscha", `theme_color`/`background_color` #0047A0; iOS sarlavhasi "Koreyscha". Service worker keshi `assetsManifest.version` bo‘yicha o‘zi yangilanadi (fayl xeshlari o‘zgardi). 6-bosqichdagi "yakuniy ikonka dizayni" bandi yopildi.
- [x] Ilova ichidagi nom ham "Koreyscha" (L-36, 2026-10-24): header, sahifa sarlavhalari, yuklanish ekrani, o‘rnatish yo‘riqnomasi, zaxira xabari.
- Ikkala zip ham repo ildizidan olindi, tarkibi `docs/design/ui-handoff/` va `docs/design/koreyscha-icons/` da.

**UI handoff — umumiy asos (2026-10-23):**
- [x] Tokenlar: neutral-300/400/500/600/800, accent-400/900 (yorug‘ va qorong‘i); Archivo va Noto Sans KR 900; `--tabbar-height` 64px.
- [x] Pastki menyu: Bosh · Bo‘limlar · Takror · Mashq · Profil (lucide ikonkalar, chapga tekis, faol katak tepasida 3px qizil chiziq); hover faqat sichqonchali qurilmada.
- [x] Umumiy komponentlar: `Icon`, `AppHeader`, `ListRow`, `SectionLabel`, `Toggle`, `Tag`, `UnitStepper`; `.btn-primary/.btn-secondary/.btn-ghost`, `.progress`, `.skeleton`. Segmented — mavjud `.seg`.
- [x] `/profil` manzili (hozircha Sozlamalar sahifasi; `/sozlamalar` ham ishlaydi), havolalar `/profil` ga.
- Chromium 360px: bosh sahifa, yonlama scroll yo‘q, 44px dan kichik nishon yo‘q, xato yo‘q.
- [x] **1b Bosh sahifa** (`/`): header "Lug‘at" + sana; "Bugun takrorlash" — katta raqam (88px/900) `DueNow` va ~daqiqa (`SecondsPerCard`), "Boshlash" (20 daqiqalik takrorlash, vaqt tanlagich dizaynda yo‘q); boshlangan mashg‘ulot bo‘lsa "Davom etish" va "Yangidan boshlash"; bo‘sh holat "Bugun takrorlash yo‘q". Joriy bo‘lim: raqam, `Started/WordCount`, mavzu teglari, 4 qadam (hozirgi qadam taxmini: hech boshlanmagan → Jadval, qisman → Karta, hammasi → Test), "Davom etish — …" → `/bolimlar/{id}?qadam=N` (bo‘lim sahifasi qadamni ochadi). Skelet holati. Chromium 360px yorug‘/qorong‘i: scroll yo‘q, xato yo‘q.
- Ochiq: "1- va 2-bo‘limdan" kabi aniq bo‘lim ro‘yxati va keyingi takrorlash vaqti (bo‘sh holatda) API’da yo‘q — umumiy matn ko‘rsatiladi.
- [x] **1c Bo‘limlar** (`/bolimlar`): sarlavha 30px; daraja tanlagich 3 ustun ("N bo‘lim" yoki "Tez orada"); qatorlar `64px 1fr auto`, raqam "01" 900/24px: tugallangan (eng yaxshi ball + check), ochiq (surface fon, qizil raqam, `Started/WordCount`, 150px progress), navbatdagi qulflangan ("Avval N-bo‘limni tugating" + "Bilaman — testni topshirish", endi tugma doim ko‘rinadi), qulflangan (kulrang, qulf). Skelet holati. 360px: scroll yo‘q, xato yo‘q.
- Ochiq: dizayndagi "3/3 urinish" (bugun qolgan «Bilaman» urinishlari) API’da yo‘q — o‘rniga "Kuniga 3 marta." matni.
- [x] **1d Bo‘lim · Jadval** (`/bolimlar/{id}`, dizayndagi `/bolim/{id}` o‘rniga mavjud manzil): header (orqaga, "N-bo‘lim", taymer 1px ramkada — sahifa ochilganda 10 daqiqadan boshlanadi, bosilsa 5 → 10 → 15, tugasa qizil "00:00"); ixcham 4 qadam (faolida 3px chiziq); "Yopish: Hech · 한국어 · O‘zbekcha"; jadval `36px 1fr 1fr`, KO 17px/700 + romanizatsiya 11px, UZ 14px, yopiq katak 24px neutral-300; so‘z bosilsa ovoz, turkum, tuzoq bloki (accent-100 fon, `triangle-alert`), misol; pastda doim ko‘rinadigan "Kartochkaga o‘tish →". 2–4-qadamlar yangi tugma/qator uslubida. 360px: scroll yo‘q, xato yo‘q.
- [x] **1e Kartochka** (`/mashq/kartochka`, `/takrorlash`, yangi `/sessiya`): yuqorida X, 4px qora progress, "N / M"; karta bloki: tag-outline "KO → UZ"/"UZ → KO", o‘ngda tur va bo‘lim; so‘z Noto 900/64px; 48px ovoz tugmasi; "Ma’nosini eslang, keyin tekshiring." + "Javobni ko‘rsatish" (`eye`); ochilganda tarjima 28px, tuzoq bloki; baho — 3 ustunli grid, o‘rtadagi qora, ostida server bergan keyingi vaqt. Chromium 360px: scroll yo‘q, xato yo‘q.
- Kelishilgan talab saqlandi: baho nomlari dizayndagi "Qiyin · Yaxshi · Oson" emas, spec’dagi "Eslay olmadim · Qiynalib esladim · Oson esladim" (ma’nosi boshqacha: birinchisi — eslay olmadim). Dizayndagi "Misol" bloki va so‘z turkumi (`fe’l`) kartochka API’sida yo‘q — ko‘rsatilmaydi.
- [x] **1f Bo‘lim testi** (`/sinov/{id}`): header X + test nomi + "N / M"; har savol uchun segment (javob berilgan qora, joriy qizil, qolgan kulrang, 6px); "Bu so‘z nimani bildiradi?" / "Koreyschasi qaysi?" + so‘z (KO 72px/900); variantlar to‘liq kenglikda A–D harflari bilan, tanlangan qora fon, to‘g‘ri/xato ko‘rsatilmaydi; pastda "O‘tish uchun N ta to‘g‘ri javob kerak…" + "Keyingisi →", "← Oldingi", "Hozir yakunlash". Og‘zaki va gapli savollar o‘z ko‘rinishida qoldi.
- [x] **1g Test natijasi** (bo‘lim testlari uchun): "To‘g‘ri N/M" | "Ball P%" (o‘tganda qizil fon, o‘tmaganda qora ramka); "Bo‘lim tugadi. Keyingi bo‘lim ochildi." yoki "Hali emas — N ta kerak edi"; "Xato javoblar · N" — KO so‘z | to‘g‘ri javob + berilgan javob (chizilgan); tugmalar: o‘tdi — "Keyingi bo‘limni boshlash", "Xato N ta so‘zni takrorlash"; o‘tmadi — "Xato so‘zlarni takrorlash", "Testni qayta topshirish". Boshqa sinovlar (amaliy, og‘zaki) natijasi eski ko‘rinishda. Chromium 360px: scroll yo‘q, xato yo‘q.
- Ochiq: "3-bo‘lim ochildi" kabi aniq raqam — API faqat `NextUnitId` beradi, shuning uchun "Keyingi bo‘lim ochildi".
- [x] **1h Mashq** (`/mashq`): sarlavha "Mashq" + "Ochilgan bo‘limlar so‘zlari bilan"; qatorlar `56px 1fr auto` (min 72px): 01 Yozma mashq · 02 Qaysi so‘z to‘g‘ri? · 03 Talaffuz (`/sinov/ogzaki`) · 04 Gap zinapoyasi · 05 Zaif joylar · 06 Xatolar daftari; Premiumsiz foydalanuvchida 03–06 da "Premium" tegi, bosilsa `/premium`. Mavjud boshqa mashqlar yo‘qolmasligi uchun "Yana" ostida: tez kartochka, gap tuzish, tuzoqlar daftari, sinovlar. 360px: scroll yo‘q, xato yo‘q.
- [x] **1i Premium** (`/premium`): header orqaga + "Premium"; kirish matni ("… bepul. Premium — …"); tariflar 2 ustun (tanlangan qora fon; ko‘p oylikda tejash foizi va oyiga narx, `GET /api/premium/plans` dan hisoblanadi); to‘lov kodi monospace 30px + "Nusxalash"; Bank/Hisob (sozlanmagan bo‘lsa "Tez orada"), egasi va summa; promo-kod input + "Tekshirish", natija accent-700 12px; rekvizit va kontakt bo‘lsa pastda "Chekni Telegramda yuborish →". 360px: scroll yo‘q, xato yo‘q.
- [x] **1a Kirish**: header "Lug‘at" + teg "A · B · C"; 2 ustun 하다 / 있다 (Noto 900/56px, ikkinchisi qizil) va ma’nolari; "Koreys tili so‘zlari — o‘zbek tilida." va izoh; "Telegram orqali kirish →" — o‘zimizning tugma, bosilganda Telegram’ning rasmiy oynasi (`Telegram.Login.auth`, `request_access: write`); skript oldindan yuklanadi, yuklanmasa eski vidjet ko‘rsatiladi. `GET /api/auth/config` endi `telegramBotId` (token boshidagi ochiq raqam) ham beradi; tokenning maxfiy qismi chiqmasligi testda tekshiriladi. Google tugmasi — Google’ning rasmiy tugmasi (uni o‘zgartirib bo‘lmaydi), to‘liq kenglikda. Pastda manba va maxfiylik havolasi.
- Chromium 360px: Telegram skripti o‘rnini bosuvchi stub bilan tugma bosildi → `bot_id: 123456`, `request_access: write` bilan chaqirildi va `POST /api/auth/telegram-login` yuborildi; scroll yo‘q, xato yo‘q. Haqiqiy Telegram oynasi production’da (koreyscha.com / BotFather domeni) tekshiriladi — Johnny.
- [x] **2a/2b Profil** (`/profil`, `/sozlamalar`): avatar (56px, bosh harflar), ism, rol teglari (Admin — accent; Muharrir, O‘qituvchi) yoki kirish usullari; admin/muharrir/o‘qituvchi uchun "Boshqaruv" (Admin panel yoki Kontent, O‘qituvchi paneli — guruh va o‘quvchi soni `api/teacher/groups` dan); Tarif ("Bepul" + Premium tugmasi / "Premium · N-oygacha"); O‘qish: Statistika, O‘qituvchi guruhi; Sozlamalar: Mavzu (Yorug‘/Qorong‘i/Qurilma, darhol saqlanadi), Lotincha o‘qilish (toggle), Eslatma (Telegram bo‘lsa, vaqt yoki "O‘chiq"); Ilova: o‘rnatish, hisobni bog‘lash, qurilmani tekshirish, haqida; pastda "Chiqish" va "Hisobni o‘chirish" (tasdiq bilan). Profilda tizim sozlamalari yo‘q.
- [x] Yangi kichik sahifalar: `/profil/eslatma` (eslatma vaqti, kunlar, sinov xabari), `/profil/hisob` (ism, kirish usullari, bog‘lash, guruhdan chiqish), `/profil/haqida`.
- [x] **2c Admin panel** (`/admin`): "Diqqat talab qiladi" — tekshiruvdagi bo‘limlar (qoralamasi bor, nashr qilinmagan) va to‘lanmagan komissiya; bo‘lmasa blok yo‘q. 6 katak: Kontent · Foydalanuvchilar · To‘lovlar · Promo va o‘qituvchilar · Ko‘rsatkichlar · Tizim.
- [x] **2d Admin → Tizim** (`/admin/tizim`): AI xizmati (Standart/Anthropic/OpenAI, sozlanmagani o‘chiq; har provayder holati L-34 bo‘yicha haqiqiy — "Ishlayapti · model · N ms" yoki "Ishlamayapti: sabab"; "Tekshirish"), oylik sarf ($ katta raqam, 6px progress, ogohlantirishda qizil), zaxira (har kecha 04:00 + "Hozir", Telegram haftalik + "Yuborish"), vaqt zonasi, sarf tafsiloti, qurilmani tekshirish.
- Chromium 360px: profil, admin, tizim — scroll yo‘q, xato yo‘q. Testlar 331 + JS 11 o‘tdi.
- Ochiq: dizayndagi "3 ta to‘lov kutmoqda" — to‘lovlar admin tomonidan qo‘lda qayd etiladi, kutayotgan to‘lovlar navbati yo‘q; o‘quvchining AI xizmatini tanlashi endi faqat admin sahifasida (dizayn bo‘yicha).

**Natijalar grafigi (2026-10-24):**
- [x] Johnny iPhone’da topdi: "Oldingi sinovlar" grafigida nuqtalar katta qora doira, chiziq qalin, 0/50/100 chiziqlari ko‘rinmas edi. Sabab — `ScoreChart` uslublari 0-bosqichda CSS’dan tushib qolgan. Uslublar qaytarildi; bir kunlik natijalarda sana ikki marta yozilmaydi. Chromium 390px: nuqtalar 4px, oxirgi qiymat yorlig‘i, sana bir marta.

**Bitta savolga bitta javob (L-37, 2026-10-24):**
- [x] Variantli bo‘sh joy savolida tarjima oldindan ko‘rinadi.
- [x] AI promptlari: zid ma’noli variant yo‘q; test bo‘sh joyi uchun har noto‘g‘ri variantga sabab, sababsizi almashtiriladi (test).
- [x] "Bu savolda xato bor" → admin `/admin/savollar`, "Diqqat talab qiladi"; ochiq xabarli saqlangan savol qayta berilmaydi (test: takror, begona test, admin ro‘yxati, hal qilish).
- [x] Bir xil ma’noli so‘zlar: kartochka va yozma mashqda farq izohi; boshqa so‘z yozilsa "ma’nosi to‘g‘ri", AI’siz (test: 안 / 아니다); test variantlarida chiqmaydi; Kontent sahifasida ogohlantirish.
- [ ] iPhone’da ko‘rib chiqish — Johnny.

**Bo‘lim testida yozish (L-38, 2026-10-24):**
- [x] `WordKoreanTyped` va `WordListeningTyped`; 25 so‘zda 11 ta yozma savol; imlo xatosi noto‘g‘ri; bir xil ma’noli so‘zda izoh (testlar: taqsimot, baholash, izoh, tinglashda matn yo‘q).
- [ ] iPhone’da koreyscha klaviatura bilan sinab ko‘rish — Johnny.

**Reklama kanallari (L-40, 2026-10-24):**
- [x] `?src=` havolasi: tashrif sanog‘i, yangi hisobga kanal, Admin → Ko‘rsatkichlar jadvali (test: normalizatsiya, birinchi kanal qoladi, eski hisob olinmaydi, D7, faqat admin).
- [ ] Kontent to‘plami (b) va tushuntirish sahifasi (c).
- [x] Kirishdan oldingi tushuntirish (L-41): to‘rt qadam, so‘z kartasi va tuzoq, bepul/Premium, o‘rnatish. Chromium 390px: gorizontal scroll yo‘q.
- [x] Pastki menyu (2026-10-25): belgi va yozuv katak o‘rtasida (telefonda chapga og‘ib ko‘rinardi); keng ekranda menyu va pastki "Davom etish" tugmasi sahifaning 640px ustuniga moslangan. Chromium 390px va 1440px.

**Haqiqiy qadam belgilari va AI tozalash (L-42, L-43, 2026-10-26):**
- [x] Jadval/Karta/Mashq/Test belgisi o‘z sharti bilan; `PracticedAt` yozma mashq va "Qaysi so‘z to‘g‘ri?" dan (test: 5 so‘zli bo‘limda 4 javobdan keyin yo‘q, 5-dan keyin bor).
- [x] AI javobidagi `</teg>`/`<parameter` qoldig‘i kesiladi, eski Gap zinapoyasi izohlari ham (testlar).

**Codex auditi bo‘yicha tuzatishlar (audit commit `64e1f0b`, 2026-10-26, branch `audit-tuzatish`):**
- [x] 1. Refresh xatosi: token faqat 401 da o‘chadi; 429/5xx/aloqa yo‘q — saqlanadi, "Qayta urinish", server tiklangach qayta kirishsiz (L-44). Test: `Pwa/SessionServiceTests` (eski kodda 3 tasi yiqildi).
- [x] 2. Chiqish: bu qurilma doim chiqadi; server tasdiqlamasa (5xx, aloqa yo‘q) kirish ekranida "boshqa qurilmalar bekor qilinmadi" (L-44). Test: o‘sha fayl (eski kodda 3 tasi yiqildi).
- [x] 3. "Bilaman" kunlik chegarasi: hisob va saqlash `unit-skip:{user}:{unit}` qulfi ostida (test qulfidan oldin olinadi, teskari tartib yo‘q). Test: 8 ta parallel turli id → 3 ta 200, qolgani 429, bir xil id qayta — o‘sha test, ertasi kuni ochiq (eski kodda 8 tasi ham o‘tdi).
- [x] 4. AI holati: davom etayotgan chaqiruv (`pending`) oxirgi natija sifatida olinmaydi; hech biri tugamagan bo‘lsa — "hali tekshirilmagan". Test: `A_call_still_running_does_not_replace_the_last_result_in_the_status` (eski kodda yiqildi).
- [x] 5. Vaqt zonasi: Seul/Toshkent tanlovi, serverda tekshiriladi, eslatma va "Bilaman" kuni unga bo‘ysunadi; admin statistikasi Koreya kunida (L-45). Testlar: Toshkent 20:00 eslatmasi, "Bilaman" chegarasi Toshkent yarim tunida, noto‘g‘ri zona 422. Chromium 390px: tanlov saqlandi, gorizontal scroll yo‘q.
- [x] 6. Ortiqcha so‘rovlar: guruh jadvali (xatolar barcha o‘quvchi uchun bitta so‘rovlar to‘plamida, oxirgi test faqat id bo‘yicha) va to‘lovlar ro‘yxati (obuna, ism, promo — 3 ta so‘rov). Testlar SQL buyruqlarini sanaydi: 2 va 6 o‘quvchi/to‘lovda teng (eski kodda 20 → 48 va 7 → 17).
- [x] 7. Zaxira muddati: diskda 30 kundan eski nusxa yo‘q (eng yangisi bundan mustasno), muvaffaqiyatsiz kechada ham tozalanadi; Telegram nusxalari uchun qo‘lda o‘chirish jarayoni va «O‘chirdim» (L-46). Testlar: `BackupRetentionTests`, `Copies_past_a_month_…`, `A_failed_night_…`. Mavjud fayllar o‘chirilmadi.
- [x] 7. Qaror: A (Johnny, 2026-10-26) — Telegram’dagi bir oydan eski nusxalarni Johnny qo‘lda o‘chiradi va Admin → Tizim’da «O‘chirdim» ni bosadi (L-46).
- [ ] Johnny: hozir chatda turgan eski nusxalarni (23-oktabrdan beri yuborilganlar) bir oy o‘tgach o‘chirish — ilova birinchi eslatmani keyingi haftalik izohda va Admin → Tizim’da beradi.
- [x] 8. README joriy holatga keltirildi: kod / avtomatik test / real muhit alohida, ochiq qabul tekshiruvlari ro‘yxati.
- Tekshirildi: `dotnet build` (0 ogohlantirish), testlar 106 (Domain) + 42 (Application) + 219 (Api) = 367, JS 11, `has-pending-model-changes` — o‘zgarish yo‘q. Migratsiya `BackupTelegramRemoved` (bitta ustun).
- Production dalili: kechki zaxiralar 23–25-oktabr `verified True` (Railway loglari).
- [ ] Ochiq (dalil yo‘q): hard spend limit; AI tariflari (`Ai__Budget__Prices__*` o‘zgaruvchilari yo‘q — AI faqat oylik son bilan cheklanadi); production nusxasidan to‘liq tiklash mashqi; real iPhone/Android oqimi, iPhone audio va koreyscha klaviatura, arzon Android/sekin internet; haqiqiy Telegram eslatmasi.

**Bitta Mashq sahifasi (L-47, 2026-10-26):**
- [x] Mashq: So‘zlar (4) · Gaplar (2) · Xatolarim (1). "Tez kartochka" va alohida "Sinov" menyusi olib tashlandi; `/sinov` → `/mashq`.
- [x] "Xatolarim" — Xatolar daftari, Zaif joylar va Tuzoqlar daftari sahifalari ustida umumiy tablar (`MistakesTabs`).
- [x] "Oldingi sinovlar" Profil → O‘qish’da; sinov sahifalarining "orqaga" havolalari Mashq/Profil’ga.
- [x] Chromium 390px, qorong‘i mavzu: Mashq, uch tab, Profil — gorizontal scroll yo‘q, sahifa xatosi yo‘q; `/sinov` Mashq’ga o‘tdi.
- [ ] iPhone’da ko‘rib chiqish — Johnny.

**O‘zbekistondan so‘mda to‘lov (L-48, 2026-10-27):**
- [x] Premium: ₩ / so‘m tanlovi, 29 000 / 69 000 so‘m, Humo karta; promo-kod so‘mda; admin to‘lovni valyuta bilan qayd etadi.
- [x] Komissiya va tushum valyuta bo‘yicha alohida; "To‘landi" valyuta bo‘yicha (test topgan xato tuzatildi).
- [x] Testlar: so‘m narxlari va karta, promo, so‘mdagi to‘lov va komissiya, noto‘g‘ri valyuta 400, won/so‘m bir oyda alohida. Chromium 390px: Premium so‘mda, gorizontal scroll yo‘q.
- [x] Railway: `Payments__Uz__*` (Johnny bergan TBC Humo karta).
- [ ] Johnny: telefonda Premium → "O‘zbekistondan (so‘m)" ni ko‘rish; birinchi haqiqiy so‘m to‘lovini admin’da "so‘m" bilan qayd etish.

**Sinovda bitta "Keyingisi" (2026-10-28):**
- [x] Talaffuz savoliga javobdan keyin ikkita tugma chiqardi ("Keyingi →" va "Keyingisi"). Ichkisi olib tashlandi; pastdagi "Keyingisi" javob berilgan savoldan keyingi javobsiz savolga o‘tadi, oxirgi savolda ham javobsiz qolgan bo‘lsa unga qaytaradi.
- [ ] Johnny: telefonda Talaffuz sinovida tekshirish (bu konteynerda mikrofon va Azure yo‘q).

**Talaffuz faqat gapirish, Enter bilan ishlash (L-49, 2026-10-28):**
- [x] Talaffuz sinovi: hamma savol "o‘zbekcha ma’no → koreyschasini ayting" (Azure baholaydi); "Bildim/Bilmadim" savoli yo‘q. Test yangilandi.
- [x] Enter asosiy tugmani bosadi (`data-enter`); maydon va Tab bilan tanlangan tugma o‘z Enter’ini saqlaydi; JS testlari (`tests/web/enter.test.mjs`).
- [x] Chromium, faqat klaviatura: yozma mashq (yozish → Enter tekshiradi → Enter keyingisi, kursor maydonda); lug‘at sinovi (yozma savollar Enter bilan, variantli savolda Enter javobsiz savolni tashlab ketmaydi).
- [ ] Johnny: noutbukda Talaffuz va yozma mashqni sichqonchasiz sinab ko‘rish.

**O‘rnatish yo‘riqnomasi (L-50, 2026-10-28):**
- [x] Instagram/Facebook/Telegram/TikTok ichki brauzeri va noma’lum WebView aniqlanadi: ogohlantirish, 3 qadam, "Havolani nusxalash", Android’da "Chrome’da ochish".
- [x] iPhone’dagi Chrome/Firefox/Edge va Samsung Internet uchun alohida qadamlar.
- [x] Kirish sahifasida Premium narxi so‘mda ham.
- [x] JS testlari (16): har bir brauzer holati. Chromium 390px (Instagram iOS, Facebook Android, Safari UA): gorizontal scroll yo‘q, sahifa xatosi yo‘q.
- [ ] Johnny: haqiqiy Instagram/Telegram havolasidan ochib ko‘rish (iPhone va Android).

**Nashr qilingan bo‘limlar tugaganda (2026-10-28):**
- [x] Barcha nashr qilingan bo‘limlarni o‘tgan o‘quvchiga Bosh sahifa "Hali ochiq bo‘lim yo‘q" o‘rniga "Hozircha barcha bo‘limlar tugadi … yangi bo‘lim shu yerda o‘zi ochiladi" deydi (`TodayDto.PassedUnits`). Test: 1-bo‘lim o‘tildi → bo‘lim yo‘q, PassedUnits 1; 2-bo‘lim nashr qilindi → u Bosh sahifada.


**Mening so‘zlarim (L-51, 2026-10-28):**
- [x] Qidiruv: lug‘at (koreyscha aynan, o‘zbekcha to‘liq ma’no) → AI; AI kartasi lug‘at qoidalari bilan tekshiriladi.
- [x] Bepulga kuniga 3 ta AI so‘z, Premium cheksiz, lug‘at cheksiz; muvaffaqiyatsiz AI chaqiruvi hisoblanmaydi.
- [x] Qo‘shish idempotent; bir so‘z ikki marta qo‘shilmaydi (409); boshqa o‘quvchining to‘plami/so‘zi 404.
- [x] Kunlik takrorga kuniga 10 ta yangi o‘z so‘zi; 11-si ertaga. Tuzoq kartada.
- [x] To‘plamlar: yaratish, nom takrori 409, ko‘chirish, nomini o‘zgartirish, o‘chirishda so‘zlar "Umumiy"ga. Migratsiya `PersonalWords` (faqat yangi jadvallar).
- [x] Testlar: `MyWordTests` (12). Chromium 390/1280, yorug‘/qorong‘i: gorizontal scroll yo‘q, sahifa xatosi yo‘q.
- [x] Mashqlarda: Aralash sinov, Talaffuz va Yozma mashqda to‘plam tanlovi; bo‘lim ro‘yxati o‘zgarmaydi; tanlov bo‘sh bo‘lsa 422. Test: `Exercises_can_be_built_from_own_words_by_deck…`. Chromium 390px: uch sahifa, scroll yo‘q, Yozma mashq "Ish" to‘plamidan so‘z berdi.
- [x] "So‘z qo‘llanishi" to‘plamlar uchun: tayyorlash, mashq, boshqa o‘quvchi uchun 404. Test: `Which_word_is_right_can_be_prepared_and_practised_for_a_deck_of_own_words`. Chromium 390px: sahifada "Umumiy" va to‘plam qatorlari, scroll yo‘q.
- [x] Yozma mashq sukut bo‘yicha "Koreyscha yozish" (PR #45).
- [ ] Johnny: telefonda bitta lug‘at so‘zi va bitta AI so‘zi qo‘shib, ertasi kungi takrorda chiqishini ko‘rish.

**Tuzoq sahnasi (L-52, 2026-10-28):**
- [x] Model va migratsiya `TrapScenes` (faqat yangi ustunlar), qoidalar `CheckScene`.
- [x] /kontent: sahna maydonlari, "Tuzoq sahnalari" (AI, 10 tadan), "Sahnani shundayicha tasdiqlash"; tasdiqlanmagan sahna o‘quvchiga ko‘rinmaydi.
- [x] Kartochka: vaziyat → xato ✗ (chiziladi) → to‘g‘ri ✓ + tinglash → izoh; "Qayta ko‘rsatish"; reduced-motion.
- [x] Mening so‘zlarim: AI so‘zining sahnasi, lug‘at so‘zining tasdiqlangan sahnasi.
- [x] Testlar: `TrapSceneTests` (10). Chromium 390px yorug‘/qorong‘i: kartochka va /kontent, scroll yo‘q, sahifa xatosi yo‘q.
- [ ] Johnny: production’da /kontent → A-1 bo‘lim → "Tuzoq sahnalari", natijani o‘qib tasdiqlash; keyin telefonda kartochkani ko‘rish.


**Qayta audit tuzatishlari (L-53, 2026-10-29):**
- [x] Premium: kechikkan boshqa valyuta/tarif promo javobi summani almashtirmaydi; hisob ketayotganda eski to‘lov summasi ko‘rsatilmaydi.
- [x] Yaroqsiz AI shaxsiy kartasi bepul kvotadan ketmaydi, provayder tokenlari va xarajati saqlanadi.
- [x] Katalogdan o‘chirilgan yoki pending sahna shaxsiy nusxadan qaytmaydi.
- [x] Bo‘sh/yaroqsiz sahna javobini qayta so‘rash mumkin; aniq null javob va yetishmagan yozuv ajratildi.
- [x] Mening so‘zlarim: koreyscha IME tasdig‘i va modifier Enter qidiruv yubormaydi.
- [x] Tanaffusdan so‘ng kunlik takrorga o‘z so‘zlaridan ko‘pi bilan 10 ta yangi so‘z; boshlangan va muddati kelgan kartalar saqlanadi.
- [x] Release build (0 ogohlantirish/xato), audit regressiyalari (9), JS (16), model/migratsiya mosligi tekshirildi. Yakuniy to‘liq tekshiruv — PR Checks’dagi CI. Mahalliy matn nusxasida ikonka fayllari bo‘lmagani uchun PWA statik fayl tekshiruvi asl GitHub checkout’ida bajariladi.
- CI/deploy dalili: PR Checks va aynan merge commit’iga bog‘langan Railway deployment statusi; health va PWA asset versiyasi bilan tekshiriladi.

**Bo‘lim bilan ovozli tanishuv (L-54, 2026-10-29):**
- [x] Johnny ma’qullagan Jonli doska dizayni: qog‘oz kataklar, binafsha rang, qo‘lyozma izohlar, Bomi, so‘z va doska qismlari ketma-ket ochilishi. Shriftlar litsenziyasi bilan lokal saqlanadi.
- [x] Alohida versiya va additive `UnitIntroductions` migratsiyasi; manba nusxasi/xeshi, xmin, eski nashrni atomar almashtirish. Bo‘sh draft bo‘lim qayta bo‘lishda o‘chirilsa, uning tanishtirish qoralamasi ham o‘chadi; regressiya tekshirildi. Nashrdagi dars tahrirlanmaydi.
- [x] Faqat Admin: /kontent → bo‘lim → «Tanishtirish darsi». AI 3 so‘zdan qoralama; inson har so‘zni tekshiradi; ssenariy tasdiqlanmasdan ovoz yo‘q; tayyor darsni eshitish tasdig‘isiz nashr yo‘q. Amallar auditda.
- [x] Har qadam alohida to‘liq MP3, ElevenLabs o‘zbekcha / mavjud TTS koreyscha. Profil+matn bo‘yicha persistent kesh, umumiy xarajat/limit nazorati. O‘quvchi generatsiya qilmaydi; faqat nashrdagi tasdiqlangan yozuvlarni oladi.
- [x] Ijro audio `ended` bilan davom etadi. Tarmoq, dekodlash va autoplay xatosida to‘xtaydi. Bitta audio element, keyingi yozuvni oldindan yuklash, pauza/tezlik/qayta eshitish, tuzoq filtri, skip, foydalanuvchi+dars bo‘yicha qurilmada qolgan joy.
- [x] Ochiq bo‘limga birinchi kirish → mavjud nashrdagi tanishtirish; `qadam=1` → jadval. Dars yo‘q bo‘lsa eski oqim. Ko‘rish/skip test natijasi va kartochka takrorlash jadvalini o‘zgartirmaydi.
- [x] Lokal Release build va model/migratsiya mosligi. To‘liq lokal .NET: 407 passed, 1 skipped (`pg_dump` lokal SDK’da yo‘q); yakuniy 8 ta yangi regressiya/adapter tekshiruvi alohida 8/8 o‘tdi. JS: 20 passed, jumladan ovozni kesmaslik, pauza, eski ended hodisasi, xato va autoplay rad etilishi.
- [x] Haqiqiy Blazor + lokal PostgreSQL bilan 25 so‘zli QA: birinchi kirish, pauza, audio tugagach doska ochilishi, skip esda qolishi, admin preview, qadam qo‘shish/olib tashlash va tahrirni tekshirib saqlash; 360px/1280px’da sahifa xatosi va yonlama scroll yo‘q. Audio bu tekshiruvda **jim QA fayli**, real ElevenLabs/Azure sifat sinovi emas.
- [x] Railway app’da `ElevenLabs__ApiKey`, `ElevenLabs__VoiceId`, `ElevenLabs__Model` Studio servisiga reference sifatida yozildi; deploy o‘tkazib yuborilib, kod nashri bilan birga kuchga kiradi. Kalit qiymatlari o‘qilmadi/nusxalanmadi.
- CI/deploy dalili: PR #49 Checks va aynan merge commit’iga bog‘langan Railway deployment; /health va yangi PWA assetlari/xeshi bilan tekshiriladi.
- [ ] Johnny/admin: haqiqiy 1-bo‘lim ssenariysini yaratish, tahrir/tasdiqlash, ElevenLabs/Azure ovozini eshitib tekshirish va xatosiz bo‘lgach nashr qilish. Haqiqiy iPhone Safari/PWA’da eshitish sinovi.

**Tanishtirishning birinchi bosishda ovoz boshlashi (2026-10-29):**
- [x] iOS uchun bitta audio element avval mavjud `audio.js` dagi haqiqiy 20ms jim PCM yozuvi bilan tayyorlanadi; bo‘sh (0 sample) WAV ishlatilmaydi. JS regressiyasi yozuvning RIFF/WAVE va sample uzunligini tekshiradi; JS jami 21/21.
- [ ] Haqiqiy iPhone Safari/PWA’da birinchi bosishni tekshirish — adminning real ovozli dars qabul sinovi bilan birga.

- [x] Tanishtirishdagi lotincha o‘qilish ham mavjud profilning «Lotincha o‘qilishini ko‘rsatish» sozlamasiga bo‘ysunadi.

**Sahnalarni tekshirish (L-52 qo‘shimcha, 2026-10-29):**
- [x] Panel (4 son), "Tekshirish" ro‘yxati (Tasdiqlash / Tahrirlash / Olib tashlash, Hammasini tasdiqlash), so‘z kartasida sahna holati, "Topilmaganlarni qayta so‘rash".
- [x] Test `The_review_list_confirms_all_drops_one_and_words_without_a_scene_can_be_asked_again`. Chromium 390px: panel va ro‘yxat, scroll yo‘q.
- [ ] Johnny: 2-bo‘limdan boshlab har bo‘limda "Topilmaganlarni qayta so‘rash" → "Tekshirish" → tasdiqlash.

**Krossvord (L-55, 2026-10-31):**
- [x] Mashq → 05 Krossvord; 2 bo‘lim ochilganda ochiladi, 1 bo‘limda qulf va tushuntirish. Kichik/O‘rta 2 bo‘lim, Katta 3 bo‘lim.
- [x] Server generator, javob/ishora serverda; ochiq so‘zning koreyschasi klientga chiqmaydi; davom ettirish; ochib berilgan so‘z Xatolar daftarida.
- [x] 3 API testi; dev bazada Chromium 390px’da boshidan oxirigacha o‘ynaldi (qulf, davom ettirish, yozish, ochish, yakun).

**Ochiq so‘z sahifalari — SEO (L-56, 2026-11-01):**
- [x] `/soz/{slug}`, `/soz` (sahifalangan), `/sitemap.xml`, `/robots.txt` — server HTML, auth’siz, fallback’dan oldin; `Seo:BaseUrl` canonical uchun.
- [x] Faqat Reviewed + Published so‘zlar; Draft so‘z, Draft bo‘lim, noma’lum slug → HTML 404 (`noindex`).
- [x] `word_slugs` + migratsiya: barqaror slug, to‘qnashuvda `-{homonym}`/`-{so‘z turi}`/raqam; romanizatsiya tahrirlansa URL o‘zgarmaydi (test).
- [x] title, description, canonical, og:*, JSON-LD `DefinedTerm`; Hangul va `< > & "` encode (test); tashqi JS yo‘q.
- [x] Service worker bu manzillarga tegmaydi (JS testi). `index.html` boot’da tushuntirish va `/soz` havolasi.
- [x] 6 ta API testi; dev ilovada Chromium 390px (yorug‘/qorong‘i): so‘z sahifasi, ro‘yxat, yonlama scroll yo‘q; `curl` bilan robots/sitemap.
- [ ] Johnny (deploydan keyin): Google Search Console va Yandex Webmaster’ga koreyscha.com, sitemap yuborish; Cloudflare managed robots.txt bizning faylni bosmasligini tekshirish; Telegram’da `/soz/...` preview’ini tekshirish.
- [ ] Indekslanish natijasi (`site:koreyscha.com`) 1–2 haftadan keyin tekshiriladi.

**Kirishsiz sinov sahifasi (L-58, 2026-11-02):**
- [x] `/sinab` — server HTML (Blazor emas), auth’siz, fallback’dan oldin; `/soz` bilan bir layout va Sokin/Tungi ranglari; sitemap’da; robots’da ochiq; service worker tegmaydi (JS testi).
- [x] A-1 nashr qilingan tanishuvidan faqat 2 so‘z (`IntroductionService.TasterWords`); ovoz `/api/sinab/audio/...` dan, faqat saqlangan fayl — kalit yo‘q bo‘lsa 404, generatsiya yo‘q (test: diktor chaqirilmaydi); 3-so‘z, boshqa qadam/qism, nashr qilinmagan tanishuv → 404.
- [x] 3 ta tuzoq savoli: A-1, Reviewed + Published, `TrapWrong`/`TrapRight`/`TrapUz` bor, `TrapScenePending == false`; variantlar aralash; javobdan keyin matn bilan «to‘g‘ri»/«tuzoq», izoh va to‘g‘ri gap; oxirida «N tadan M tasini topdingiz». Hech narsa saqlanmaydi.
- [x] Manba: `/sinab?src=ig` → `sinab-ig` (`SourceService.Normalize`), `koreyscha.src` bo‘sh bo‘lsa yoziladi va bitta `/api/visits`; CTA `/?src=sinab-ig`. `/soz` sahifasida «Kirmasdan 1 daqiqada sinab ko‘rish» → `/sinab?src=soz`.
- [x] Rate limit `sinab` (IP, 120/daq), `Cache-Control: public, max-age=3600`.
- [x] 3 API testi + `/soz` testi kengaytirildi; 5 JS testi (pleer `ended` bilan, yuklanmasa to‘xtaydi, kliplar tartibi, tuzoq bahosi, manba bir marta).
- [x] Dev ilovada (mahalliy sinov tanishuvi va tuzoqlar bilan, keyin o‘chirildi) Chromium 390px yorug‘ va 360px qorong‘i: birinchi bosishda ovoz boshlandi, ikki qismli qadam birlashtirilib o‘ynaldi, oxirigacha bordi; tuzoqlar va hisob; `koreyscha.src = sinab-ig`, `/api/visits` bir marta (qayta yuklashda yo‘q); yonlama scroll yo‘q, konsol xatosi yo‘q.
- [ ] Johnny: haqiqiy iPhone Safari’da ovozni tekshirish (ayniqsa birinchi bosish va ikki qismli qadam).
- [ ] Johnny: Instagram bio havolasini `koreyscha.com/sinab?src=ig` ga almashtirish.
- [ ] Johnny: Kontent → A-1’da kamida 3 ta so‘zning tuzoq sahnasi tekshirilganini ko‘rish (kam bo‘lsa borlari chiqadi).
- [ ] 2 haftadan keyin: Admin → Ko‘rsatkichlar → Manbalar’da `sinab-ig`, `sinab-soz`, `soz` qatorlarining konversiyasini solishtirish.

**«Bo‘lim tugadi» ulashish kartasi (L-59, 2026-11-03):**
- [x] O‘tilgan bo‘lim testi natijasida «Do‘stga ulashish» (ikkinchi darajali tugma, kichik karta ko‘rinishi bilan); «Bilaman», o‘tilmagan test va to‘g‘ri javobsiz testda yo‘q.
- [x] Server: ko‘pi bilan 3 ta to‘g‘ri javob berilgan so‘z, eng ko‘p ishlatiladigani birinchi; GET ham shu kartani qaytaradi (2 API testi).
- [x] 1080×1350 PNG: Sokin va Tungi, ㅋ logotip, haqiqiy ball; havola `koreyscha.com/sinab?src=ulash` → `sinab-ulash` (test).
- [x] Fayl ulashish → matn ulashish → Telegram havolasi; bekor qilish jim, xato xabari (6 JS testi). Chromium 360px yorug‘/qorong‘i: tugma 48px, yonlama scroll yo‘q.
- [ ] Johnny: haqiqiy iPhone va Android’da ulashish (Telegram/Instagram’ga rasm bilan borishi).
- [ ] 2 haftadan keyin: Admin → Manbalar’da `sinab-ulash` qatori.
