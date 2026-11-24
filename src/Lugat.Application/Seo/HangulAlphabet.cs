namespace Lugat.Application.Seo;

/// <param name="Key">Stable Latin name of the letter: the address of its audio (/alifbo/ovoz/{Key}-…).</param>
/// <param name="Letter">The jamo (compatibility block, as it is written on its own).</param>
/// <param name="Name">Its Korean name: 기역, 니은…; a vowel is named by its syllable (아).</param>
/// <param name="Reading">Latin reading in the project's style (Revised Romanization, like the cards); "g / k" where the
/// sound differs at the start and at the end of a syllable.</param>
/// <param name="Sample">A syllable that says the letter: a lone jamo gets empty audio from Azure (L-62).</param>
/// <param name="NoteUz">A short Uzbek note on the sound.</param>
public sealed record HangulLetter(string Key, string Letter, string Name, string Reading, string Sample, string NoteUz);

/// <param name="Parts">The jamo, in writing order.</param>
public sealed record HangulSyllable(IReadOnlyList<string> Parts, string Syllable, string Reading, string NoteUz);

/// <summary>
/// The Korean alphabet for the public page /alifbo (L-93), and the one table of jamo names: the introduction voice says a
/// lone letter by its name too (L-62, <c>IntroductionAudioService.Speakable</c>). The sound notes were written with the
/// content model and are for Johnny to check; <c>HangulRules</c> stays the source of the pronunciation rules.
/// </summary>
public static class HangulAlphabet
{
    public static readonly IReadOnlyList<HangulLetter> Consonants =
    [
        new("g", "ㄱ", "기역", "g / k", "가", "Bo‘g‘in boshida g bilan k orasidagi yumshoq tovush, oxirida qisqa k."),
        new("n", "ㄴ", "니은", "n", "나", "O‘zbekcha n."),
        new("d", "ㄷ", "디귿", "d / t", "다", "Boshida d bilan t orasida, bo‘g‘in oxirida qisqa t."),
        new("r", "ㄹ", "리을", "r / l", "라", "Unlidan oldin tilni bir urib aytiladigan yengil r, bo‘g‘in oxirida l."),
        new("m", "ㅁ", "미음", "m", "마", "O‘zbekcha m."),
        new("b", "ㅂ", "비읍", "b / p", "바", "Boshida b bilan p orasida, bo‘g‘in oxirida qisqa p."),
        new("s", "ㅅ", "시옷", "s", "사", "O‘zbekcha s; ㅣ oldida sh ga yaqin: 시 — «shi»."),
        new("ng", "ㅇ", "이응", "— / ng", "아", "Bo‘g‘in boshida o‘qilmaydi, unli shu bilan boshlanadi. Oxirida «tong» dagi ng."),
        new("j", "ㅈ", "지읒", "j", "자", "«Jo‘ja» dagi j ga yaqin, yumshoqroq aytiladi."),
        new("ch", "ㅊ", "치읓", "ch", "차", "Ch, ortidan havo kuchli chiqadi."),
        new("k", "ㅋ", "키읔", "k", "카", "K, ortidan havo kuchli chiqadi (ㄱ dan farqi shu)."),
        new("t", "ㅌ", "티읕", "t", "타", "T, ortidan havo kuchli chiqadi (ㄷ dan farqi shu)."),
        new("p", "ㅍ", "피읖", "p", "파", "P, ortidan havo kuchli chiqadi (ㅂ dan farqi shu)."),
        new("h", "ㅎ", "히읗", "h", "하", "O‘zbekcha h."),
    ];

    public static readonly IReadOnlyList<HangulLetter> Vowels =
    [
        new("a", "ㅏ", "아", "a", "아", "O‘zbekcha a."),
        new("ya", "ㅑ", "야", "ya", "야", "Ya."),
        new("eo", "ㅓ", "어", "eo", "어", "O bilan a orasidagi tovush: og‘iz ochiq, lablar yumaloqlanmaydi."),
        new("yeo", "ㅕ", "여", "yeo", "여", "Y + ㅓ: «yo» ga o‘xshaydi, lekin lablar yumaloqlanmaydi."),
        new("o", "ㅗ", "오", "o", "오", "O‘zbekcha o, lablar yumaloq."),
        new("yo", "ㅛ", "요", "yo", "요", "Yo, lablar yumaloq."),
        new("u", "ㅜ", "우", "u", "우", "O‘zbekcha u."),
        new("yu", "ㅠ", "유", "yu", "유", "Yu."),
        new("eu", "ㅡ", "으", "eu", "으", "Lablar yumaloqlanmay, cho‘zilib aytiladigan qisqa ı: «qiz» dagi i ga yaqin."),
        new("i", "ㅣ", "이", "i", "이", "O‘zbekcha i."),
    ];

    public static readonly IReadOnlyList<HangulLetter> DoubleConsonants =
    [
        new("kk", "ㄲ", "쌍기역", "kk", "까", "ㄱ ning tarang shakli: bo‘g‘iz siqilib, havosiz qattiq k."),
        new("tt", "ㄸ", "쌍디귿", "tt", "따", "ㄷ ning tarang shakli: havosiz qattiq t."),
        new("pp", "ㅃ", "쌍비읍", "pp", "빠", "ㅂ ning tarang shakli: havosiz qattiq p."),
        new("ss", "ㅆ", "쌍시옷", "ss", "싸", "ㅅ ning tarang shakli: kuchli, cho‘ziqroq s."),
        new("jj", "ㅉ", "쌍지읒", "jj", "짜", "ㅈ ning tarang shakli: havosiz qattiq j (ch ga yaqin)."),
    ];

    public static readonly IReadOnlyList<HangulLetter> ComplexVowels =
    [
        new("ae", "ㅐ", "애", "ae", "애", "O‘zbekcha e, og‘iz kengroq ochiladi."),
        new("yae", "ㅒ", "얘", "yae", "얘", "Ye (ㅐ ning y li shakli)."),
        new("e", "ㅔ", "에", "e", "에", "O‘zbekcha e. Bugungi talaffuzda ㅐ bilan deyarli farqlanmaydi."),
        new("ye", "ㅖ", "예", "ye", "예", "Ye."),
        new("wa", "ㅘ", "와", "wa", "와", "ㅗ + ㅏ: wa."),
        new("wae", "ㅙ", "왜", "wae", "왜", "ㅗ + ㅐ: we."),
        new("oe", "ㅚ", "외", "oe", "외", "Bugungi talaffuzda we, ㅙ ga yaqin."),
        new("wo", "ㅝ", "워", "wo", "워", "ㅜ + ㅓ: wo."),
        new("we", "ㅞ", "웨", "we", "웨", "ㅜ + ㅔ: we."),
        new("wi", "ㅟ", "위", "wi", "위", "ㅜ + ㅣ: wi."),
        new("ui", "ㅢ", "의", "ui", "의", "ㅡ + ㅣ: so‘z boshida ui, boshqa joyda ko‘pincha i."),
    ];

    public static readonly IReadOnlyList<HangulSyllable> Syllables =
    [
        new(["ㄱ", "ㅏ"], "가", "ga", "ㅏ kabi tik unli undoshning o‘ng tomoniga yoziladi."),
        new(["ㄴ", "ㅗ"], "노", "no", "ㅗ kabi yotiq unli undoshning ostiga yoziladi."),
        new(["ㅂ", "ㅏ", "ㅂ"], "밥", "bap", "Uchinchi harf pastga tushadi: bu 받침, bo‘g‘in oxiridagi undosh."),
    ];

    /// <summary>What 받침 is, under the syllable examples.</summary>
    public const string FinalNoteUz = "Bo‘g‘in oxiridagi undosh 받침 deyiladi va bo‘g‘inning pastiga yoziladi. Oxirida undosh to‘liq ochilmaydi: ㄱ — k, ㅂ — p, ㄷ, ㅅ, ㅈ — qisqa t kabi eshitiladi. Shuning uchun 밥 «bap» o‘qiladi.";

    /// <summary>Every letter on the page, in page order.</summary>
    public static IEnumerable<HangulLetter> All => Consonants.Concat(Vowels).Concat(DoubleConsonants).Concat(ComplexVowels);

    /// <summary>
    /// The names of the compatibility consonants ㄱ…ㅎ (U+3131–U+314E), clusters included, in code-point order: how a
    /// teacher says a lone letter (L-62).
    /// </summary>
    public static readonly IReadOnlyList<string> ConsonantNames =
        ["기역", "쌍기역", "기역시옷", "니은", "니은지읒", "니은히읗", "디귿", "쌍디귿", "리을", "리을기역", "리을미음", "리을비읍", "리을시옷", "리을티읕",
         "리을피읖", "리을히읗", "미음", "비읍", "쌍비읍", "비읍시옷", "시옷", "쌍시옷", "이응", "지읒", "쌍지읒", "치읓", "키읔", "티읕", "피읖", "히읗"];

    /// <summary>The spoken name of a lone jamo (ㄹ → 리을, ㅏ → 아), or null for any other character.</summary>
    public static string? SpokenName(char c) =>
        c is >= 'ㄱ' and <= 'ㅎ' ? ConsonantNames[c - 'ㄱ']
        : c is >= 'ㅏ' and <= 'ㅣ' ? ((char)(0xAC00 + 11 * 28 * 21 + (c - 0x314F) * 28)).ToString() // ㅏ → 아 (ㅇ + vowel)
        : null;

    /// <summary>The recordings the page plays: the sample syllable and, where it differs, the letter's name.</summary>
    public static IReadOnlyList<(string Key, string Text)> Recordings { get; } =
    [
        .. All.SelectMany(l => l.Name == l.Sample
            ? new[] { (Key: l.Key + "-1", Text: l.Sample) }
            : [(Key: l.Key + "-1", Text: l.Sample), (Key: l.Key + "-2", Text: l.Name)]),
        .. Syllables.Select((s, i) => (Key: $"bogin-{i + 1}", Text: s.Syllable)),
    ];
}
