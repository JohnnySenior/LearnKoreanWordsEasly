# Lug‘at — o‘zbeklar uchun koreys tili lug‘ati (PWA)

**Mahsulot va texnik talablar · v1.0 · 2026-10-22 · Buyurtmachi: Johnny (Jamshidbek)**

Ushbu hujjat C# asosida kod yozadigan AI agentlar (Claude Code, Codex) va dasturchilar uchun yagona ish hujjati. Agent suhbatni o‘qimasdan, faqat shu hujjat va havola qilingan repo orqali ishni to‘liq tushunib, kod yoza olishi kerak.

> **Ishchi nom:** "Lug‘at". Yakuniy brend keyinroq tanlanadi. Kodda `Lugat` prefiksi ishlatiladi (`Lugat.Domain`, `Lugat.Api` …). Nomni o‘zingiz o‘ylab topmang (AG01).

---

## 0. Agent uchun eng muhim 10 qoida

1. **Asos — KoreanAiTeacher.** Bu yangi mahsulot Johnnyning mavjud, real ishlab turgan ilovasi asosida quriladi:
   **https://github.com/JohnnySenior/KoreanAiTeacher**
   Ishni boshlashdan oldin shu repodagi `README.md`, `AGENTS.md`, `docs/SPECIFICATION.md`, `docs/DECISIONS.md` (D-01…D-54) va `docs/CHECKLIST.md` ni to‘liq o‘qing. Arxitektura, kod uslubi, nomlash, test usuli va UX qoidalari o‘sha repodagidek bo‘ladi. Shubha bo‘lsa — KoreanAiTeacher qanday qilgan bo‘lsa, shunday qiling.
2. **KoreanAiTeacher repo’siga hech narsa yozmang.** U faqat o‘qish uchun manba. Barcha ish yangi repoda bo‘ladi: **https://github.com/JohnnySenior/LearnKoreanWordsEasly** (§13.1).
3. **Maqsaddan og‘ishmang.** Bu — umumiy (ommaviy) foydalanuvchilar uchun **lug‘at yodlash** mahsuloti. Grammatika kitobi, PDF import, TOPIK insho, kitob testlari, dars kalendari v1 da **yo‘q** (§3). Ularni “foydali bo‘lar” deb qaytarmang.
4. **Talab maqomlari** KoreanAiTeacher’dagidek: **KELISHILGAN** (Johnny tasdiqlagan — o‘zgartirmang), **TEXNIK ASOS** (taklif; asos bilan o‘zgartirsa bo‘ladi, `docs/DECISIONS.md` ga yozing), **TEKSHIRILADI** (real sinovsiz va’da qilmang), **JOHNNY TASDIQLAYDI** (standart qiymat bilan qiling, lekin ochiq savol sifatida qayd eting).
5. **C# va .NET 10** (KELISHILGAN). Backend ASP.NET Core, frontend Blazor WebAssembly. Tilni yoki frameworkni almashtirmang.
6. **Barcha mantiq serverda.** Takrorlash, bo‘lim qulfi, test baholash, premium tekshiruvi — backend’da. Klient faqat ko‘rsatadi. Sabab: keyinchalik Meta ko‘zoynagi (Web App) va boshqa klientlar shu API’dan foydalanadi (§14).
7. **Interfeys o‘zbekcha (lotin)**, koreyscha Hangul bilan. API xatolari `ProblemDetails`, `detail` o‘zbekcha.
8. **Mock’ni real deb ko‘rsatmang.** Har bosqichda nima qilindi, nima tekshirildi, nima ishlamayapti — `docs/CHECKLIST.md` ga.
9. **Maxfiy qiymatlar** (bot tokeni, AI/Azure kalitlari, Google client secret) repo, log va brauzerga tushmaydi. Nomlari `.env.example` da.
10. **Kichik, tekshiriladigan qadamlar.** Har bosqich (§13) oxirida `dotnet build` va `dotnet test` yashil, CI yashil, checklist yangilangan bo‘lsin. Bitta ulkan PR emas.

---

## 1. Mahsulot nima va kim uchun

### 1.1 Bir jumlada
Koreyadagi va Koreyaga ketayotgan o‘zbeklar uchun koreys tili so‘zlarini **o‘zbek tilida**, eng ko‘p ishlatiladigan so‘zlardan boshlab, **25 talik bo‘limlarda ketma-ket** va **haqiqatan yodlatib** o‘rgatadigan PWA.

### 1.2 Foydalanuvchilar (KELISHILGAN)
- **Asosiy:** Koreyada ishlayotgan o‘zbeklar (ko‘pchilik E-9 vizada, zavod/qurilish/qishloq xo‘jaligi), koreys tilini boshlang‘ich darajada biladi yoki umuman bilmaydi.
- **Ikkinchi:** EPS-TOPIK / TOPIK ga tayyorlanayotgan o‘zbeklar.
- **Tarqatish kanali:** Koreyada yashovchi **o‘zbek koreys tili o‘qituvchilari** (Telegram kurslari). Ular o‘quvchilariga ilovani tavsiya qiladi (§9).

### 1.3 Raqobat va farq (kontekst, agent uchun)
Bozorda **kibo.uz** bor: bepul, o‘zbekcha, Google kirish, 30 talik bo‘limlar, ketma-ket ochilish, reyting. Uning zaif joylari — bizning farqimiz:

| Kibo | Bizda (KELISHILGAN) |
|---|---|
| "Keyingi" tugmasi = "yodladim" deb hisoblanadi | Bo‘lim faqat **yakuniy testdan ≥80%** bilan tugaydi |
| Unutilgan so‘z qachon qaytishi noma’lum | Takrorlash jadvali (`ladder-v1`) va keyingi vaqt ko‘rsatiladi |
| Faqat KO→UZ | Ikki yo‘nalish (KO→UZ, UZ→KO), aralash tartib |
| Mashq yo‘q | Jadval (yopib yodlash), kartochka, yozma mashq, "Qaysi so‘z to‘g‘ri?", tinglash |
| Tushuntirish yo‘q | **O‘zbekcha tuzoq** izohlari (o‘zbeklar adashadigan joylar) |
| Talaffuz yo‘q | Azure TTS (eshitish) va talaffuz baholash |
| Tijoriy kitob ro‘yxatlari (쏙쏙 TOPIK) | Rasmiy, ochiq litsenziyali 국립국어원 ro‘yxati, **chastota tartibida** |

### 1.4 Tamoyillar (KoreanAiTeacher spec UX01 dan, KELISHILGAN)
Sokin va sodda. Majburiy streak, jazo, konfetti, **reyting/leaderboard yo‘q** (foydalanuvchilarni bir-biri bilan musobaqalashtirmang). Rang yagona belgi emas. 360 px kenglikda yonlama scroll yo‘q. Tegish nishoni ≥ 44 px.

---

## 2. Kontent: so‘zlar qayerdan va qanday

### 2.1 Manba (KELISHILGAN)
**국립국어원 「한국어 학습용 어휘 목록」 (2003)** — 5,965 so‘z:
- A (boshlang‘ich) 982 · B (o‘rta) 2,111 · C (yuqori) 2,872.
- Har yozuvda: so‘z, omonim raqami (masalan 가다**01**), so‘z turkumi (품사), qisqa izoh/hanja (풀이), chastota o‘rni (순위), daraja (등급).
- Litsenziya: **KOGL 1-tur** (공공누리 제1유형) — manba ko‘rsatilsa tijoriy foydalanish mumkin. Ilovada "Manba: 국립국어원 한국어 학습용 어휘 목록" **albatta ko‘rsatiladi** (Sozlamalar → Haqida va har bir so‘z kartasining "Manba" qismida).
- Rasmiy yuklab olish sahifasi: https://www.korean.go.kr/front_eng/down/down_02V.do?etc_seq=70 (Excel versiyasi bor — import uchun shuni ishlating).

**Taqiqlangan (KELISHILGAN):** tijoriy kitoblardan (쏙쏙 TOPIK, 서울대/연세 darsliklari va h.k.) so‘z ro‘yxati, tarjima, misol yoki dialog ko‘chirish; kibo.uz kontentini ko‘chirish. EPS-TOPIK darsligidan faqat **mavzu va so‘z nomlari** olinishi mumkin (v1.1 "Ish joyi" yo‘nalishi uchun), matnlar o‘zimizniki bo‘ladi.

### 2.2 Bo‘limlarga taqsimlash (KELISHILGAN)
- Daraja ichida so‘zlar **chastota o‘rni (순위) bo‘yicha o‘sish tartibida** — eng ko‘p ishlatiladigan birinchi.
- **Har bo‘lim 25 so‘z.** A daraja ≈ 33 bo‘lim (aniq son filtrlashdan keyin chiqadi; oxirgi bo‘lim 25 dan kam bo‘lishi mumkin — kamida 10 bo‘lsin, aks holda oldingisiga qo‘shiladi).
- Kartochkaga **mos bo‘lmagan** turkumlar bo‘limlarga kirmaydi: `보` (yordamchi fe’l/sifat), `의` (qaram ot), `관` (aniqlovchi). Ular bazada saqlanadi, lekin `IsStudyable = false` (keyinchalik grammatika bo‘limi uchun).
- Bir xil yozilgan omonimlar (가다01 fe’l va 가다01 보) — alohida yozuv, alohida ma’no izohi bilan. Bitta bo‘limda bir xil yozilgan ikki so‘z bo‘lsa, kartada ma’no konteksti albatta ko‘rsatiladi (KoreanAiTeacher spec PR07).
- Bo‘lim raqamlari **barqaror**: kontent nashr qilingandan keyin so‘z boshqa bo‘limga ko‘chirilmaydi (foydalanuvchi progressi buzilmasin). Tuzatish faqat matnda (`ContentVersion` oshadi, statistika nollanmaydi — KoreanAiTeacher DATA02, D-13).

### 2.3 Har bir so‘zning maydonlari (TEXNIK ASOS)
Namuna fayl: `content/samples/lugat_A_namuna_50.json` (Johnny beradi; A darajaning chastota bo‘yicha birinchi 50 so‘zi = 1- va 2-bo‘lim). Format:

```json
{
  "id": "A-001",
  "word": "하다",
  "homonym": "01",
  "pos": "fe'l",
  "level": "A",
  "freq_rank": 2,
  "translation_uz": "qilmoq",
  "romanization": "hada",
  "example_ko": "오늘 청소를 해요.",
  "example_uz": "Bugun tozalayman.",
  "trap_uz": "Yaxshi xabar: o'zbekchadagi \"ot + qilmoq\" kabi ishlaydi: 공부하다 (o'qish qilmoq), 전화하다 (telefon qilmoq).",
  "theme": "Asosiy fe'l",
  "source": "국립국어원 한국어 학습용 어휘 목록 (KOGL 1)"
}
```

| Maydon | Qoida |
|---|---|
| `word`, `homonym`, `pos`, `level`, `freq_rank` | Faqat rasmiy ro‘yxatdan. AI o‘zgartirmaydi. |
| `translation_uz` | Tabiiy o‘zbekcha (lotin), 1–3 ma’no `;` bilan. Kartadagi asosiy javob — birinchi ma’no; qolganlari maqbul variant (KoreanAiTeacher PR06: sinonim xato emas). |
| `romanization` | Revised Romanization (국립국어원 qoidasi). Kartada ixtiyoriy ko‘rsatiladi (Sozlamalar: "Lotincha o‘qilishini ko‘rsatish", standart **yoqiq** — boshlang‘ichlar uchun). |
| `example_ko` / `example_uz` | 1 ta misol, `요`-shaklda, ≤ 8 어절, imkon qadar shu va oldingi bo‘limlardagi so‘zlardan. Hayotiy (ish, do‘kon, kasalxona, uy). |
| `trap_uz` | O‘zbek tilida so‘zlashuvchi adashadigan **haqiqiy** joy (so‘zma-so‘z tarjima, xushmuomalalik darajasi, omonim, o‘xshash so‘z). Haqiqiy tuzoq bo‘lmasa — `null` (o‘ylab topilmaydi, KoreanAiTeacher D-46). O‘xshashlik ham foydali ("o‘zbekchadagi -ib bering kabi"). |
| `theme` | Qisqa mavzu teg (Asosiy fe’l, Olmosh, Vaqt, Joy, Odamlar, Ish, Tana, Muloqot …). |
| `source` | Doim to‘ldiriladi. |

O‘zbek apostrofi: saqlashda bitta kanonik shakl (KoreanAiTeacher qaysi belgini ishlatgan bo‘lsa — o‘sha), solishtirishda `TextNormalizer.Key` (barcha apostrof variantlari teng).

### 2.4 Kontent tayyorlash jarayoni (TEXNIK ASOS)
1. **Import:** admin rasmiy Excel faylni yuklaydi → barcha 5,965 yozuv `CatalogWord` sifatida `Draft` holatida saqlanadi (faqat rasmiy maydonlar). Qayta import idempotent (so‘z + omonim + turkum + daraja bo‘yicha kalit).
2. **Bo‘limlarga bo‘lish:** §2.2 qoidasi bilan `Unit` va `UnitNumber/OrderInUnit` beriladi.
3. **AI qoralama:** admin "Bo‘lim uchun AI qoralama" tugmasini bosadi → AI 10 tadan so‘z bilan (KoreanAiTeacher D-46 dagi `WordsPerCall` usuli) `translation_uz`, `romanization`, `example_*`, `trap_uz`, `theme`, hamda 1–2 ta "Qaysi so‘z to‘g‘ri?" savolini yozadi. Server javobni tekshiradi (Hangul/lotin, bitta `___`, variantlar farqli…), yaroqsizini tashlaydi.
4. **Inson tekshiruvi (majburiy):** admin yoki muharrir (o‘qituvchi) har so‘zni ko‘radi, tahrirlaydi, `Reviewed` qiladi. Kim tekshirgani saqlanadi (`ReviewedBy`, ixtiyoriy ommaviy "Muharrir: ..." yozuvi).
5. **Nashr:** bo‘limdagi **barcha** so‘zlar `Reviewed` bo‘lgandagina bo‘lim `Published` bo‘ladi va foydalanuvchilarga ko‘rinadi. Nashr qilinmagan bo‘lim ro‘yxatda "Tez orada" deb ko‘rinadi.
6. **Seed:** ishga tushirishda 50 so‘zli namuna fayldan 1–2-bo‘limlar import qilinadi (dev va test muhitida). Production’da admin import qiladi.

Kontent **umumiy** (barcha foydalanuvchilar uchun bitta) — AI har foydalanuvchi uchun alohida chaqirilmaydi (KoreanAiTeacher BUD04).

---

## 3. V1 chegarasi

### 3.1 KoreanAiTeacher’dan nima olinadi va nima olinmaydi (KELISHILGAN)

| KoreanAiTeacher funksiyasi | V1 da | Izoh |
|---|---|---|
| `ladder-v1` takrorlash (D-05), Card/ReviewEvent, idempotent javob, undo (D-14) | **Olinadi** | O‘zgarishsiz. Kartalar endi katalog so‘ziga bog‘lanadi (§5). |
| Sessiya navbati (PLAN08, D-14), teskari savol aralash (D-45) | **Olinadi** | Yangi so‘zlar faqat ochiq va hali tugallanmagan bo‘limdan. |
| Jadvalni yodlash, ustunni yopish (D-36) | **Olinadi** | Bo‘lim sahifasining 1-qadami. |
| Kartochka (UX03), uchta baho, keyingi vaqt | **Olinadi** | |
| Yozma mashq (D-23) | **Olinadi** | Bepul. |
| "Qaysi so‘z to‘g‘ri?" (D-46, `WordUsage`) | **Olinadi** | Kontent katalog darajasida (umumiy), bepul. |
| Sinov tizimi (D-30, TEST01–04) | **Olinadi** | Bo‘lim yakuniy testi shu dvigatelda (§4.4). |
| Xatolar daftari (D-41) | **Olinadi** | **Premium.** |
| Natijalar (UX08) | **Olinadi** | "Statistika" sahifasi. Reyting yo‘q. |
| TTS: Azure (D-29, D-31), kesh | **Olinadi** | Bepul. Audio katalog so‘zi uchun umumiy kesh. |
| Talaffuz baholash: Azure (D-32) | **Olinadi** | **Premium.** |
| Gap zinapoyasi (D-47), gap tekshiruvi (D-52), Tuzoqlar daftari, Zaif joylar (D-54) | **Olinadi** | **Premium** (har chaqiruv AI xarajati). |
| Tinglash (D-53) | **v1.1** | Kod ko‘chiriladi, lekin menyuda yashirin (`Features:Listening=false`). |
| AI sarf nazorati, kunlik limit (D-50, BUD03–05) | **Olinadi** | Limit tarif bo‘yicha (§7.4). |
| Telegram eslatmalari (D-16) | **Olinadi** | Faqat Telegram orqali kirganlar uchun (§8). |
| Zaxira nusxa (D-22, D-37, D-51) | **Olinadi** | |
| PDF import, lug‘at parseri (D-09, D-10) | **Yo‘q** | Kontent admin importi orqali. |
| Grammatika (D-12, D-24, D-26–28) | **Yo‘q** | |
| Kitob testlari, imtihon sanasi, TOPIK yozish (D-33–35, D-49) | **Yo‘q** | |
| Dars topshiriqlari, kalendar (D-15, D-18, D-43, D-44) | **Yo‘q** | O‘rniga bo‘limlar. |
| Telegram Mini App ichida ishlash, initData allow-list | **Yo‘q** | V1 — PWA (§6). Mini App ichida raqamli tovar faqat Stars bilan sotiladi, shuning uchun Mini App kanal sifatida ishlatilmaydi. |

### 3.2 Keyingi bosqichlar (v1 da qilinmaydi, lekin arxitektura to‘sqinlik qilmasin)
- **Kamera:** narsani suratga olish → AI taniydi → so‘z + 2–3 ibora → shaxsiy lug‘atga.
- **"Sajang nima dedi?":** koreyscha nutqni eshitib, o‘zbekcha tushuntirish + javob variantlari.
- **Ish joyi yo‘nalishi:** EPS-TOPIK mavzulari bo‘yicha alohida bo‘limlar.
- **Meta Ray-Ban Display Web App** klienti.
- **To‘lov shlyuzi** (Toss Payments / PortOne) — v1 da qo‘lda tasdiqlash (§7.3).
- B va C darajalar kontenti — jarayon v1 da tayyor bo‘ladi, kontent tayyorlangani sari nashr qilinadi.

Shuning uchun: `CatalogWord` va shaxsiy so‘z (`PersonalWord`, kelajakda kamera uchun) bir xil `Card` mexanizmidan foydalana olishi kerak (§5.2).

---

## 4. Asosiy foydalanuvchi oqimi

### 4.1 Birinchi kirish (onboarding)
1. Landing yoki o‘qituvchi havolasi (`/?ref=PROMO` yoki `/join/{inviteCode}`) → "Kirish": **Telegram** yoki **Google** (§6.3).
2. Qisqa tanishtiruv (2–3 ekran, o‘tkazib yuborsa bo‘ladi): bo‘limlar qanday ochiladi, test, takrorlash.
3. **"Ilovani telefonga o‘rnating"** ko‘rsatmasi (iPhone: Safari → Ulashish → "Bosh ekranga qo‘shish"; Android: "O‘rnatish" tugmasi). Keyinroq Sozlamalardan ham ochiladi.
4. Bosh sahifa: A daraja, 1-bo‘lim ochiq.

### 4.2 Bo‘limlar sahifasi (Kibo skrinshotidagidek ro‘yxat, KELISHILGAN)
- Yuqorida daraja tanlash: **A · B · C** (nashr qilinmagan daraja "Tez orada").
- Har bo‘lim qatori: raqam, "N-bo‘lim · 25 ta so‘z", holat va belgi:
  - **Qulflangan** (qulf ikonkasi, xira) — ochib bo‘lmaydi, bosilsa: "Avval N-1-bo‘limni tugating" va **"Bilaman — testni topshirish"** tugmasi (faqat navbatdagi qulflangan bo‘lim uchun, §4.5).
  - **Ochiq** — progress (masalan "12/25 boshlangan").
  - **Tugallangan** — eng yaxshi test natijasi (masalan "92%") va belgi.
- Ixtiyoriy: bo‘limning eng ko‘p uchraydigan 3 ta mavzu tegi ("Asosiy fe’l · Olmosh").

### 4.3 Bo‘lim sahifasi — 4 qadam
Bo‘lim ichida yo‘l ko‘rsatkichi (stepper), lekin qadamlar majburiy tartibda emas — faqat **4-qadam (test) natijasi** bo‘limni tugatadi:

1. **Jadval** (D-36): 25 so‘z jadvalda (№ | 한국어 | O‘zbekcha). Koreyscha yoki o‘zbekcha ustunni yopish, yopiq katakni bosib ochish, so‘zni bosganda: talaffuz, misol, tuzoq, "Qaysi so‘z to‘g‘ri?" iboralari. Taymer 5/10/15 daq. Jadval ko‘rish takrorlash jadvalini **o‘zgartirmaydi**.
2. **Kartochka**: shu bo‘limning 25 so‘zi ikki yo‘nalishda (drill, D-36 dagi kabi). Bo‘lim birinchi ochilganda kartalar yaratiladi (§5.3). Javoblar `ladder-v1` ga yoziladi.
3. **Mashqlar**: yozma mashq, "Qaysi so‘z to‘g‘ri?", talaffuz (premium) — shu bo‘lim so‘zlari bilan.
4. **Yakuniy test** (§4.4).

### 4.4 Bo‘lim yakuniy testi (KELISHILGAN: ≥80%)
- **25 savol**, har so‘zdan bittadan, aralash tartibda. Savol turlari taqsimoti (TEXNIK ASOS):
  - 6 × `WordMeaning` (koreyscha → o‘zbekcha ma’noni tanlash, 4 variant);
  - 6 × `WordKorean` (o‘zbekcha → koreyscha so‘zni tanlash, 4 variant);
  - 8 × `WordKoreanTyped` (o‘zbekcha → koreyscha so‘zni **yozish**; L-38);
  - 2 × `WordListening` (audio eshitiladi → koreyscha so‘zni tanlash; matn javobgacha ko‘rinmaydi);
  - 3 × `WordListeningTyped` (audio eshitiladi → koreyscha so‘zni **yozish**; L-38). TTS mavjud bo‘lmasa, eshitish savollari o‘rniga tanlash va yozish savollari tuziladi (25 so‘zda 8 / 7 / 10) va natijada qayd etiladi.
  - Qaysi so‘z qaysi turga tushishi tasodifiy (seed sessiya vaqtidan, KoreanAiTeacher D-45 usuli).
- **Noto‘g‘ri variantlar (distraktor):** shu yoki oldingi nashr qilingan bo‘limlardan, iloji bo‘lsa bir xil so‘z turkumidan; to‘g‘ri javobning sinonimi, bir xil yozilgan omonimi yoki ma’nosi bir xil bo‘lgan so‘z variant bo‘lmaydi. Variantlar serverda aralashtiriladi.
- **O‘tish sharti:** to‘g‘ri javoblar ≥ `ceil(savollar × 0.8)` (25 savolda **20**). Foiz `Units:PassPercent` (standart 80) sozlamada.
- Test davomida to‘g‘ri javob ko‘rsatilmaydi (TEST03). Yakunda: ball, to‘g‘ri/noto‘g‘ri ro‘yxati, xato so‘zlarning to‘g‘ri javobi.
- **O‘tdi:** bo‘lim `Passed`, keyingi bo‘lim avtomatik `Open`. Tabriklash — sokin matn bilan, konfettisiz.
- **O‘tmadi:** "Xato qilgan N ta so‘zni takrorlash" (shu so‘zlar bilan kartochka drill) va "Testni qayta topshirish". Qayta topshirishda yangi tasodifiy tartib, yangi distraktorlar va boshqa savol turi taqsimoti.
- Qayta urinish cheklovi yo‘q (bo‘limning o‘zida). Faqat "Bilaman" testi cheklangan (§4.5).
- **Test takrorlash jadvalini o‘zgartirmaydi** (KoreanAiTeacher TEST04: variant tanlash mustaqil eslash dalili emas). Xato javoblar Xatolar daftariga "bo‘lim testi" manbasi bilan tushadi.
- Server baholaydi; javoblar `clientEventId` bilan idempotent; test yakunlangandan keyin javob o‘zgartirilmaydi.

### 4.5 "Bilaman — testni topshirish" (o‘tkazib yuborish, KELISHILGAN)
- Faqat **navbatdagi bitta qulflangan bo‘lim** uchun (ketma-ket: 3 ni o‘tkazish uchun avval 2 ni). Bir nechta bo‘limni ketma-ket o‘tkazish mumkin, har birining testini topshirib.
- Test formati §4.4 bilan bir xil, o‘tish sharti bir xil.
- O‘tsa: bo‘lim `Passed` (`PassedBy = SkipTest`), keyingisi ochiladi. Bo‘lim so‘zlari uchun kartalar yaratiladi, **to‘g‘ri javob berilgan** so‘zlar `Review` holatida 1 kunlik oraliq bilan (keyingi kuni tekshiriladi), **xato** so‘zlar `New` holatida (KoreanAiTeacher `LearningState` invariantlarini buzmagan holda — agent `ladder-v1` ga mos usulni DECISIONS’ga yozadi).
- O‘tmasa: bo‘lim qulflangan qoladi, "Avval oldingi bo‘limni tugating".
- **Suiiste’moldan himoya:** bitta bo‘lim uchun "Bilaman" testi kuniga ko‘pi bilan **3 marta** (`Units:SkipAttemptsPerDay`). Taxmin qilib o‘tib ketishni qiyinlashtiradi.

### 4.6 Takrorlash (har kuni)
- Bosh sahifada: "Bugun takrorlash: N ta karta" va "Boshlash/Davom etish" (KoreanAiTeacher Bugun sahifasi, D-42).
- Takrorlash bo‘limlarga bog‘liq emas: **barcha ochilgan** bo‘limlarning muddati kelgan kartalari (5-bo‘limda o‘qiyotgan odamga 1-bo‘limda unutgan so‘zi ham chiqadi).
- Yangi so‘zlar faqat joriy ochiq (tugallanmagan) bo‘limdan olinadi.
- Har baholashdan keyin taxminiy keyingi vaqt ko‘rsatiladi (UX03).

### 4.7 Navigatsiya (TEXNIK ASOS)
Pastki panel: **Bosh sahifa · Bo‘limlar · Takrorlash · Mashq · Profil**.
- Mashq: yozma mashq, "Qaysi so‘z to‘g‘ri?", talaffuz*, gap zinapoyasi*, zaif joylar*, xatolar daftari* (* — premium, qulf belgisi bilan ko‘rinadi, bosilsa Premium sahifasi).
- Profil: Statistika, Premium holati, Sozlamalar (mavzu: yorug‘/qorong‘i/qurilma; lotincha o‘qilish; eslatmalar), o‘qituvchi guruhi, "Ilovani o‘rnatish", Haqida (manba, litsenziya, maxfiylik siyosati), Chiqish, Hisobni o‘chirish.

---

## 5. Ma’lumot modeli (TEXNIK ASOS)

Yangi repo — **yangi baza**, eski ma’lumotni ko‘chirish yo‘q. KoreanAiTeacher migratsiyalari ko‘chirilmaydi; yangi `Initial` migratsiya yoziladi.

### 5.1 Yangi va o‘zgargan modellar

| Model | Maydonlar va qoidalar |
|---|---|
| `User` | `Id`, `DisplayName`, `CreatedAt`, `LastSeenAt`, `TimeZone` (standart `Asia/Seoul`), `Role` (`Learner`/`Teacher`/`Editor`/`Admin`, bir nechta bo‘lishi mumkin — flags yoki alohida jadval), eslatma sozlamalari (KoreanAiTeacher’dagi), `ShowRomanization`, `Theme`, `DeletedAt`. **Telegram ID to‘g‘ridan-to‘g‘ri User’da emas** — `UserLogin` da. |
| `UserLogin` | `UserId`, `Provider` (`Telegram`/`Google`), `ProviderUserId` (unique per provider), `Username/Email` (faqat ko‘rsatish uchun), `CreatedAt`. Bitta odam ikkala usulni bog‘lashi mumkin (Profil → "Hisobni bog‘lash"). |
| `CatalogWord` | §2.3 maydonlari + `Id`, `Level` (A/B/C), `UnitId`, `OrderInUnit`, `IsStudyable`, `Status` (`Draft`/`Reviewed`), `ContentVersion`, `ReviewedByUserId`, `ReviewedAt`, `AiProvider/Model` (qoralama kim yozgani), `SourceKey` (import idempotentligi uchun). |
| `CatalogUsage` | KoreanAiTeacher `WordUsage` ning umumiy (owner’siz) varianti: `CatalogWordId`, `ContentVersion`, `Phrases`, `Questions` ("Qaysi so‘z to‘g‘ri?"). `TrapUz` va `Example` `CatalogWord` da. |
| `Unit` | `Id`, `Level`, `Number` (daraja ichida 1..N, unique), `Status` (`Draft`/`Published`), `PublishedAt`, `WordCount`. |
| `UserUnitProgress` | `UserId`, `UnitId` (unique juft), `Status` (`Open`/`Passed`; qulflangan = yozuv yo‘q yoki `Locked`), `OpenedAt`, `PassedAt`, `PassedBy` (`Test`/`SkipTest`), `BestScorePercent`, `Attempts`, `Version` (xmin). |
| `UnitTestAttempt` | KoreanAiTeacher `TestAttempt` dvigatelida: `Kind = UnitFinal` yoki `UnitSkip`, `UnitId`, savollar, javoblar, ball, `Passed`, `StartedAt`, `FinishedAt`. |
| `Card` | KoreanAiTeacher’dagidek, lekin `WordSenseId` o‘rniga **`LearningItemId`** (§5.2). Unique: `OwnerId + LearningItemId + Direction`. |
| `ReviewEvent` | KoreanAiTeacher’dagidek (`LearningItemId` bilan). |
| `Subscription` | `UserId`, `Plan` (`Monthly`/`Quarterly`/`Trial`/`Gift`), `StartsAt`, `EndsAt`, `Source` (`ManualTransfer`/`Promo`/`Gateway`/`Admin`), `PaymentId?`, `CreatedByUserId`. Premium = `now ∈ [StartsAt, EndsAt)` bo‘lgan yozuv bor. Muddatlar ketma-ket qo‘shiladi (amaldagi obuna tugagan joydan davom etadi). |
| `Payment` | `Id`, `UserId`, `AmountKrw`, `Plan`, `PaidAt`, `Method` (`BankTransfer`/…), `PromoCodeId?`, `Note`, `RecordedByUserId`, `Status` (`Confirmed`/`Refunded`). |
| `PromoCode` | `Code` (unique, katta-kichik harf farqsiz), `TeacherUserId?`, `DiscountPercent`, `CommissionPercent` (standart 30), `IsActive`, `ValidUntil?`. |
| `Group` / `GroupMember` | O‘qituvchi guruhi (§9): `Group(Id, TeacherUserId, Name, InviteCode, CreatedAt)`, `GroupMember(GroupId, UserId, JoinedAt, LeftAt?)`. |
| `AudioAsset`, `AiUsage`, `ReminderDelivery`, `BackupRun` | KoreanAiTeacher’dagidek. `AudioAsset` katalog so‘zi uchun umumiy (kalit: matn + til + ovoz + versiya). |

### 5.2 `LearningItem` — kelajak uchun bitta abstraksiya
`Card` to‘g‘ridan-to‘g‘ri `CatalogWord` ga emas, **`LearningItem`** ga bog‘lanadi:
- `LearningItem(Id, Kind, CatalogWordId?, PersonalWordId?, Korean, Uzbek, ContentVersion)` — v1 da faqat `Kind = Catalog`.
- Kelajakda kamera so‘zlari (`Kind = Personal`, owner’li) xuddi shu Card/ReviewEvent/sessiya mexanizmida ishlaydi.
- Agent boshqa toza yechim tanlasa (masalan, polimorf FK o‘rniga ikki nullable FK + check constraint), DECISIONS’ga yozadi. Asosiy talab: **bitta takrorlash dvigateli ikki xil so‘z manbasi bilan ishlashi**.

### 5.3 Invariantlar
- **DATA01:** unique — `UserLogin(Provider, ProviderUserId)`, `Card(OwnerId, LearningItemId, Direction)`, `ReviewEvent.Id`, `UserUnitProgress(UserId, UnitId)`, `Unit(Level, Number)`, `PromoCode.Code`. ReviewEvent + Card bitta tranzaksiyada.
- Kartalar bo‘lim **birinchi ochilganda** (yoki "Bilaman" testi o‘tganda) yaratiladi — idempotent (qayta ochish dublikat yaratmaydi).
- Bo‘lim faqat `Published` bo‘lsa ochilishi mumkin. 1-bo‘lim (A) har yangi foydalanuvchi uchun avtomatik `Open`.
- Keyingi bo‘lim ochilishi faqat server tomonidan (`UnitProgressService`), test natijasi asosida. Klient "ochildi" deb yubora olmaydi.
- Bo‘lim so‘zi tahrirlansa (`ContentVersion++`), kartalar va statistika saqlanadi; audio va AI artefaktlari eskiradi (AU05).
- Foydalanuvchi hisobini o‘chirsa: shaxsiy ma’lumot (login, progress, kartalar, javoblar) o‘chiriladi yoki anonimlashtiriladi; to‘lov yozuvlari buxgalteriya uchun anonim holda qoladi. Katalog o‘zgarmaydi.

---

## 6. Texnik arxitektura

### 6.1 Stack (KELISHILGAN: C#, KoreanAiTeacher bilan bir xil)
| Qism | Tanlov |
|---|---|
| Backend | .NET 10 LTS, ASP.NET Core minimal API, modular monolith (ARCH01) |
| Frontend | Blazor WebAssembly + kichik JS interop (audio, mikrofon, PWA o‘rnatish, Telegram Login, Google Identity) |
| Baza | PostgreSQL + EF Core, migratsiyalar ilova ishga tushganda (D-08) |
| Fon ishlari | ASP.NET Core worker, bazadagi job/outbox |
| AI | KoreanAiTeacher `IAiProvider` / `AiGateway` (Anthropic + OpenAI, D-21) |
| Ovoz | Azure Speech TTS va talaffuz baholash (D-31, D-32), OpenAI zaxira TTS |
| Testlar | xUnit v3, real PostgreSQL (D-11), `FakeTimeProvider` |
| Hosting | Railway, **alohida loyiha** (KoreanAiTeacher’dan alohida servis va baza), Dockerfile |

Qatlamlar: `Lugat.Domain`, `Lugat.Contracts`, `Lugat.Application`, `Lugat.Infrastructure`, `Lugat.Api`, `Lugat.Web` + testlar (KoreanAiTeacher AGENTS.md tuzilmasi).

### 6.2 PWA (KELISHILGAN: mobil ilova emas, PWA)
- `manifest.webmanifest`: nom, qisqa nom, ikonka (192/512, maskable), `display: standalone`, `start_url: /`, `theme_color`, `background_color`, `lang: uz`.
- Service worker (Blazor PWA shabloni asosida): ilova qobig‘i va `_framework` fayllari keshlanadi, yangi versiya chiqqanda "Yangilanish tayyor — qayta yuklash" xabari. API javoblari keshlanmaydi (shaxsiy ma’lumot).
- Offline: ochilgan sessiyadagi yuborilmagan javoblar mahalliy navbatda saqlanadi va ulanish qaytganda o‘sha `eventId` bilan yuboriladi (KoreanAiTeacher REL03 — mavjud mexanizm). To‘liq oflayn ishlash va’da qilinmaydi.
- O‘rnatish: Android/Chrome’da `beforeinstallprompt` orqali "O‘rnatish" tugmasi; iOS’da qo‘lda ko‘rsatma ekrani (rasmli 3 qadam).
- **TEKSHIRILADI:** Blazor WASM birinchi yuklanish hajmi va vaqti arzon Android telefonda 4G’da. Maqsad: birinchi yuklanish ≤ 6 s, keyingisi (keshdan) ≤ 2 s. Trimming, AOT emas (hajm oshadi), Brotli siqish, `boot` ekrani (KoreanAiTeacher index.html’dagi). O‘lchov natijasi CHECKLIST’ga.
- Telegram SDK (`telegram-web-app.js`) **yuklanmaydi** (Mini App emas).

### 6.3 Kirish (TEXNIK ASOS)
- **Telegram Login Widget** (asosiy — auditoriyaning hammasida Telegram bor). Server tekshiruvi rasmiy algoritm bo‘yicha: `secret = SHA256(bot_token)`, `HMAC_SHA256(data_check_string, secret) == hash`, `auth_date` yangiligi (≤ 24 soat). Bu Mini App initData algoritmidan **farq qiladi** — KoreanAiTeacher’dagi `TelegramInitDataValidator` ni ko‘r-ko‘rona ishlatmang. BotFather’da domen sozlanadi (Johnny qiladi).
- **Google** (Google Identity Services, ID token serverda tekshiriladi: imzo, `aud`, `iss`, `exp`).
- Sessiya: KoreanAiTeacher D-03 (bearer access 1 soat + refresh 7 kun; PWA uchun refresh 30 kun — TEXNIK ASOS).
- Allow-list **yo‘q** — ochiq ro‘yxatdan o‘tish. Shu sabab: rate limit (kirish, test, AI endpointlari), AI kunlik limitlari (§7.4).
- Development’da `POST /api/auth/dev` (KoreanAiTeacher’dagidek, faqat Development’da ro‘yxatdan o‘tadi va buni test tekshiradi).
- Telegram orqali kirgan foydalanuvchiga bot eslatma yubora olishi uchun "Botni ishga tushirish" havolasi (`t.me/<bot>?start=...`) onboarding’da (NOT02).

### 6.4 Xavfsizlik (KoreanAiTeacher §14 + ochiq ro‘yxatdan o‘tish)
- Barcha shaxsiy endpointlar server tasdiqlagan `userId` bilan; klient yuborgan `ownerId` ga ishonilmaydi. Boshqa odamning progressi/testi/kartasi ID orqali ochilmaydi (404) — har modul uchun test.
- Rollar: `Admin`, `Editor` endpointlari alohida policy bilan. O‘qituvchi faqat o‘z guruhidagi o‘quvchilarning §9.2 dagi ma’lumotini ko‘radi.
- Rate limit (ASP.NET Core RateLimiter): kirish 10/daq/IP, test boshlash 20/soat/user, AI endpointlari tarif limiti bilan.
- Mikrofon yozuvi baholashdan keyin o‘chiriladi (SEC04).
- Maxfiylik siyosati sahifasi (o‘zbekcha): qanday ma’lumot yig‘iladi (login ID, ism, o‘quv natijalari, to‘lov yozuvi), AI/Azure’ga nima yuboriladi, qanday o‘chirish mumkin. Johnny matnni tasdiqlaydi.

---

## 7. Premium va to‘lov

### 7.1 Nima bepul, nima pullik (KELISHILGAN)
| Bepul | Premium |
|---|---|
| Barcha bo‘limlar va so‘zlar (A, keyin B, C) | Talaffuz baholash (Azure, mikrofon) |
| Jadval, kartochka, takrorlash | Xatolar daftari |
| Bo‘lim testlari va "Bilaman" testi | Gap zinapoyasi, AI gap tekshiruvi, Tuzoqlar daftari, Zaif joylar |
| Yozma mashq, "Qaysi so‘z to‘g‘ri?" | (v1.1) Tinglash |
| So‘zni eshitish (TTS, keshlangan) | |
| Statistika | |

**JOHNNY TASDIQLAYDI — tavsiya:** so‘z kartasidagi tayyor (katalogdagi) `trap_uz` izohi **bepul** ko‘rsatilsin. U bir marta tayyorlangan, xarajati yo‘q va mahsulotning asosiy farqi — foydalanuvchi uni birinchi daqiqalarda ko‘rishi kerak. Pullik "AI tushuntirish" = interaktiv AI (gap tekshiruvi, zinapoya). Standart: `Premium:TrapNotesFree = true`.

### 7.2 Narx (KELISHILGAN)
- Oylik: **₩5,000**. 3 oylik: **₩12,000**.
- Narxlar konfiguratsiyada (`Premium:Plans`), kodga yozilmaydi.
- **JOHNNY TASDIQLAYDI:** yangi foydalanuvchiga bepul premium sinov muddati. Standart `Premium:TrialDays = 0` (o‘chiq).

### 7.3 To‘lov v1 — qo‘lda tasdiqlash (KELISHILGAN: Telegram Stars emas)
1. Premium sahifasi: tariflar, "Qanday to‘lanadi": bank o‘tkazmasi rekvizitlari (konfiguratsiyadan: `Payments:BankName`, `Payments:AccountNumber`, `Payments:AccountHolder`), promo-kod maydoni (chegirmani darhol ko‘rsatadi), "Chekni yuborish" → Telegram havolasi (`Payments:ContactTelegram`).
2. Foydalanuvchiga **to‘lov kodi** ko‘rsatiladi (masalan `L-7K3Q`, userga bog‘langan qisqa kod) — o‘tkazma izohiga yozish uchun, admin uni topishi oson bo‘lsin.
3. Admin panelda: kod yoki ism bo‘yicha foydalanuvchini topadi → "To‘lovni qayd etish" (summa, tarif, promo-kod) → `Payment` + `Subscription` avtomatik yaratiladi.
4. Foydalanuvchiga ilovada (va Telegram orqali kirgan bo‘lsa botda) "Premium faollashdi, muddati: …" xabari.
5. Qaytarish: admin `Refunded` qiladi → obuna shu kundan tugaydi.

`IPaymentGateway` interfeysi hozirdan bo‘lsin (v1 da implementatsiyasiz), keyin Toss/PortOne qo‘shilganda oqim o‘zgarmasin.

### 7.4 AI va ovoz limiti (TEXNIK ASOS, KoreanAiTeacher D-50 usulida)
- Bepul foydalanuvchi: AI so‘rovi **0** (premium funksiyalar yopiq). TTS cheklanmagan (kesh).
- Premium: kuniga AI 60 so‘rov, talaffuz baholash 60 urinish (`Ai:DailyLimits:*`).
- Umumiy oylik byudjet ogohlantirishi va to‘xtatish (BUD05) — summalar konfiguratsiyada; Johnny belgilaydi.
- Limit tugasa: tushunarli o‘zbekcha xabar, asosiy kartochka/takrorlash ishlashda davom etadi (REL01).

---

## 8. Eslatmalar
- KoreanAiTeacher D-16 mexanizmi: faqat Telegram orqali kirgan va botni ishga tushirgan foydalanuvchilarga.
- Standart: har kuni 20:00 (foydalanuvchi vaqti), faqat muddati kelgan karta bo‘lsa: "Bugun N ta so‘zni takrorlash vaqti. ~M daqiqa." Havola ilovani takrorlash sahifasida ochadi.
- Sozlamalarda vaqt, kunlar va o‘chirish. Ayblovchi matn yo‘q (NOT04). Kechikkan eslatmalar uyum qilib yuborilmaydi (NOT03).
- Web Push — v1.1 (iOS’da faqat o‘rnatilgan PWA’da ishlaydi; TEKSHIRILADI).

---

## 9. O‘qituvchilar (KELISHILGAN: tarqatish kanali)

### 9.1 O‘qituvchi va guruh
- Admin foydalanuvchiga `Teacher` rolini beradi va unga promo-kod biriktiradi.
- O‘qituvchi guruh yaratadi → **taklif havolasi** `/join/{inviteCode}`. O‘quvchi havolani ochib kiradi → "Siz [O‘qituvchi ismi] guruhiga qo‘shilyapsiz. O‘qituvchi sizning bo‘lim natijalaringiz va xato so‘zlaringizni ko‘radi" → rozilik tugmasi. O‘quvchi Profil’dan guruhdan chiqishi mumkin.
- Taklif havolasi orqali kelgan o‘quvchiga o‘qituvchining promo-kodi avtomatik biriktiriladi (Premium sahifasida oldindan to‘ldiriladi).

### 9.2 O‘qituvchi paneli (`/oqituvchi`)
- Guruhlar ro‘yxati, har guruhda o‘quvchilar jadvali: ism, joriy bo‘lim, tugallangan bo‘limlar soni, oxirgi test natijasi, so‘nggi 7 kunda faol kunlar, oxirgi kirish.
- O‘quvchini bosganda: bo‘limlar bo‘yicha ballar, eng ko‘p xato qilgan 10 so‘z.
- Guruh bo‘yicha: eng qiyin 10 so‘z (eng ko‘p xato qilingan).
- O‘qituvchi boshqa ma’lumotni ko‘rmaydi (javoblar tarixi, to‘lov, login).
- **Pilot ko‘rsatkichi** (Johnny kuzatadi): guruh o‘quvchilarining necha foizi 2-haftada ham faol.

### 9.3 Komissiya hisoboti
- O‘qituvchi panelida: "Mening promo-kodim", shu kod bilan qilingan tasdiqlangan to‘lovlar soni va summasi, oy bo‘yicha komissiya (`CommissionPercent`). To‘lovchilarning ismi ko‘rsatilmaydi (faqat soni va summa).
- Komissiyani to‘lash ilovadan tashqarida (Johnny qo‘lda). Admin "To‘landi" deb belgilaydi.

---

## 10. Admin panel (`/admin`, faqat `Admin`; kontent qismi `Editor` ga ham)
- **Kontent:** rasmiy Excel import; daraja/bo‘lim/holat filtri; so‘zni tahrirlash (barcha §2.3 maydonlari); "AI qoralama" (bo‘lim uchun); "Tekshirildi"; bo‘limni nashr qilish (barcha so‘z tekshirilgan bo‘lsagina); audio oldindan yaratish (bo‘lim uchun).
- **Foydalanuvchilar:** qidirish (ism, to‘lov kodi, Telegram username, email), progress ko‘rish, rol berish.
- **To‘lovlar:** to‘lovni qayd etish, ro‘yxat, qaytarish, obuna muddatini qo‘lda o‘zgartirish (sabab bilan).
- **Promo-kodlar va o‘qituvchilar:** yaratish, o‘chirish, komissiya "To‘landi".
- **Sarf:** AI va ovoz sarfi (`ai_usage`), kunlik/oylik.
- **Asosiy ko‘rsatkichlar** (oddiy jadval, grafik shart emas): ro‘yxatdan o‘tganlar, kunlik faol, D1/D7 retention, bo‘lim bo‘yicha test o‘tish foizi, premium soni, oylik tushum.

---

## 11. API (boshlang‘ich shartnoma, TEXNIK ASOS)
KoreanAiTeacher endpointlari (`/api/sessions`, `/api/sessions/{id}/answers`, `/api/reviews/{eventId}/undo`, `/api/writing/*`, `/api/tests/*`, `/api/speech/*`, `/api/ai/*`, `/api/me/*`) imkon qadar saqlanadi. Yangilari:

| Amal | Endpoint |
|---|---|
| Kirish | `POST /api/auth/telegram-login`, `POST /api/auth/google`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/me/logins` (hisob bog‘lash) |
| Hisob | `GET/PATCH /api/me`, `DELETE /api/me` (o‘chirish) |
| Darajalar va bo‘limlar | `GET /api/levels` · `GET /api/levels/{level}/units` (holat, progress, ball) · `GET /api/units/{id}` (so‘zlar — faqat ochiq bo‘lim uchun) |
| Bo‘limni boshlash | `POST /api/units/{id}/open` (idempotent; kartalar yaratiladi) |
| Bo‘lim testi | `POST /api/units/{id}/tests` (`kind: final|skip`) → savollar (javobsiz) · `POST /api/unit-tests/{attemptId}/answers` · `POST /api/unit-tests/{attemptId}/finish` → natija, `passed`, ochilgan bo‘lim |
| Bo‘lim kartochkasi | `POST /api/sessions` ga `unitId` (va ixtiyoriy `catalogWordIds` — xato so‘zlar) |
| Bugun | `GET /api/today` — muddati kelgan kartalar, joriy bo‘lim, davom ettiriladigan sessiya |
| Premium | `GET /api/me/premium` (holat, muddat, to‘lov kodi) · `GET /api/premium/plans` · `POST /api/premium/promo/check` |
| Guruh | `POST /api/groups/join/{inviteCode}` · `DELETE /api/groups/{id}/membership` |
| O‘qituvchi | `GET/POST /api/teacher/groups` · `GET /api/teacher/groups/{id}` · `GET /api/teacher/students/{userId}` · `GET /api/teacher/commission` |
| Admin | `/api/admin/catalog/*`, `/api/admin/units/*`, `/api/admin/users/*`, `/api/admin/payments/*`, `/api/admin/promo-codes/*`, `/api/admin/usage`, `/api/admin/metrics` |

Qoidalar: mutatsiyalar idempotent (`clientEventId`), optimistic concurrency (xmin, 409), xatolar `ProblemDetails` o‘zbekcha (401/403/404/409/422/429/503), premium talab qiluvchi endpoint premiumsiz **402 emas, 403** + `detail: "Bu funksiya Premium’da."` va `type: "premium-required"` (klient Premium sahifasini ochadi).

---

## 12. Ekranlar ro‘yxati (har biri: loading, empty, error, retry holatlari bilan — UX06)
1. Landing/kirish (Telegram, Google).
2. Onboarding (2–3 ekran) + "Ilovani o‘rnatish".
3. Bosh sahifa (bugungi takrorlash, joriy bo‘lim, "Davom etish").
4. Bo‘limlar (daraja tablari, qulf/ochiq/tugallangan).
5. Bo‘lim sahifasi (4 qadam + "Bilaman").
6. Jadval (D-36).
7. Kartochka sessiyasi (UX03).
8. Bo‘lim testi va natija.
9. Mashq menyusi va mashq sahifalari (yozma, qo‘llanish, talaffuz*, zinapoya*, zaif joylar*, xatolar daftari*).
10. Statistika.
11. Premium (tariflar, to‘lov ko‘rsatmasi, promo-kod, holat).
12. Profil va Sozlamalar, Haqida (manba, litsenziya, maxfiylik).
13. Guruhga qo‘shilish (rozilik).
14. O‘qituvchi paneli.
15. Admin panel.

**Dizayn:** KoreanAiTeacher’ning mavjud CSS va komponentlaridan boshlang (D-25), lekin brend rangi va nomi konfiguratsiya/CSS o‘zgaruvchilarida bo‘lsin — Johnny keyin yangi dizayn beradi. Qorong‘i rejim `prefers-color-scheme` va Sozlamalar tanlovi bo‘yicha (Telegram mavzusi emas). Hech qaysi raqib ilovaning dizaynini nusxalamang.

---

## 13. Bajarish bosqichlari va qabul mezonlari

Har bosqich alohida branch/PR, CI yashil bo‘lgach `main` ga. Har bosqich oxirida `docs/CHECKLIST.md` yangilanadi.

### 13.1 Bosqich 0 — Repo va asos
- Yangi repo: **https://github.com/JohnnySenior/LearnKoreanWordsEasly** (Johnny ochgan). Barcha kod faqat shu repoga yoziladi.
- KoreanAiTeacher `main` dagi joriy holatini yangi repoga ko‘chiring (git tarixisiz, birinchi commit izohida manba commit SHA’si). Namespace va loyiha nomlarini `KoreanAiTeacher.*` → `Lugat.*` ga o‘zgartiring.
- §3.1 dagi "Yo‘q" qismlarini olib tashlang: kod, endpointlar, sahifalar, testlar, migratsiyalar. Eski migratsiyalarni o‘chirib, yangi `Initial` migratsiya.
- Ushbu hujjatni `docs/SPECIFICATION.md` sifatida qo‘ying. KoreanAiTeacher’ning `DECISIONS.md` dan saqlanib qolgan qarorlar (D-05, D-11, D-14, D-21, D-23, D-29–32, D-36, D-41, D-45–47, D-50, D-52, D-54 va h.k.) yangi `docs/DECISIONS.md` ga **qisqa havola bilan** ko‘chiriladi ("KAT D-05 dan olingan"); olib tashlanganlari ko‘chirilmaydi. Yangi qarorlar `L-01` dan boshlanadi.
- Yangi `AGENTS.md`, `README.md`, `.env.example`, CI (`.github/workflows/ci.yml`), Dockerfile.
- **Qabul:** `dotnet build` va `dotnet test` yashil (qolgan testlar); ilova lokal ishga tushadi; olib tashlangan modullar haqida CHECKLIST’da ro‘yxat.

### 13.2 Bosqich 1 — Kirish va foydalanuvchilar
- `User` + `UserLogin`, Telegram Login Widget va Google, refresh, logout, hisobni bog‘lash, hisobni o‘chirish, rollar, rate limit.
- **Qabul:** noto‘g‘ri/eskirgan Telegram hash va noto‘g‘ri Google token rad etiladi (unit + HTTP test); bir provayder ID ikki User yaratmaydi; boshqa user ma’lumoti 404; dev login faqat Development’da.

### 13.3 Bosqich 2 — Katalog va kontent jarayoni
- `CatalogWord`, `CatalogUsage`, `Unit`; Excel import (idempotent); §2.2 bo‘lish algoritmi; AI qoralama; tekshirish va nashr; namuna JSON seed.
- **Qabul:** Excel import 5,965 yozuvni (yoki haqiqiy qatorlar sonini) saqlaydi, qayta import dublikat yaratmaydi; A daraja bo‘limlari chastota tartibida va 25 tadan; `보/의/관` bo‘limlarga kirmaydi; nashr qilinmagan bo‘lim foydalanuvchi API’sida ko‘rinmaydi; AI yaroqsiz javobi saqlanmaydi (test fake provider bilan).

### 13.4 Bosqich 3 — Bo‘limlar, kartalar, takrorlash
- `UserUnitProgress`, `LearningItem`, bo‘limni ochish va kartalar yaratish, Bo‘limlar va Bo‘lim sahifalari, Jadval, kartochka drill, kunlik takrorlash (barcha ochilgan bo‘limlardan), Bosh sahifa.
- **Qabul:** yangi foydalanuvchida faqat A-1 ochiq; qulflangan bo‘lim so‘zlari API orqali olinmaydi (403/404); bo‘limni ikki marta ochish kartalarni ko‘paytirmaydi; takrorlash navbatida 1-bo‘limning muddati kelgan kartasi 3-bo‘limda o‘qiyotgan userga chiqadi; KoreanAiTeacher’ning scheduler va sessiya testlari yashil.

### 13.5 Bosqich 4 — Bo‘lim testi va "Bilaman"
- §4.4 va §4.5 to‘liq: savol generatori (distraktor qoidalari), `WordListening` turi, baholash, ochilish, qayta topshirish, "Bilaman" kunlik limiti, xatolar daftariga yozish.
- **Qabul (avtomatik testlar):** 20/25 → o‘tdi va keyingi ochildi; 19/25 → o‘tmadi; testni yakunlashni ikki marta yuborish natijani ikki marta qo‘llamaydi; "Bilaman" faqat navbatdagi bo‘lim uchun, kuniga 3 marta; distraktorlar orasida to‘g‘ri javobning omonimi/sinonimi yo‘q; test javobgacha to‘g‘ri javobni API’da qaytarmaydi; test kartochka jadvalini o‘zgartirmaydi.

### 13.6 Bosqich 5 — Premium va to‘lov
- `Subscription`, `Payment`, `PromoCode`, entitlement servisi, premium endpointlarda 403 `premium-required`, Premium sahifasi, to‘lov kodi, admin to‘lov qayd etish, muddatni ketma-ket qo‘shish, qaytarish.
- **Qabul:** premiumsiz user talaffuz/xatolar daftari/zinapoya endpointlarida 403 oladi; to‘lov qayd etilgach darhol premium; muddat tugagach (FakeTimeProvider) premium yopiladi; ikki to‘lov muddatlarni to‘g‘ri qo‘shadi; promo-kod chegirmasi va komissiya to‘g‘ri hisoblanadi.

### 13.7 Bosqich 6 — PWA
- Manifest, ikonkalar, service worker, yangilanish xabari, o‘rnatish oqimi (Android tugma, iOS ko‘rsatma), offline javob navbati.
- **Qabul:** Chrome Lighthouse PWA "installable"; iPhone Safari’da bosh ekranga qo‘shib ochiladi (standalone); arzon Android’da birinchi yuklanish vaqti o‘lchanib CHECKLIST’ga yozilgan; yangi deploydan keyin eski kesh muammosiz yangilanadi.

### 13.8 Bosqich 7 — O‘qituvchilar va admin
- §9 va §10 to‘liq.
- **Qabul:** o‘qituvchi faqat o‘z guruhidagi rozilik bergan o‘quvchilarni ko‘radi (boshqa o‘quvchi 404); guruhdan chiqqan o‘quvchi ko‘rinmaydi; komissiya hisobotida to‘lovchilar ismi yo‘q; `Editor` admin to‘lov sahifalariga kira olmaydi.

### 13.9 Bosqich 8 — Eslatmalar, ishga tushirish
- Telegram eslatmalari, maxfiylik siyosati sahifasi, Railway’da alohida loyiha, backup, hard spend limit, asosiy ko‘rsatkichlar.
- **Qabul:** eslatma faqat muddati kelgan karta bo‘lganda va tanlangan vaqtda (fake clock testi); real iPhone va Android’da to‘liq oqim (kirish → 1-bo‘lim → test → 2-bo‘lim ochildi → ertasi kuni takrorlash) sinovdan o‘tgan va CHECKLIST’da dalil bilan yozilgan.

---

## 14. Kelajak klientlari uchun arxitektura talabi (Meta ko‘zoynagi va boshqalar)
- Barcha o‘quv mantiqi `Application` xizmatlarida va `/api` orqali. Blazor sahifasida biznes qoida bo‘lmasin (masalan, "80% dan ko‘p bo‘lsa ochiladi" faqat serverda).
- Kartochka sessiyasi API’si (`/api/sessions`, `/answers`) shunday bo‘lsinki, kichik ekranli boshqa klient (Meta Ray-Ban Display Web App: HTML/CSS/JS, 600×600 displey) faqat savolni ko‘rsatib, uchta bahodan birini yubora olsin.
- Auth token’ni boshqa klient ham ishlatishi mumkin (CORS konfiguratsiyasi sozlamada).
- Kamera funksiyasi uchun: `LearningItem.Kind = Personal` va AI tanish adapteri (`IVisionRecognizer`) qo‘shilishi kodni sindirmasligi kerak. V1 da bu interfeyslar **yaratilmaydi** — faqat to‘sqinlik qilmaslik talab etiladi.

---

## 15. Ochiq savollar (JOHNNY TASDIQLAYDI — agent standart qiymat bilan davom etadi)
| # | Savol | Standart |
|---|---|---|
| Q1 | Ilovaning yakuniy nomi va domeni | `Lugat`, Railway domeni |
| Q2 | Tayyor `trap_uz` bepulmi? | Ha (`Premium:TrapNotesFree = true`) |
| Q3 | Bepul premium sinov muddati | 0 kun |
| Q4 | Bank rekvizitlari va kontakt Telegram | Konfiguratsiyada bo‘sh; Premium sahifasida "Tez orada" |
| Q5 | Premium AI kunlik limitlari va oylik byudjet | 60 so‘rov/kun; byudjetni Johnny belgilaydi |
| Q6 | Eslatma standart vaqti | 20:00 |
| Q7 | Maxfiylik siyosati matni | Agent qoralama yozadi, Johnny tasdiqlaydi |
| Q8 | To‘lovlarni qaysi shaxs/tashkilot nomiga qabul qilish (biznes ro‘yxatdan o‘tish) | Kod bunga bog‘liq emas; faqat konfiguratsiya |

---

## 16. Manbalar
- KoreanAiTeacher (asos, faqat o‘qish): https://github.com/JohnnySenior/KoreanAiTeacher
- Lug‘at (yangi repo, ish shu yerda): https://github.com/JohnnySenior/LearnKoreanWordsEasly
- 국립국어원 한국어 학습용 어휘 목록 (KOGL 1): https://www.korean.go.kr/front_eng/down/down_02V.do?etc_seq=70
- Telegram Login Widget (server tekshiruvi): https://core.telegram.org/widgets/login
- Google Identity Services — ID token tekshirish: https://developers.google.com/identity/gsi/web/guides/verify-google-id-token
- Blazor PWA: https://learn.microsoft.com/aspnet/core/blazor/progressive-web-app
- Telegram Stars siyosati (nima uchun Mini App ishlatilmaydi): https://core.telegram.org/bots/payments-stars
- Azure Speech til qo‘llab-quvvatlashi: https://learn.microsoft.com/azure/ai-services/speech-service/language-support

Implementatsiya kuni paket versiyalari, tariflar va tashqi API hujjatlari qayta tekshiriladi (BUD02).

## 16. Bo‘lim bilan ovozli tanishuv (2026-10-29, Johnny bilan KELISHILGAN)

Bo‘lim jadvalidan oldin ixtiyoriy tanishtirish: so‘zlar bittadan ochiladi, ovozga mos doska qadamlari va tuzoqlarga chuqurroq izoh. O‘tkazib yuborish, pauza, tezlik, qayta eshitish, faqat tuzoqlar va qolgan joyidan davom etish mumkin. Dars ko‘rish yodlash yoki bo‘limni tugatish hisoblanmaydi. Dizayn — Johnny ma’qullagan Jonli doska namunasi (L-54).

Kontent oqimi har bo‘lim uchun majburiy: admin tekshirilgan katalogdan AI qoralama so‘raydi → ssenariyni o‘qib tahrirlaydi, har so‘zni tasdiqlaydi → ovoz yaratiladi → admin tayyor darsni eshitib tekshiradi → alohida nashr qiladi. Faqat nashrdan keyin o‘quvchiga ko‘rinadi; o‘quvchi kirganda AI generatsiyasi yo‘q. Yangi tahrir qoralama, oldingi nashr almashtirilmaguncha ishlaydi. Texnik tafsilotlar L-54.
