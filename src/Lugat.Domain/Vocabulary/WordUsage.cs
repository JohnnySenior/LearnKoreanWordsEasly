namespace Lugat.Domain.Vocabulary;

/// <summary>A Korean phrase with its natural Uzbek meaning, e.g. "문을 열다 — eshikni ochmoq".</summary>
public sealed record UsagePair(string Korean, string Uzbek);

/// <summary>
/// A "which word is right?" question (D-46): an Uzbek sentence, the Korean one with a gap, the right word as it stands
/// there and look-alikes an Uzbek speaker would pick by translating word for word (데려왔어요 / 가져왔어요).
/// </summary>
public sealed record UsageQuestion(string Uzbek, string Sentence, string Answer, List<string> Wrong, string ExplanationUz);

/// <summary>
/// How a word is used (D-46): common phrases, the trap for an Uzbek speaker ("biznes ochmoq" is not 열다) and
/// questions that contrast it with the word a word-for-word translation would give. Written once by the AI per word
/// and kept; <see cref="ContentVersion"/> ties it to the word's text, so an edited word gets a fresh one.
/// </summary>
public sealed class WordUsage
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid WordSenseId { get; set; }

    public int ContentVersion { get; set; }

    public List<UsagePair> Phrases { get; set; } = [];

    /// <summary>Where the Uzbek meaning leads astray, in Uzbek; null when there is no real trap.</summary>
    public string? TrapUz { get; set; }

    public UsagePair? Example { get; set; }

    public List<UsageQuestion> Questions { get; set; } = [];

    public string Provider { get; set; } = "";

    public string? Model { get; set; }

    public string? Warning { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
