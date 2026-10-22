namespace Lugat.Application.Abstractions;

/// <summary>Serializes operations across processes. Acquire before loading tracked state.</summary>
public interface IOperationLock
{
    Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct);
}
