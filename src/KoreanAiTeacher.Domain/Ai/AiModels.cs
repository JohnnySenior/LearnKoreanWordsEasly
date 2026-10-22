namespace KoreanAiTeacher.Domain.Ai;

/// <summary>AI services the app can use. Keys live only in server configuration (spec SEC05, D-21).</summary>
public enum AiProviderKind
{
    Anthropic,
    OpenAi,

    /// <summary>Microsoft Azure Speech: pronunciation only (D-31), never a text AI.</summary>
    Azure,
}

/// <summary>One AI call, successful or not, for cost tracking (spec BUD03). No prompt or answer text is kept here.</summary>
public sealed class AiUsage
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>What the call was for, e.g. "sentence-check" or "ping".</summary>
    public string Feature { get; set; } = "";

    public AiProviderKind Provider { get; set; }

    public string Model { get; set; } = "";

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int DurationMs { get; set; }

    /// <summary>tokens, characters, or audio-ms. Old speech rows use InputTokens but are identifiable by Feature.</summary>
    public string Unit { get; set; } = "tokens";

    public decimal? EstimatedUsd { get; set; }

    public DateOnly? PriceAsOf { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>Short reason when the call failed; never contains keys.</summary>
    public string? Error { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>Outcome of an AI check (spec API02). A timeout or provider failure is never the learner's result.</summary>
public enum AiCheckStatus
{
    Completed,

    /// <summary>"Tekshirish kerak": the answer did not pass validation or the model was unsure (spec AI01).</summary>
    NeedsReview,

    Failed,
}

/// <summary>A sentence the learner wrote with a target word and the AI verdict on it (spec PR05, PR08).</summary>
public sealed class SentenceCheck
{
    /// <summary>Chosen by the client and reused on retries, so a retry never pays twice (spec API01).</summary>
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid? WordSenseId { get; set; }

    /// <summary>Set when the sentence practises a grammar point instead of a word (D-26).</summary>
    public Guid? GrammarItemId { get; set; }

    public string Target { get; set; } = "";

    public string TargetMeaning { get; set; } = "";

    public string Answer { get; set; } = "";

    public AiCheckStatus Status { get; set; }

    /// <summary>Validated result as JSON (see Application.Ai.SentenceCheckResult); null when the call failed.</summary>
    public string? ResultJson { get; set; }

    public AiProviderKind? Provider { get; set; }

    public string? Model { get; set; }

    /// <summary>Prompt version, part of what makes a result reproducible (spec §12 AiArtifact).</summary>
    public string PromptVersion { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}

