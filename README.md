# KoreanAiTeacher — TOPIK yordamchi

O‘qituvchi bergan koreyscha so‘zlar va grammatikani eslab qolish, og‘zaki qaytarish va gaplarda ishlatishga yordam beradigan **Telegram Mini App**. Maqsad — TOPIK II (3–4 daraja) imtihoniga tayyorgarlikni daftar va o‘qituvchi bilan ishlashga qo‘shimcha sifatida qo‘llab-quvvatlash.

> **Holat (2026-10-05):** import, kartochkalar, grammatika, yozma/og‘zaki mashq, kitob sinovlari, so‘z qo‘llanishi va TOPIK yozish mavjud. Ayrim oqimlar real iPhone Telegram’da sinalgan; Android, real AI bilan insho sifati va to‘liq recovery drill hali ochiq. Joriy dalillar va bajarilmagan v1 bandlari: [docs/CHECKLIST.md](docs/CHECKLIST.md).

## Asosiy hujjat

To‘liq mahsulot va texnik talablar: **[docs/SPECIFICATION.md](docs/SPECIFICATION.md)** (v1.0, 2026-09-28).
Asl DOCX nusxa arxiv sifatida: [docs/original/](docs/original/).

Spetsifikatsiya yagona manba hisoblanadi. Talab o‘zgarsa, shu faylda sana va sabab bilan yangilanadi (spec §17, AG04).

## Ishga tushirish

.NET 10 SDK va PostgreSQL kerak (sozlash: [AGENTS.md](AGENTS.md)).

```bash
dotnet test
dotnet run --project src/KoreanAiTeacher.Api   # http://localhost:5248
```

Agentlar va dasturchilar uchun qoidalar: [AGENTS.md](AGENTS.md). Texnik qarorlar: [docs/DECISIONS.md](docs/DECISIONS.md). Sozlamalar namunasi: [.env.example](.env.example).

## Qisqacha

- **Foydalanuvchilar (v1):** 2 kishi, ikkalasi Telegram orqali kiradi; har birining natijalari alohida.
- **Material:** lug‘at PDF (Musaev F., TOPIK o‘rta daraja lug‘ati) va grammatika PDF (Hapgyeok Recipe). PDF import → tekshirish → tasdiqlash.
- **Dars tartibi:** haftasiga 180 so‘z (6 jadval × 30), seshanba/payshanba/shanba 23:00 (Asia/Seoul), har darsga 60 so‘z + 20 grammatika bandi.
- **Mashq:** ikki yo‘nalishli kartochkalar (KO→UZ, UZ→KO), takrorlash jadvali, yozma/tinglash/og‘zaki mashq, AI gap tekshiruvi (avval ishora, keyin javob), sinovlar.
- **Interfeys:** o‘zbekcha, sokin va sodda mobil dizayn.
- **Byudjet maqsadi:** oyiga taxminan 25 USD (hosting + AI + ovoz).

## Texnik asos

| Qism | Tanlov |
|---|---|
| Backend | C#, .NET 10 LTS, ASP.NET Core Web API (modular monolith) |
| Frontend | Blazor WebAssembly + kichik JS interop (Telegram SDK, mikrofon, audio) |
| Ma’lumot | PostgreSQL + EF Core |
| Fon ishlari | ASP.NET Core worker, bazadagi job/outbox |
| AI/ovoz | Claude/OpenAI matn adapterlari, Azure/OpenAI ovoz adapterlari |
| Testlar | xUnit, database integratsiya testlari, real Telegram qurilmalarida sinov |

Loyihalar: `Domain`, `Contracts`, `Application`, `Infrastructure`, `Api`, `Web` va testlar.

## Bajarish bosqichlari

1. Texnik tekshiruv: Blazor Telegram ichida, mikrofon/audio, STT sifati, narx va hosting.
2. Asos va import: kirish, DB, ikki PDF importi, dastlabki 60 so‘z va 20 grammatika.
3. Takrorlash: ikki yo‘nalish, dars rejasi, sessiyani davom ettirish.
4. AI va mashq: misollar, gap tekshirish, grammatika suhbati, audio, sinovlar.
5. Yakuniy tekshiruv: eslatmalar, ulashish, xarajat nazorati, backup, pilot.

Batafsil qabul mezonlari: spec §16–17.

