namespace Lugat.Application.Abstractions;

/// <summary>Private file storage (generated audio). Keys are opaque; callers never build paths.</summary>
public interface IFileStore
{
    Task SaveAsync(string key, Stream content, CancellationToken ct);

    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    Task DeleteAsync(string key, CancellationToken ct);
}
