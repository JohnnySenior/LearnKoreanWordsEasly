using Lugat.Domain.Ai;

namespace Lugat.Application.Ai;

/// <summary>
/// Server-side AI settings (D-21). Keys come only from hosting variables such as <c>Ai__Anthropic__ApiKey</c>
/// and never reach the browser, the repo or the logs (spec SEC05, ARCH04).
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Used when the learner has not chosen a service in settings.</summary>
    public AiProviderKind DefaultProvider { get; set; } = AiProviderKind.Anthropic;

    /// <summary>When the chosen service is down, try the other one if it is configured.</summary>
    public bool FallbackToOther { get; set; } = true;

    /// <summary>AI calls per Premium learner per day (spec §7.4, BUD05). Learners without Premium get none.</summary>
    public int MaxCallsPerUserPerDay { get; set; } = 60;

    /// <summary>Editors and admins draft the shared catalog with AI (spec §2.4): a level is about 100 calls.</summary>
    public int MaxCallsPerEditorPerDay { get; set; } = 300;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>All providers together; shorter than the client's two-minute deadline.</summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(100);

    public AiProviderOptions Anthropic { get; set; } = new() { Model = "claude-sonnet-5", BaseUrl = "https://api.anthropic.com/" };

    public AiProviderOptions OpenAi { get; set; } = new() { Model = "gpt-5-mini", BaseUrl = "https://api.openai.com/" };

    public AiProviderOptions For(AiProviderKind kind) => kind == AiProviderKind.Anthropic ? Anthropic : OpenAi;
}

public sealed class AiProviderOptions
{
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "";

    public string BaseUrl { get; set; } = "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}

/// <summary>
/// A request for a JSON object that matches <see cref="JsonSchema"/>.
/// <see cref="UserContent"/> is learner data: providers must pass it as data, never as instructions (spec AI03).
/// </summary>
/// <param name="Attachments">Files the model should read, e.g. one book page as PDF (sent before the text).</param>
/// <param name="Timeout">Overrides <see cref="AiOptions.Timeout"/> for slow requests such as reading a scanned page.</param>
public sealed record AiJsonRequest(
    string Feature,
    string Instructions,
    string UserContent,
    string SchemaName,
    string JsonSchema,
    int MaxOutputTokens,
    IReadOnlyList<AiAttachment>? Attachments = null,
    TimeSpan? Timeout = null);

public sealed record AiAttachment(string MediaType, string FileName, byte[] Content);

public sealed record AiJsonResponse(string Json, string Model, int InputTokens, int OutputTokens);

/// <summary>A provider failure. <see cref="Transient"/> ones (timeout, 429, 5xx) may be retried on the other provider.</summary>
public sealed class AiProviderException(string message, bool transient) : Exception(message)
{
    public bool Transient { get; } = transient;
}

/// <summary>One AI service. Implementations live in Infrastructure (spec §11: ILearningAi adapters).</summary>
public interface IAiProvider
{
    AiProviderKind Kind { get; }

    bool IsConfigured { get; }

    string Model { get; }

    Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct);
}

