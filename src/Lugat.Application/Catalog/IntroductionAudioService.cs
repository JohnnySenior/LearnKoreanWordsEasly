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

    private static readonly string[] ConsonantNames =
        ["기역", "쌍기역", "기역시옷", "니은", "니은지읒", "니은히읗", "디귿", "쌍디귿", "리을", "리을기역", "리을미음", "리을비읍", "리을시옷", "리을티읕",
         "리을피읖", "리을히읗", "미음", "비읍", "쌍비읍", "비읍시옷", "시옷", "쌍시옷", "이응", "지읒", "쌍지읒", "치읓", "키읔", "티읕", "피읖", "히읗"];

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
            if (c is >= '\u3131' and <= '\u314E') sb.Append(ConsonantNames[c - '\u3131']);
            else if (c is >= '\u314F' and <= '\u3163') sb.Append((char)(0xAC00 + 11 * 28 * 21 + (c - 0x314F) * 28)); // ㅏ → 아 (ㅇ + vowel)
            else sb.Append(c);
        }
        var spoken = sb.ToString().Trim().Trim('-', '~', '–', '—').Trim();
        return spoken.Length > 0 ? spoken : text;
    }
}
