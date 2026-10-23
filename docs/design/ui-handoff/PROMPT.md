# Claude Code uchun buyruq (shuni nusxalab yuboring)

```
Repo: JohnnySenior/LearnKoreanWordsEasly.

design_handoff_lugat_ui/ papkasini repo ildiziga qo‘ydim. Avval AGENTS.md, docs/SPECIFICATION.md
va design_handoff_lugat_ui/README.md ni to‘liq o‘qing.

Vazifa: README.md dagi 13 ta ekranni (1a–1i, 2a–2d) Lugat.Web (Blazor WebAssembly) da
shu dizaynga moslab yasang. design/Lugat Screens.dc.html — faqat vizual namuna (brauzerda oching),
HTML/JS ni ko‘chirmang; Razor komponentlari va src/Lugat.Web/wwwroot/css/app.css dagi mavjud
CSS o‘zgaruvchilari bilan qayta quring.

Qoidalar:
- Biznes mantiq faqat serverda (spec §14). Ekrandagi raqamlar API’dan keladi, mock emas.
- Hali API’si yo‘q ekran uchun avval sahifa va komponentni yasang, ma’lumot joyini
  "Tez orada" yoki bo‘sh holat bilan ko‘rsating va CHECKLIST.md ga yozing.
- Har bir ekranni alohida kichik commit/PR qiling; dotnet build va dotnet test yashil bo‘lsin.
- Profil qayta tuzilishi (2a–2d): KoreanAiTeacher’dagi Settings.razor ichidagi AI, sarf,
  zaxira bloklarini /admin/tizim sahifasiga ko‘chiring; /profil faqat shaxsiy narsalar.
- Burchaklar 0 px, tugma matni chapga tekislangan, 2px ajratuvchi chiziqlar — README’dagi
  "Dizayn qoidalari" bo‘limiga qat’iy amal qiling.
- 360 px kenglikda yonlama scroll bo‘lmasin, tegish nishoni ≥ 44 px.

Tugagach: qaysi ekranlar tayyor, qaysilari mock ma’lumotda qoldi — ro‘yxat bering.
```
