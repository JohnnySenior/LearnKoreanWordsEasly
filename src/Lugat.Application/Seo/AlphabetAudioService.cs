using System.Text.Json;
using Lugat.Application.Abstractions;
using Lugat.Application.Speech;
using Lugat.Contracts;

namespace Lugat.Application.Seo;

/// <summary>
/// L-93: the voice of the public alphabet page. The admin records it once (a few calls, each a batch); the files stay
/// in the file store (the Railway volume, like the /sinab audio) and visitors only read them, so a visit never costs a
/// recording. A lone jamo gets empty audio from Azure (L-62), so each letter is said by a syllable and by its name.
/// </summary>
public sealed class AlphabetAudioService(SpeechService speech, IFileStore files, IOperationLock operations, TimeProvider time)
{
    public const int Batch = 15;

    private const string ReadyKey = "alifbo/ready.json";

    private static readonly HashSet<string> Keys = [.. HangulAlphabet.Recordings.Select(r => r.Key)];

    // The page asks on every visit; the list changes only when the admin records, so it is read at most once a minute.
    private static (HashSet<string> Keys, DateTimeOffset At)? cached;

    private static string FileKey(string key) => $"alifbo/{key}.mp3";

    /// <summary>The recordings the page may offer (an empty set before the admin records them).</summary>
    public async Task<IReadOnlySet<string>> ReadyAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (cached is { } c && now - c.At < TimeSpan.FromMinutes(1))
        {
            return c.Keys;
        }

        HashSet<string> ready = [];
        await using (var stream = await files.OpenReadAsync(ReadyKey, ct))
        {
            if (stream is not null)
            {
                try { ready = [.. (await JsonSerializer.DeserializeAsync<List<string>>(stream, cancellationToken: ct) ?? []).Where(Keys.Contains)]; }
                catch (JsonException) { }
            }
        }

        cached = (ready, now);
        return ready;
    }

    public async Task<Stream> OpenAsync(string key, CancellationToken ct) =>
        Keys.Contains(key) && (await ReadyAsync(ct)).Contains(key) && await files.OpenReadAsync(FileKey(key), ct) is { } stream
            ? stream
            : throw new NotFoundException("Ovoz topilmadi.");

    /// <summary>Records up to <see cref="Batch"/> of the missing files; call again while it makes progress.</summary>
    public async Task<AlphabetAudioDto> NextAsync(Guid adminId, CancellationToken ct)
    {
        await using var lease = await operations.AcquireAsync("alifbo-audio", ct);
        var ready = new List<string>();
        var missing = new List<(string Key, string Text)>();
        foreach (var r in HangulAlphabet.Recordings)
        {
            await using var existing = await files.OpenReadAsync(FileKey(r.Key), ct);
            if (existing is null)
            {
                missing.Add(r);
            }
            else
            {
                ready.Add(r.Key);
            }
        }

        var failed = new List<string>();
        foreach (var (key, text) in missing.Take(Batch))
        {
            try
            {
                var bytes = await speech.GetAsync(adminId, text, ct);
                if (bytes.Length == 0) throw new ServiceUnavailableException("Bo‘sh ovoz.");
                await files.SaveAsync(FileKey(key), new MemoryStream(bytes, writable: false), ct);
                ready.Add(key);
            }
            catch (ServiceUnavailableException)
            {
                failed.Add(text);
            }
        }

        await files.SaveAsync(ReadyKey, new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(ready), writable: false), ct);
        cached = null;
        return new(ready.Count, HangulAlphabet.Recordings.Count, failed);
    }
}
