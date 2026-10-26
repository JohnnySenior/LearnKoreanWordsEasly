using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lugat.Api.Tests.Infrastructure;

/// <summary>
/// Counts the SQL commands EF Core sends to one test database while it is alive, to show that a list costs the same
/// number of round trips whatever its length (audit 2026-10-26). Other factories' databases are not counted.
/// </summary>
public sealed class SqlCounter : IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly string _database;
    private readonly IDisposable _all;
    private readonly List<IDisposable> _listeners = [];
    private int _count;

    public SqlCounter(string database)
    {
        _database = database;
        _all = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public int Count => Volatile.Read(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name == DbLoggerCategory.Name)
        {
            lock (_listeners)
            {
                _listeners.Add(listener.Subscribe(this));
            }
        }
    }

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key == RelationalEventId.CommandExecuting.Name && value.Value is CommandEventData { Command: DbCommand command }
            && command.Connection?.Database == _database)
        {
            Interlocked.Increment(ref _count);
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        _all.Dispose();
        lock (_listeners)
        {
            _listeners.ForEach(l => l.Dispose());
        }
    }
}
