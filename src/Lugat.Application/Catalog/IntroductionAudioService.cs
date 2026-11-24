using System.Security.Cryptography;
using System.Text;
using Lugat.Application.Abstractions;
using Lugat.Application.Ai;
using Lugat.Application.Speech;
using Lugat.Domain.Ai;

namespace Lugat.Application.Catalog;

public interface IIntroductionNarrator
{
    bool IsConfigured { get; }
    string Profile { get; }
    string Model { get; }
    Task<byte[]> SynthesizeAsync(string text, CancellationToken ct);
}

public sealed class IntroductionAudioService(IIntroductionNarrator narrator, SpeechService korean, ISpeechSynthesizer tts,
    IFileStore files, IOperationLock operations, AiUsageService usage)
{
    public bool IsConfigured => narrator.IsConfigured && tts.IsConfigured;
    public string NarratorProfile => narrator.Profile;
    public string KoreanProfile => tts.Model;

    public async Task<string> MakeAsync(Guid adminId, string text, bool koreanText, CancellationToken ct)
    {
        if (!IsConfigured) throw new ServiceUnavailableException("Tanishtirish uchun ElevenLabs va koreyscha ovoz sozlamalari kerak.");
        var profile = koreanText ? KoreanProfile : NarratorProfile;
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(profile + "\n" + text)));
        var key = $"introductions/{(koreanText ? "ko" : "uz")}-{hash}.mp3";
        await using var lease = await operations.AcquireAsync("intro-audio:" + key, ct);
        await using (var exists = await files.OpenReadAsync(key, ct))
            if (exists is not null) return key;
        byte[] bytes;
        if (koreanText)
        {
            try { bytes = await korean.GetAsync(adminId, Speakable(text), ct); }
            catch (ServiceUnavailableException)
            {
                // Name the part, so the editor knows which Korean text to change.
                throw new ServiceUnavailableException($"«{text}» uchun koreyscha ovoz tayyorlanmadi. Birozdan keyin qayta urinib ko‘ring; takrorlansa, ssenariyda shu qismni o‘zgartiring.");
            }
        }
        else
        {
            var reservation = await usage.ReserveAsync(adminId, "tts", AiProviderKind.ElevenLabs, narrator.Model, text.Length, 0, 400, ct, "characters");
            try
            {
                bytes = await narrator.SynthesizeAsync(text, ct);
                if (bytes.Length == 0) throw new ServiceUnavailableException("Diktor bo‘sh ovoz qaytardi.");
                await usage.CompleteAsync(reservation, narrator.Model, text.Length, 0, 0, null);
            }
            catch
            {
                await usage.FailAsync(reservation, 0, "Tanishtirish ovozi tayyorlanmadi.");
                throw;
            }
        }
        await files.SaveAsync(key, new MemoryStream(bytes, writable: false), ct);
        return key;
    }

    /// <summary>
    /// What the Korean voice is given (L-62). A lone letter such as «-ㄹ» (a 받침 or an ending, common when a verb's
    /// stem is explained) is not text a voice can read: Azure answers it with empty audio every time. Each jamo is
    /// said by its name, as a teacher would (ㄹ → 리을, ㅏ → 아); a leading or trailing "-" or "~" is dropped.
    /// </summary>
    public static string Speakable(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            // L-93: the names live with the alphabet page's table (HangulAlphabet).
            if (Seo.HangulAlphabet.SpokenName(c) is { } name) sb.Append(name);
            else sb.Append(c);
        }
        var spoken = sb.ToString().Trim().Trim('-', '~', '–', '—').Trim();
        return spoken.Length > 0 ? spoken : text;
    }
}
