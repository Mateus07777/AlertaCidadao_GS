using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Services;

public sealed class IdempotencyService(AlertaDbContext db)
{
    public async Task<IdempotencyRecord?> FindAsync(string key, string hash, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.SingleOrDefaultAsync(x => x.IdempotencyKey == key && x.ExpiresAt > DateTime.UtcNow, ct);
        return record?.RequestHash == hash ? record : null;
    }
    public Task<bool> KeyExistsAsync(string key, CancellationToken ct) => db.IdempotencyRecords.AnyAsync(x => x.IdempotencyKey == key && x.ExpiresAt > DateTime.UtcNow, ct);
}
