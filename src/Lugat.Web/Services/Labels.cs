using Lugat.Domain.Imports;

namespace Lugat.Web.Services;

/// <summary>Uzbek wording for statuses and warnings. Technical details stay out of the main flow (spec UX05).</summary>
public static class Labels
{
    public static string Of(ImportStatus status) => status switch
    {
        ImportStatus.Uploaded => "Yuklandi, navbatda",
        ImportStatus.Extracting => "So‘zlar ajratilmoqda…",
        ImportStatus.NeedsReview => "Tekshirish kerak",
        ImportStatus.Confirmed => "Tasdiqlandi",
        ImportStatus.Failed => "Xatolik",
        ImportStatus.Cancelled => "Bekor qilindi",
        _ => status.ToString(),
    };

    public static string Of(EntryStatus status) => status switch
    {
        EntryStatus.NeedsReview => "Tekshirilmagan",
        EntryStatus.Approved => "Tasdiqlangan",
        EntryStatus.Excluded => "Chiqarilgan",
        _ => status.ToString(),
    };

    public static IEnumerable<string> Of(EntryWarnings warnings)
    {
        if (warnings.HasFlag(EntryWarnings.MissingTranslation))
        {
            yield return "Tarjima topilmadi";
        }

        if (warnings.HasFlag(EntryWarnings.BrokenNumbering))
        {
            yield return "Raqamlash chalkash (masalan, “1.” yo‘q)";
        }

        if (warnings.HasFlag(EntryWarnings.MultiLineTranslation))
        {
            yield return "Tarjima bir necha qatordan yig‘ildi";
        }

        if (warnings.HasFlag(EntryWarnings.UnattachedFragment))
        {
            yield return "Yonidagi matn noaniq biriktirildi";
        }

        if (warnings.HasFlag(EntryWarnings.PossibleDuplicate))
        {
            yield return "Bu so‘z kitobda avval ham bor";
        }

        if (warnings.HasFlag(EntryWarnings.UnexpectedCharacters))
        {
            yield return "Kutilmagan belgilar";
        }

        if (warnings.HasFlag(EntryWarnings.MultipleSenses))
        {
            yield return "Bir necha ma’no";
        }
    }
}
