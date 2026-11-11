using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AlertaCidadao.Services;

public sealed class IdempotencyService(AlertaDbContext db)
{
    public static string ScopeKey(int citizenId, string operation, string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{citizenId}\0{operation}\0{key}")));

    public async Task<IdempotencyRecord?> FindAsync(string key, string hash, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.SingleOrDefaultAsync(x => x.IdempotencyKey == key && x.ExpiresAt > DateTime.UtcNow, ct);
        return record?.RequestHash == hash ? record : null;
    }
    public Task<bool> KeyExistsAsync(string key, CancellationToken ct) => db.IdempotencyRecords.AnyAsync(x => x.IdempotencyKey == key && x.ExpiresAt > DateTime.UtcNow, ct);

    public async Task RemoveExpiredAsync(string key, CancellationToken ct)
    {
        var expired = await db.IdempotencyRecords.SingleOrDefaultAsync(x => x.IdempotencyKey == key && x.ExpiresAt <= DateTime.UtcNow, ct);
        if (expired is null) return;
        db.IdempotencyRecords.Remove(expired);
        await db.SaveChangesAsync(ct);
    }
}
