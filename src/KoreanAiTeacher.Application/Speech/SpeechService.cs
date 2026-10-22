using System.Security.Cryptography;
using System.Text;
using KoreanAiTeacher.Application.Abstractions;
using KoreanAiTeacher.Application.Ai;
using KoreanAiTeacher.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KoreanAiTeacher.Application.Speech;

public sealed class SpeechOptions
{
    public const string SectionName = "Ai:Tts";

    /// <summary>"Azure", "OpenAi" or empty: Azure when its key is set (natural Korean voices, D-31), else OpenAI.</summary>
    public string? Provider { get; set; }

    /// <summary>OpenAI speech model; the key is Ai:OpenAi:ApiKey.</summary>
    public string Model { get; set; } = "gpt-4o-mini-tts";

    /// <summary>OpenAI voice.</summary>
    public string Voice { get; set; } = "coral";

    /// <summary>OpenAI voice for the man in listening dialogues (D-53).</summary>
    public string MaleVoice { get; set; } = "ash";

    public AzureSpeechOptions Azure { get; set; } = new();

    /// <summary>New (not cached) recordings per learner per day; cached ones are free (spec BUD05).</summary>
    public int MaxNewPerUserPerDay { get; set; } = 400;

    /// <summary>Speaking checks per learner per day (Azure free tier has a few hours of speech a month).</summary>
    public int MaxAssessPerUserPerDay { get; set; } = 200;
}

public sealed class AzureSpeechOptions
{
    public string? Key { get; set; }

    /// <summary>Region of the Speech resource, e.g. koreacentral.</summary>
    public string? Region { get; set; }

    /// <summary>ko-KR-SunHiNeural (female) or ko-KR-InJoonNeural (male), among others.</summary>
    public string Voice { get; set; } = "ko-KR-SunHiNeural";

    /// <summary>The man in listening dialogues (D-53); the woman is <see cref="Voice"/>.</summary>
    public string MaleVoice { get; set; } = "ko-KR-InJoonNeural";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(Region) && !string.IsNullOrWhiteSpace(Voice);
}

/// <summary>Turns Korean text into speech (MP3). Implemented with Azure and OpenAI in Infrastructure.</summary>
public interface ISpeechSynthesizer
{
    bool IsConfigured { get; }

    AiProviderKind Kind { get; }

    /// <summary>Model and voice; part of the cache key, so another voice records anew.</summary>
    string Model { get; }

    /// <summary>Voices used for dialogues; part of their cache key.</summary>
    string DialogueModel { get; }

    Task<byte[]> SynthesizeAsync(string text, CancellationToken ct);

    /// <summary>A dialogue or announcement as one recording: a woman's and a man's voice, a short pause between turns (D-53).</summary>
    Task<byte[]> SynthesizeDialogueAsync(IReadOnlyList<SpeechLine> lines, CancellationToken ct);
}

/// <param name="Male">The man's voice; otherwise the woman's.</param>
public sealed record SpeechLine(bool Male, string Text);

/// <summary>
/// Pronunciation of Korean words and sentences (spec AU01, D-29). Each text is recorded once and kept in the file
/// store, so listening again costs nothing. Slow playback is done on the device (playbackRate), not recorded twice.
/// </summary>
public sealed class SpeechService(
    ISpeechSynthesizer tts,
    IFileStore files,
    IOptions<SpeechOptions> options,
    ILogger<SpeechService> logger,
    IOperationLock operations,
    AiUsageService usage)
{
    public const int MaxLength = 200;

    public const string Feature = "tts";

    public async Task<byte[]> GetAsync(Guid ownerId, string? text, CancellationToken ct)
    {
        var clean = (text ?? "").Trim();
        if (clean.Length == 0 || clean.Length > MaxLength || !SentenceCheckService.ContainsHangul(clean))
        {
            throw new ValidationException($"Tinglash uchun {MaxLength} belgigacha koreyscha matn kerak.");
        }

        return await GetOrMakeAsync(ownerId, CacheKey(clean), clean.Length, tts.Model, c => tts.SynthesizeAsync(clean, c), ct);
    }

    /// <summary>Longest listening passage, in characters (D-53).</summary>
    public const int MaxDialogueLength = 1500;

    /// <summary>A listening passage (D-53): recorded once with two voices and kept like single words.</summary>
    public async Task<byte[]> GetDialogueAsync(Guid ownerId, IReadOnlyList<SpeechLine> lines, CancellationToken ct)
    {
        var clean = lines.Select(l => l with { Text = l.Text.Trim() }).Where(l => l.Text.Length > 0).ToList();
        var length = clean.Sum(l => l.Text.Length);
        if (clean.Count == 0 || length > MaxDialogueLength || !clean.All(l => SentenceCheckService.ContainsHangul(l.Text)))
        {
            throw new ValidationException("Tinglash matni koreyscha bo‘lishi kerak.");
        }

        var text = string.Join("\n", clean.Select(l => (l.Male ? "M:" : "F:") + l.Text));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tts.Kind}|{tts.DialogueModel}|{text}"));
        var key = $"tts/dialogue-{Convert.ToHexStringLower(hash)}.mp3";
        return await GetOrMakeAsync(ownerId, key, length, tts.DialogueModel, c => tts.SynthesizeDialogueAsync(clean, c), ct);
    }

    private async Task<byte[]> GetOrMakeAsync(Guid ownerId, string key, int characters, string model, Func<CancellationToken, Task<byte[]>> make, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync("tts:" + key, ct);
        await using (var cached = await files.OpenReadAsync(key, ct))
        {
            if (cached is not null)
            {
                using var ms = new MemoryStream();
                await cached.CopyToAsync(ms, ct);
                return ms.ToArray();
            }
        }

        if (!tts.IsConfigured)
        {
            throw new ServiceUnavailableException("Ovoz xizmati ulanmagan (serverda OpenAI kaliti yo‘q).");
        }

        var reservation = await usage.ReserveAsync(ownerId, Feature, tts.Kind, model, characters, 0, options.Value.MaxNewPerUserPerDay, ct, "characters");

        byte[] audio;
        string? error = null;
        try
        {
            audio = await make(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            error = ex is AiProviderException ? ex.Message : "ovoz xizmati javob bermadi";
            logger.LogWarning("Speech for {Length} characters failed: {Error}", characters, error);
            throw new ServiceUnavailableException("Ovoz hozir tayyorlanmadi. Birozdan keyin qayta urinib ko‘ring.");
        }
        finally
        {
            if (error is null && !ct.IsCancellationRequested)
                await usage.CompleteAsync(reservation, model, characters, 0, 0, null);
            else
                await usage.FailAsync(reservation, 0, error ?? "so‘rov bekor qilindi");
        }

        await files.SaveAsync(key, new MemoryStream(audio), ct);
        return audio;
    }

    /// <summary>Same text, model and voice → same file.</summary>
    public string CacheKey(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tts.Kind}|{tts.Model}|{text}"));
        return $"tts/{Convert.ToHexStringLower(hash)}.mp3";
    }
}

