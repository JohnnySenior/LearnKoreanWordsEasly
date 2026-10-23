# Handoff: Lug‘at — mobil UI (asosiy oqim + Profil/Admin)

## Umumiy
Lug‘at PWA uchun 13 ta mobil ekran: kirish, bosh sahifa, bo‘limlar, bo‘lim jadvali, kartochka, bo‘lim testi, natija, mashq menyusi, premium, hamda qayta tuzilgan Profil (o‘quvchi va admin), Admin panel va Admin → Tizim. Mahsulot talablari: `docs/SPECIFICATION.md` (§4, §7, §10, §12).

## Dizayn fayllari haqida
`design/Lugat Screens.dc.html` — HTML’da yasalgan **dizayn namunasi** (brauzerda to‘g‘ridan-to‘g‘ri oching; canvas rejimida, zoom/pan bilan). Bu production kod emas. Vazifa — shu ko‘rinish va xatti-harakatni **Lugat.Web (Blazor WebAssembly)** ichida mavjud tuzilma bilan qayta qurish: Razor sahifalar/komponentlar + `src/Lugat.Web/wwwroot/css/app.css`. Namunadagi inline style’lar o‘rniga `app.css` da klasslar yozing.

## Aniqlik darajasi
**High-fidelity.** Ranglar, shrift, o‘lchamlar, matnlar yakuniy. Ekranlardagi raqamlar (18 ta karta, ₩, $18.40, 41 o‘quvchi …) — namuna; real qiymat API’dan keladi.

## Dizayn qoidalari (qat’iy)
- Burchak radiusi hamma joyda **0** (ikonkalar ichidagi kichik radius bundan mustasno).
- Tugma va qatordagi matn **chapga tekislangan**; trailing ikonka o‘ngda (`justify-content: space-between`).
- Asosiy bo‘limlar orasida **2px** chiziq `var(--color-divider)`; ro‘yxat qatorlari orasida **1px** `var(--color-divider)`.
- Accent (qizil) faqat asosiy amal, faol holat va kichik urg‘u uchun. Accent rangidagi oddiy matn — `--color-accent-700` (#ae1800, app.css’da `--color-warn`).
- Soya yo‘q (faqat dialoglarda `--shadow-lg`). Gradient, emoji, konfetti yo‘q.
- 360 px kenglik asos. Yonlama scroll yo‘q. Tegish nishoni ≥ 44 px.
- Koreyscha matn `lang="ko"` / `.ko` → `var(--font-ko)`.

## Dizayn tokenlari
Ular `app.css` da allaqachon bor — shularni ishlating:

| Token | Yorug‘ | Qorong‘i |
|---|---|---|
| `--color-bg` | #f3f2f2 | #191817 |
| `--color-surface` | #eae9e9 | #242221 |
| `--color-text` | #201e1d | #eeebea |
| `--color-accent` | #ec3013 | #f0442a |
| `--color-divider` | text 40% | text 32% |
| `--color-muted` | #605d5d | #b3afae |
| `--color-warn` (accent-700) | #ae1800 | #ff9783 |

Qo‘shimcha ramp qiymatlari (agar app.css’da yo‘q bo‘lsa qo‘shing): accent-100 #fff2ef (tuzoq bloki foni), accent-400 #ff9783, accent-600 #dd2b0f (primary hover), accent-900 #4d170e (tuzoq matni), neutral-300 #d7d3d3 (yopiq katak, progress izi), neutral-400 #bab6b6 (o‘chiq toggle), neutral-500 #9b9797 (qulflangan raqam), neutral-600 #7d7979.

Shrift: Archivo (400/600/800/900), Noto Sans KR (400/500/700/900). `index.html` da 900 og‘irligini ham yuklang.

Tipografiya:
- Display raqam: Archivo 900, 88px (Bosh sahifa), 56px (Natija), letter-spacing −0.04/−0.03em, line-height 0.9–1.
- Koreyscha katta so‘z: Noto Sans KR 900 — 64px (kartochka), 72px (test), 56px (kirish).
- H2 30px / H3 25px / H4 20px — Archivo 800, line-height 1.12.
- Sarlavha (header): Archivo 800, 18px.
- Guruh sarlavhasi (kicker): 11px, 600, uppercase, letter-spacing .1em, `--color-muted`.
- Qator matni 15px; izoh 12–13px `--color-muted`.

O‘lchamlar: gutter 20px; header padding 12–16px × 20px; tabbar balandligi **64px** (`--tabbar-height` ni 64px ga o‘zgartiring); katta tugma 52px, ikkinchi darajali 48px, kichik 44px; ro‘yxat qatori min 52px (bo‘limlar 56–76px).

## Umumiy komponentlar (bir marta yasang)
- **AppHeader** — chap: orqaga strelka (22px, lucide `arrow-left`) yoki brend "Lug‘at"; sarlavha 18px/800; ostida 2px chiziq.
- **TabBar** — 5 ustunli grid: Bosh · Bo‘limlar · Takror · Mashq · Profil. Har katakda ikonka (20px) ustida, label 10px ostida, chapga tekis (padding-left 12px). Faol: `--color-accent`, 700, tepada 3px accent chiziq (`box-shadow: inset 0 3px 0`). Ikonkalar: lucide `home`, `layers`, `rotate-ccw`, `target`, `user`.
- **Button** — `.btn-primary` (accent fon, bg rang matn; hover accent-600, active accent-700), `.btn-secondary` (1px divider ramka; hover text 7% tint), `.btn-ghost` (accent matn). Barchasi 0 radius, Archivo 800, 14–15px, padding 0 16px, matn chapda, strelka (lucide `arrow-right` 18–20px) o‘ngda.
- **ListRow** — `grid-template-columns: 1fr auto`, min-height 52px, padding 0 20px, pastida 1px chiziq. O‘ngda qiymat (13px muted) + lucide `chevron-right` 18px.
- **SectionLabel** — kicker matn, padding 16px 20px 6px, ostida 2px chiziq.
- **Segmented** — 1px ramka, opsiyalar orasida 1px chiziq, padding 6–8px × 10–12px, 12–13px; tanlangan: accent fon, bg rang matn.
- **Toggle** — 40×22 kvadrat, ichida 16×16 kvadrat, padding 3px. Yoqiq: `--color-text` fon, tugmacha o‘ngda. O‘chiq: neutral-400, chapda.
- **Tag** — 11px, padding 3px 10px, 0 radius. neutral: #f8f4f4 fon / #444141 matn; accent: #fff2ef / #7c1405; outline: 1px accent.
- **Stepper (bo‘lim 4 qadam)** — 4 ustunli grid, ustunlar orasida 1px chiziq. Faol qadam: pastda 3px accent chiziq, raqam accent-700.

## Ekranlar

### 1a Kirish — `/kirish`
- Header: "Lug‘at" + tag "A · B · C".
- 2 ustunli grid (2px vertikal chiziq): 하다 / 있다 (Noto KR 900 56px; ikkinchisi accent), ostida "qilmoq" / "bor bo‘lmoq" 13px.
- H2 30px: "Koreys tili so‘zlari — o‘zbek tilida." Matn 15px: "Eng ko‘p ishlatiladigan so‘zlardan boshlab, 25 talik bo‘limlarda. Bo‘lim faqat testdan 80% bilan tugaydi."
- Tugmalar (52px): primary "Telegram orqali kirish →", secondary "Google orqali kirish →".
- Footer (2px chiziq ustida, 11px muted): "Manba: 국립국어원 한국어 학습용 어휘 목록 (KOGL 1)".

### 1b Bosh sahifa — `/`
- Header: "Lug‘at" + bugungi sana (13px muted).
- Blok 1 (2px ostida): kicker accent-700 "Bugun takrorlash"; katta raqam N (88px/900) + "ta karta / ~M daqiqa"; izoh "1- va 2-bo‘limdan muddati kelgan so‘zlar."; primary 52px "Boshlash →". Manba: `GET /api/today`.
- Blok 2: kicker "Joriy bo‘lim" + o‘ngda "12 / 25"; H3 "2-bo‘lim"; mavzu teglari (neutral).
- Stepper: 1 Jadval (✓) · 2 Karta (faol, surface fon, progress 3px) · 3 Mashq · 4 Test.
- Secondary 48px "Davom etish — kartochka →".
- Bo‘sh holat (karta yo‘q): raqam o‘rniga "Bugun takrorlash yo‘q" va keyingi vaqt.

### 1c Bo‘limlar — `/bolimlar`
- H3 "Bo‘limlar"; daraja tanlagich: 3 ustun (A · B · C), tanlangan accent fon; ostida 10px "33 bo‘lim" yoki "Tez orada".
- Qator gridi `64px 1fr auto`. Raqam Archivo 900 24px "01".
  - **Tugallangan**: "1-bo‘lim · 25 ta so‘z", mavzular 12px; o‘ngda "92%" + check.
  - **Ochiq**: surface fon, raqam accent; "12/25 boshlangan"; 150px progress (3px, iz neutral-300, to‘lgan accent); o‘ngda strelka.
  - **Navbatdagi qulflangan**: raqam neutral-500; qulf ikonka; "Avval 2-bo‘limni tugating"; secondary 40px tugma "Bilaman — testni topshirish" + o‘ngda "3/3 urinish" (11px).
  - **Qulflangan**: hammasi neutral-500, o‘ngda qulf.

### 1d Bo‘lim · Jadval — `/bolim/{id}` (1-qadam)
- Header: orqaga, "2-bo‘lim", o‘ngda taymer "10:00" (1px ramka, tabular-nums). Taymer variantlari 5/10/15.
- Stepper (1 Jadval faol).
- "Yopish:" + segmented: Hech · 한국어 · O‘zbekcha.
- Jadval: grid `36px 1fr 1fr`, sarlavha 11px uppercase + 2px chiziq; qator min 50px + 1px chiziq. Koreyscha 17px/700 + romanizatsiya 11px muted (sozlama yoqiq bo‘lsa). O‘zbekcha 14px.
- Yopilgan ustun kataklari: 24px balandlikdagi neutral-300 blok. Katakni bosish — shu katakni ochadi. So‘zni bosish — so‘z tafsiloti (talaffuz, misol, tuzoq).
- Pastda primary "Kartochkaga o‘tish →". Jadval ko‘rish takrorlash jadvalini o‘zgartirmaydi.

### 1e Kartochka — `/sessiya`
- Tepada: X, progress (4px, ink), "7 / 25".
- Karta bloki (2px tepa/past): tag-outline "KO → UZ", o‘ngda "fe’l · 2-bo‘lim"; so‘z 64px; romanizatsiya 15px muted; o‘ngda 48×48 secondary ikon tugma (lucide `volume-2`, TTS).
- **Javob yopiq**: "Ma’nosini eslang, keyin tekshiring." + primary 56px "Javobni ko‘rsatish" (lucide `eye`).
- **Javob ochiq**: tarjima 28px/800; "Misol" (KO 17px + UZ 14px); **O‘zbekcha tuzoq** bloki — fon accent-100, sarlavha 11px uppercase accent-700 + lucide `triangle-alert`, matn 14px accent-900. `trap_uz` null bo‘lsa blok ko‘rinmaydi.
- Baho: 3 ustunli grid (2px tepa chiziq): Qiyin · Yaxshi · Oson, har biri ostida keyingi vaqt (11px). O‘rtadagi (tavsiya) — ink fon, bg matn. Vaqtlar serverdan (`ladder-v1`).

### 1f Bo‘lim testi — `/bolim/{id}/test`
- Header: X, "2-bo‘lim · Yakuniy test", "14 / 25".
- 25 segmentli progress (6px, gap 2px): javob berilgan ink, joriy accent, qolgan neutral-300.
- Savol: "Bu so‘z nimani bildiradi?" (13px muted) + so‘z 72px. Turlar: WordMeaning / WordKorean / WordListening (spec §4.4).
- Variantlar: to‘liq kenglikdagi qatorlar, grid `44px 1fr`, harf A–D (800, 13px), matn 17px/600, min 60px, 1px chiziq. Tanlangan: ink fon, bg matn. To‘g‘ri/xato **ko‘rsatilmaydi**.
- Pastda: "O‘tish uchun 20 ta to‘g‘ri javob kerak. Javoblar oxirida ko‘rsatiladi." + primary "Keyingisi →".

### 1g Test natijasi
- 2 ustun: "To‘g‘ri 22/25" (56px) | accent fon "Ball 88%".
- O‘tdi: H4 "Bo‘lim tugadi. 3-bo‘lim ochildi." + izoh. Konfetti yo‘q.
- "Xato javoblar · N" ro‘yxati: grid `96px 1fr` — KO so‘z | to‘g‘ri javob (600) + foydalanuvchi javobi (chizilgan, muted).
- Tugmalar: primary "3-bo‘limni boshlash →", secondary "Xato N ta so‘zni takrorlash".
- O‘tmadi holati (hali chizilmagan): ball bloki accent emas, ink ramka; sarlavha "Hali emas — 20 ta kerak edi"; tugmalar "Xato so‘zlarni takrorlash" (primary) va "Testni qayta topshirish".

### 1h Mashq — `/mashq`
- H3 "Mashq" + "Ochilgan bo‘limlar so‘zlari bilan".
- Qatorlar (min 72px, grid `56px 1fr auto`): 01 Yozma mashq · 02 Qaysi so‘z to‘g‘ri? · 03 Talaffuz* · 04 Gap zinapoyasi* · 05 Zaif joylar* · 06 Xatolar daftari*. Premiumsiz foydalanuvchida * qatorlarda tag-accent "Premium"; bosilsa Premium sahifasi (API 403 `premium-required`).

### 1i Premium — `/premium`
- Kirish matni: "Barcha bo‘limlar, testlar va takrorlash **bepul**. Premium — talaffuz baholash, xatolar daftari va AI mashqlari."
- Tariflar 2 ustun: Oylik ₩5,000 | 3 oylik ₩12,000 (ink fon, "−20%" accent-400, "₩4,000 / oy"). Narxlar `GET /api/premium/plans`.
- To‘lov kodi: monospace 30px/700 "L-7K3Q" + ghost "Nusxalash".
- Bank / Hisob — konfiguratsiyadan; bo‘sh bo‘lsa "Tez orada".
- Promo-kod: input + secondary "Tekshirish"; natija accent-700 12px "Chegirma 10% · ₩10,800".
- Primary "Chekni Telegramda yuborish →".

### 2a Profil — o‘quvchi — `/profil`
Tartib (yuqoridan pastga), har guruh 2px chiziq bilan:
1. Avatar 56×56 kvadrat (ink fon, bosh harflar 22px/900, pastki chap burchakda) + ism 20px/800 + login teglari (Telegram, Google).
2. Tarif qatori: "Bepul" + primary "Premium" (premium bo‘lsa "Premium · 14-dekabrgacha", tugmasiz).
3. **O‘qish**: Statistika (o‘ngda "50 so‘z") · O‘qituvchi guruhi (o‘qituvchi ismi; guruh yo‘q bo‘lsa qator yashirin).
4. **Sozlamalar**: Mavzu (segmented Yorug‘/Qorong‘i/Qurilma → `data-theme`) · Lotincha o‘qilish (toggle) · Eslatma (faqat Telegram login bo‘lsa; o‘ngda "20:00").
5. **Ilova**: Telefonga o‘rnatish · Hisobni bog‘lash · Haqida (manba, litsenziya, maxfiylik).
6. Pastda alohida (2px tepa): secondary "Chiqish", ghost accent-700 "Hisobni o‘chirish" (tasdiqlash dialogi bilan).

### 2b Profil — admin/muharrir/o‘qituvchi
2a bilan bir xil, faqat avatar ostida **Boshqaruv** bloki qo‘shiladi (kicker accent-700, ostida 2px **ink** chiziq):
- 2 ustunli katak: **Admin panel** (surface fon; "3 ta to‘lov kutmoqda / 2 bo‘lim tekshiruvda"; strelka) | **O‘qituvchi paneli** ("2 guruh / 41 o‘quvchi").
- Faqat roliga mos katak ko‘rinadi (Editor → faqat "Kontent"ga havola; Teacher → faqat O‘qituvchi paneli).
- Rol teglari: Admin (tag-accent), Muharrir, O‘qituvchi (neutral).
- **Profilda tizim sozlamalari yo‘q.**

### 2c Admin panel — `/admin`
- **Diqqat talab qiladi** (kicker accent-700): qatorlar grid `40px 1fr auto` — raqam 20px/900 accent + matn 14px + chevron. Misollar: "To‘lov tasdiqlanishi kerak", "Bo‘lim tekshiruvda (A-3, A-4)", "Komissiya to‘lanmagan". Hech narsa bo‘lmasa blok yashirin.
- 2×3 katak gridi (1px ichki chiziqlar, min 104px): 01 Kontent · 02 Foydalanuvchilar · 03 To‘lovlar · 04 Promo va o‘qituvchilar · 05 Ko‘rsatkichlar · 06 Tizim. Har katakda kichik raqam (11px) tepada, sarlavha 16px/800 + izoh 12px pastda. Bo‘limlar spec §10 ga mos.

### 2d Admin → Tizim — `/admin/tizim`
KoreanAiTeacher `Pages/Settings.razor` dagi tizim bloklari shu yerga ko‘chadi:
- **AI xizmati**: segmented Standart / Anthropic / OpenAI (sozlanmagani disabled); izoh "Tanlangani javob bermasa, ikkinchisi ishlatiladi."; har provayder qatori: holat ("Ulangan · 412 ms" yoki accent-700 "API kaliti qo‘shilmagan") + ghost "Tekshirish".
- **Oylik sarf**: "$18.40" 28px/900 + "/ $50.00"; 6px progress (ink); "24 soatda N AI so‘rov · taxminiy hisob". Ogohlantirish chegarasida progress accent bo‘ladi.
- **Zaxira nusxa**: "Har kecha 04:00" + oxirgi tekshirilgan + secondary "Hozir"; "Telegram’ga haftalik" + oxirgi yuborilgan + secondary "Yuborish". Xato bo‘lsa qator ostida accent-700 matn.
- Vaqt zonasi (Asia/Seoul) · Qurilmani tekshirish →.

## Holatlar (har ekranda, UX06)
Yuklanmoqda (skelet: neutral-300 bloklar, matn joyida), bo‘sh, xato (ProblemDetails `detail` + "Qayta urinish" secondary), offline. Tugma bosilganda disabled (45% opacity).

## Interaktivlik (namunada ishlaydi)
- 1d: "Yopish" segmenti ustunni yopadi/ochadi.
- 1e: "Javobni ko‘rsatish" ⇄ baho tugmalari.
- 1f: variant tanlash.
- Animatsiya yo‘q; holat almashinuvi bir zumda.

## State
- Klientda faqat UI holati: yopilgan ustun, javob ochiq/yopiq, tanlangan variant, taymer.
- Qolgani serverdan: `/api/today`, `/api/levels/{level}/units`, `/api/units/{id}`, `/api/sessions`, `/api/units/{id}/tests`, `/api/me`, `/api/me/premium`, `/api/premium/plans`, `/api/admin/*`, `/api/ai/status`, `/api/ai/budget`, `/api/backups/status`.

## Ikonkalar
Lucide (inline SVG, `stroke="currentColor"`, stroke-width 2): home, layers, rotate-ccw, target, user, arrow-left, arrow-right, chevron-right, lock, check, x, volume-2, eye, triangle-alert.

## Fayllar
- `design/Lugat Screens.dc.html` — barcha ekranlar (1a–1i pastda, 2a–2d tepada). Brauzerda oching.
- `design/support.js`, `design/_ds/…/styles.css`, `_ds_bundle.js` — namunani ochish uchun kerak; ilovaga ko‘chirilmaydi.
- `PROMPT.md` — Claude Code’ga beriladigan tayyor buyruq.
- `screenshots/` — har ekranning PNG rasmi (720×1520, 2x): `1a-kirish.png` … `1i-premium.png`, `2a-profil-oquvchi.png`, `2b-profil-admin.png`, `2c-admin-panel.png`, `2d-admin-tizim.png`, hamda `overview.png` (umumiy ko‘rinish). 2a/2b/2d ichi scroll bo‘ladi — rasmda faqat yuqori qismi; to‘liq tartib yuqoridagi matnda. Ziddiyat bo‘lsa, README matni ustun.
