using KoreanAiTeacher.Application.Ai;
using KoreanAiTeacher.Domain.Ai;

namespace KoreanAiTeacher.Api.Tests.Ai;

/// <summary>Stands in for Claude or ChatGPT. Never calls the network.</summary>
public sealed class FakeAiProvider(AiProviderKind kind, bool configured = true) : IAiProvider
{
    public AiProviderKind Kind { get; } = kind;

    public bool IsConfigured { get; set; } = configured;

    public string Model => Kind == AiProviderKind.Anthropic ? "fake-claude" : "fake-gpt";

    public List<AiJsonRequest> Requests { get; } = [];

    /// <summary>What the next calls return; throws when set to an exception.</summary>
    public Func<AiJsonRequest, string> Answer { get; set; } = _ => Good;

    public Exception? Failure { get; set; }

    public Task<AiJsonResponse> CompleteJsonAsync(AiJsonRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(new AiJsonResponse(Answer(request), Model, 120, 80));
    }

    public const string Good = """
        {"verdict":"mostly_correct","errorType":"grammar","hintUz":"Fe’l oxiriga qarang.","explanationUz":"O‘tgan zamon uchun -았어요 kerak.",
         "correctedText":"어제 학교에 갔어요.","alternatives":["어제는 학교에 갔어요."],"targetUsage":"used_correctly","confidence":0.9,"sourceRefs":[]}
        """;
}
