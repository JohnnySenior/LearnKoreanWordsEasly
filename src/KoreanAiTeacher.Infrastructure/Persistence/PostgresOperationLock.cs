using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using KoreanAiTeacher.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace KoreanAiTeacher.Infrastructure.Persistence;

/// <summary>A dedicated session owns the advisory lock; no database transaction stays open during AI calls.</summary>
public sealed class PostgresOperationLock(IConfiguration config) : IOperationLock
{
    public async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(config.GetConnectionString("Default"));
        try
        {
            await connection.OpenAsync(ct);
            var id = BinaryPrimitives.ReadInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes("kat:" + key)));
            await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(@id)", connection) { CommandTimeout = 120 };
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync(ct);
            return new Lease(connection, id);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(NpgsqlConnection connection, long id) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@id)", connection);
                command.Parameters.AddWithValue("id", id);
                await command.ExecuteNonQueryAsync(CancellationToken.None);
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
