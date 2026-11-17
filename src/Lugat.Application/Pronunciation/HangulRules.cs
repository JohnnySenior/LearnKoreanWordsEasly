using System.Text;

namespace Lugat.Application.Pronunciation;

/// <summary>One standard pronunciation rule seen in a word: its Uzbek and Korean name and what changed where.</summary>
public sealed record PronunciationRule(string Key, string NameUz, string NameKo, string Detail);

/// <summary>
/// L-83: names the standard pronunciation rules (표준 발음법) that turn a word's spelling into its reviewed reading.
/// The reading is already checked by a person, so nothing is predicted: each change between spelling and reading is
/// matched to a rule at the syllable joint where it happens. When a change fits no rule (or the syllables or vowels
/// do not line up), <see cref="Explain"/> says so, and the editor is asked to look at the word.
/// </summary>
public static class HangulRules
{
    public sealed record Result(IReadOnlyList<PronunciationRule> Rules, bool Complete);

    // Jamo of the 19 initials and 28 finals (index 0 of the finals: none), as Unicode compatibility letters.
    private const string Initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private static readonly string[] Finals =
        ["", "ㄱ", "ㄲ", "ㄱㅅ", "ㄴ", "ㄴㅈ", "ㄴㅎ", "ㄷ", "ㄹ", "ㄹㄱ", "ㄹㅁ", "ㄹㅂ", "ㄹㅅ", "ㄹㅌ", "ㄹㅍ", "ㄹㅎ", "ㅁ", "ㅂ", "ㅂㅅ", "ㅅ", "ㅆ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ"];

    private static readonly Dictionary<string, (string Uz, string Ko)> Names = new()
    {
        ["liaison"] = ("Ulanish", "연음"),
        ["neutral"] = ("Bo‘g‘in oxiridagi undosh", "음절의 끝소리 규칙"),
        ["cluster"] = ("Ikki undoshdan biri", "자음군 단순화"),
        ["nasal"] = ("Burunlashish", "비음화"),
        ["rton"] = ("ㄹ ning ㄴ ga aylanishi", "ㄹ의 비음화"),
        ["lateral"] = ("ㄹ ga aylanish", "유음화"),
        ["tense"] = ("Taranglashish", "경음화"),
        ["aspirate"] = ("Kuchli nafas", "격음화"),
        ["hdrop"] = ("ㅎ ning tushib qolishi", "ㅎ 탈락"),
        ["palatal"] = ("ㅈ / ㅊ ga aylanish", "구개음화"),
        ["ninsert"] = ("ㄴ qo‘shilishi", "ㄴ 첨가"),
        ["ui"] = ("ㅢ ning soddalashishi", "이중 모음 ㅢ"),
        ["ye"] = ("ㅖ ning ㅔ deb aytilishi", "이중 모음 ㅖ"),
    };

    private sealed record Syllable(char Text, int L, int V, int T);

    /// <summary>The rules between <paramref name="korean"/> and its <paramref name="reading"/>; empty when they are the same.</summary>
    public static Result Explain(string korean, string? reading)
    {
        if (string.IsNullOrWhiteSpace(reading))
        {
            return new([], false);
        }

        var spelled = Syllables(korean);
        var read = Syllables(reading);
        if (spelled is null || read is null || spelled.Count != read.Count)
        {
            return new([], false);
        }

        var rules = new List<PronunciationRule>();
        var complete = true;
        for (var i = 0; i < spelled.Count; i++)
        {
            var (s, r) = (spelled[i], read[i]);
            if (s.V != r.V && !Vowel(s, r, i, rules))
            {
                complete = false;
            }

            if (i == 0 && s.L != r.L)
            {
                complete = false; // the first sound of a word does not change
            }
        }

        for (var i = 0; i < spelled.Count; i++)
        {
            var hasNext = i + 1 < spelled.Count;
            var final = Finals[spelled[i].T];
            var next = hasNext ? Initials[spelled[i + 1].L].ToString() : null;
            var readFinal = Finals[read[i].T];
            var readNext = hasNext ? Initials[read[i + 1].L].ToString() : null;
            if (final == readFinal && next == readNext)
            {
                continue;
            }

            var joint = hasNext ? $"{spelled[i].Text}{spelled[i + 1].Text}" : spelled[i].Text.ToString();
            var nextVowel = hasNext ? spelled[i + 1].V : -1;
            if (!Joint(final, next, readFinal, readNext, nextVowel, joint, rules))
            {
                complete = false;
            }
        }

        return new(rules.DistinctBy(r => (r.Key, r.Detail)).ToList(), complete);
    }

    /// <summary>Walks the rules at one joint, changing (final, next) step by step, and checks it reaches the reading.</summary>
    private static bool Joint(string final, string? next, string readFinal, string? readNext, int nextVowel, string joint, List<PronunciationRule> rules)
    {
        var f = final;
        var n = next;
        var found = new List<PronunciationRule>();
        void Add(string key, string detail) => found.Add(Rule(key, $"{joint}: {detail}"));

        // ㄴ insertion: a vowel 이/야/여/요/유 after a consonant gets ㄴ (꽃잎 → 꼰닙, 무슨 일 → 무슨닐).
        if (n == "ㅇ" && f.Length > 0 && f != "ㅇ" && readNext is "ㄴ" or "ㄹ" && nextVowel is 2 or 6 or 12 or 17 or 20)
        {
            n = "ㄴ";
            Add("ninsert", "keyingi unli oldidan ㄴ qo‘shiladi");
        }

        // ㅎ at the end of the first syllable.
        if (f.EndsWith('ㅎ') && n is not null)
        {
            var rest = f[..^1];
            if (n is "ㄱ" or "ㄷ" or "ㅈ")
            {
                var aspirated = n switch { "ㄱ" => "ㅋ", "ㄷ" => "ㅌ", _ => "ㅊ" };
                Add("aspirate", $"ㅎ + {n} → {aspirated}");
                (f, n) = (rest, aspirated);
            }
            else if (n == "ㅇ")
            {
                Add("hdrop", "ㅎ unli oldidan aytilmaydi");
                f = rest;
            }
            else if (n == "ㅅ")
            {
                Add("tense", "ㅎ + ㅅ → ㅆ");
                (f, n) = (rest, "ㅆ");
            }
            else if (n == "ㄴ")
            {
                Add("nasal", "ㅎ ㄴ oldidan ㄴ bo‘ladi");
                f = rest.Length > 0 ? rest : "ㄴ";
            }
        }

        // ㄱ ㄷ ㅂ ㅈ (alone or in a pair) before ㅎ: one strong breathed sound (축하 → 추카).
        if (n == "ㅎ" && f.Length > 0 && Aspirated(f[^1]) is { } strong && readNext != "ㅎ")
        {
            Add("aspirate", $"{Representative(f[^1].ToString())} + ㅎ → {strong}");
            (f, n) = (f[..^1], strong);
        }

        // Liaison: the last final moves before the next vowel; ㄷ/ㅌ before 이 become ㅈ/ㅊ.
        if (n == "ㅇ" && f.Length > 0 && f != "ㅇ")
        {
            var moved = f[^1].ToString();
            if (moved is "ㄷ" or "ㅌ" && nextVowel == 20 && readNext is "ㅈ" or "ㅊ")
            {
                var palatal = moved == "ㄷ" ? "ㅈ" : "ㅊ";
                Add("palatal", $"{moved} + 이 → {palatal}");
                moved = palatal;
            }
            else
            {
                Add("liaison", $"{moved} keyingi unliga ulanib o‘qiladi");
            }

            (f, n) = (f[..^1], moved);
        }

        // A pair of finals keeps one (닭 → 닥, 앉다 → 안따, 읽고 → 일꼬): whichever leads to the reading.
        if (f.Length == 2)
        {
            foreach (var kept in new[] { f[0].ToString(), f[1].ToString() })
            {
                var after = new List<PronunciationRule>(found) { Rule("cluster", $"{joint}: {f[0]}{f[1]} dan faqat {kept} aytiladi") };
                if (Rest(kept, n, readFinal, readNext, joint, after))
                {
                    rules.AddRange(after);
                    return true;
                }
            }

            return false;
        }

        if (Rest(f, n, readFinal, readNext, joint, found))
        {
            rules.AddRange(found);
            return true;
        }

        return false;
    }

    /// <summary>The rules after liaison: the final as said, ㄹ/ㄴ changes, nasal and tense sounds.</summary>
    private static bool Rest(string f, string? n, string readFinal, string? readNext, string joint, List<PronunciationRule> found)
    {
        void Add(string key, string detail) => found.Add(Rule(key, $"{joint}: {detail}"));

        if (f.Length == 1 && Representative(f) != f && n != "ㅇ")
        {
            Add("neutral", $"{f} oxirida {Representative(f)} deb aytiladi");
            f = Representative(f);
        }

        // ㄹ after ㅁ ㅇ (and ㄱ ㅂ) is said ㄴ (종로 → 종노, 독립 → 동닙).
        if (n == "ㄹ" && f is "ㅁ" or "ㅇ" or "ㄱ" or "ㅂ" && readNext == "ㄴ")
        {
            Add("rton", $"{f} dan keyin ㄹ → ㄴ");
            n = "ㄴ";
        }

        // ㄴ next to ㄹ becomes ㄹ (신라 → 실라, 연락 → 열락, 설날 → 설랄).
        if (f == "ㄴ" && n == "ㄹ" && readFinal == "ㄹ")
        {
            Add("lateral", "ㄴ + ㄹ → ㄹ + ㄹ");
            f = "ㄹ";
        }
        else if (f == "ㄹ" && n == "ㄴ" && readNext == "ㄹ")
        {
            Add("lateral", "ㄹ + ㄴ → ㄹ + ㄹ");
            n = "ㄹ";
        }

        // ㄱ ㄷ ㅂ before ㄴ ㅁ: ㅇ ㄴ ㅁ (국물 → 궁물, 입니다 → 임니다).
        if (f is "ㄱ" or "ㄷ" or "ㅂ" && n is "ㄴ" or "ㅁ")
        {
            var nasal = f switch { "ㄱ" => "ㅇ", "ㄷ" => "ㄴ", _ => "ㅁ" };
            Add("nasal", $"{f} {n} oldidan {nasal} bo‘ladi");
            f = nasal;
        }

        // A plain consonant said tense (학교 → 학꾜, 앉다 → 안따, 발달 → 발딸).
        if (n is not null && readNext is not null && n != readNext && Tense(n) == readNext)
        {
            Add("tense", $"{n} → {readNext}");
            n = readNext;
        }

        return f == readFinal && n == readNext;
    }

    private static bool Vowel(Syllable s, Syllable r, int index, List<PronunciationRule> rules)
    {
        // ㅢ: 희망 → 히망 (after a consonant), 회의 → 회이 (not first); ㅖ after a consonant: 계속 → 게속.
        if (s.V == 19 && (r.V == 20 || r.V == 5) && (s.L != 11 || index > 0))
        {
            rules.Add(Rule("ui", $"{s.Text}: ㅢ → {(r.V == 20 ? "ㅣ" : "ㅔ")}"));
            return true;
        }

        if (s.V == 7 && r.V == 5 && s.L != 11 && s.L != 5)
        {
            rules.Add(Rule("ye", $"{s.Text}: ㅖ → ㅔ"));
            return true;
        }

        return false;
    }

    private static PronunciationRule Rule(string key, string detail) => new(key, Names[key].Uz, Names[key].Ko, detail);

    /// <summary>How a final is said at the end of a syllable: ㄱ ㄴ ㄷ ㄹ ㅁ ㅂ ㅇ.</summary>
    private static string Representative(string final) => final switch
    {
        "ㄲ" or "ㅋ" => "ㄱ",
        "ㅅ" or "ㅆ" or "ㅈ" or "ㅊ" or "ㅌ" or "ㅎ" => "ㄷ",
        "ㅍ" => "ㅂ",
        _ => final,
    };

    private static string? Aspirated(char final) => final switch
    {
        'ㄱ' => "ㅋ",
        'ㄷ' or 'ㅅ' => "ㅌ",
        'ㅂ' => "ㅍ",
        'ㅈ' => "ㅊ",
        _ => null,
    };

    private static string Tense(string initial) => initial switch
    {
        "ㄱ" => "ㄲ",
        "ㄷ" => "ㄸ",
        "ㅂ" => "ㅃ",
        "ㅅ" => "ㅆ",
        "ㅈ" => "ㅉ",
        _ => initial,
    };

    /// <summary>Hangul syllables only (spaces are left out); null when anything else is in the text.</summary>
    private static List<Syllable>? Syllables(string text)
    {
        var list = new List<Syllable>();
        foreach (var c in text.Normalize(NormalizationForm.FormC))
        {
            if (c == ' ')
            {
                continue;
            }

            if (c is < '가' or > '힣')
            {
                return null;
            }

            var code = c - '가';
            list.Add(new(c, code / 588, code % 588 / 28, code % 28));
        }

        return list.Count == 0 ? null : list;
    }
}
