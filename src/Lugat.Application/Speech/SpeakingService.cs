using Lugat.Application.Abstractions;
using Lugat.Application.Ai;
using Lugat.Contracts;
using Lugat.Domain.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lugat.Application.Speech;

/// <summary>Korean speech recognition and pronunciation assessment (Azure Speech in Infrastructure, D-32).</summary>
public interface IPronunciationAssessor
{
    bool IsConfigured { get; }

    /// <param name="wav">16 kHz mono PCM WAV.</param>
    /// <param name="referenceText">The text the learner should say; null for free speech (transcript only).</param>
    Task<SpeakingResultDto> AssessAsync(byte[] wav, string? referenceText, CancellationToken ct);
}

/// <summary>
/// Speaking practice (spec AU02–AU04, D-32): the learner says a word or sentence, the service returns what was heard
/// and, when a reference text is given, the pronunciation scores Azure reports. No score is invented from the text
/// (AU04). The recording is not stored.
/// </summary>
public sealed class SpeakingService(
    IPronunciationAssessor assessor,
    IOptions<SpeechOptions> options,
    ILogger<SpeakingService> logger,
    AiUsageService usage,
    Premium.PremiumService premium)
{
    public const string Feature = "stt";

    /// <summary>About 30 seconds of 16 kHz mono PCM.</summary>
    public const int MaxBytes = 1_000_000;

    public async Task<SpeakingResultDto> AssessAsync(Guid ownerId, byte[] wav, string? referenceText, CancellationToken ct)
    {
        await premium.RequireAsync(ownerId, ct); // pronunciation checks are Premium (spec §7.1)
        if (wav.Length < 3200 || wav.Length > MaxBytes || wav[0] != (byte)'R' || wav[1] != (byte)'I' || wav[2] != (byte)'F' || wav[3] != (byte)'F')
        {
            throw new ValidationException("Yozuv juda qisqa, juda uzun yoki noto‘g‘ri formatda. 1–30 soniya gapiring.");
        }

        var reference = string.IsNullOrWhiteSpace(referenceText) ? null : referenceText.Trim();
        if (reference is not null && (reference.Length > SpeechService.MaxLength || !SentenceCheckService.ContainsHangul(reference)))
        {
            throw new ValidationException("Solishtirish uchun koreyscha matn kerak.");
        }

        if (!assessor.IsConfigured)
        {
            throw new ServiceUnavailableException("Talaffuzni tekshirish xizmati ulanmagan (serverda Azure Speech kaliti yo‘q).");
        }

        var model = reference is null ? "stt ko-KR" : "pronunciation ko-KR";
        var milliseconds = Math.Max(0, wav.Length - 44) / 32;
        var reservation = await usage.ReserveAsync(ownerId, Feature, AiProviderKind.Azure, model, milliseconds, 0, options.Value.MaxAssessPerUserPerDay, ct, "audio-ms");

        string? error = null;
        try
        {
            return await assessor.AssessAsync(wav, reference, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            error = ex is AiProviderException ? ex.Message : "nutqni tanish xizmati javob bermadi";
            logger.LogWarning("Speech assessment failed: {Error}", error);
            throw new ServiceUnavailableException("Nutq hozir tekshirilmadi. Birozdan keyin qayta urinib ko‘ring.");
        }
        finally
        {
            if (error is null && !ct.IsCancellationRequested)
                await usage.CompleteAsync(reservation, model, milliseconds, 0, 0, null);
            else
                await usage.FailAsync(reservation, 0, error ?? "so‘rov bekor qilindi");
        }
    }
}

