using KoreanAiTeacher.Application.Abstractions;

namespace KoreanAiTeacher.Infrastructure.Files;

public sealed class FileStoreOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Root folder for original files. In production this must be a persistent volume,
    /// never the container's temporary disk (spec REL04).
    /// </summary>
    public string Root { get; set; } = "App_Data/files";
}

/// <summary>File storage on a local or mounted disk. Keys are validated so they cannot escape the root.</summary>
public sealed class LocalFileStore(string root) : IFileStore
{
    private readonly string _root = Path.GetFullPath(root);

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var file = File.Create(temp))
            {
                await content.CopyToAsync(file, ct);
            }

            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Resolve(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }

        return path;
    }
}

