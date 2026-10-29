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
        if (koreanText) bytes = await korean.GetAsync(adminId, text, ct);
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
}
