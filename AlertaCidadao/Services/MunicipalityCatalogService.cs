using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Services;

public sealed class MunicipalityCatalogUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class MunicipalityCatalogService(HttpClient client, AlertaDbContext db, ILogger<MunicipalityCatalogService> logger)
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UfLocks = new(StringComparer.OrdinalIgnoreCase);

    private static readonly (string Uf, string Nome, int CodigoIbge)[] Estados =
    [
        ("AC", "Acre", 12), ("AL", "Alagoas", 27), ("AP", "Amapá", 16), ("AM", "Amazonas", 13), ("BA", "Bahia", 29),
        ("CE", "Ceará", 23), ("DF", "Distrito Federal", 53), ("ES", "Espírito Santo", 32), ("GO", "Goiás", 52),
        ("MA", "Maranhão", 21), ("MT", "Mato Grosso", 51), ("MS", "Mato Grosso do Sul", 50), ("MG", "Minas Gerais", 31),
        ("PA", "Pará", 15), ("PB", "Paraíba", 25), ("PR", "Paraná", 41), ("PE", "Pernambuco", 26), ("PI", "Piauí", 22),
        ("RJ", "Rio de Janeiro", 33), ("RN", "Rio Grande do Norte", 24), ("RS", "Rio Grande do Sul", 43),
        ("RO", "Rondônia", 11), ("RR", "Roraima", 14), ("SC", "Santa Catarina", 42), ("SP", "São Paulo", 35),
        ("SE", "Sergipe", 28), ("TO", "Tocantins", 17)
    ];

    public static IReadOnlyList<StateOption> GetStates() => Estados.Select(x => new StateOption(x.Uf, x.Nome)).ToArray();

    public async Task<IReadOnlyList<MunicipalityOption>> GetMunicipalitiesAsync(string uf, CancellationToken ct)
    {
        var normalizedUf = uf.Trim().ToUpperInvariant();
        if (!Estados.Any(x => x.Uf == normalizedUf))
            throw new ArgumentException("A sigla da unidade federativa não é válida.", nameof(uf));

        var cached = await ReadMunicipalitiesAsync(normalizedUf, ct);
        if (cached.Count > 0) return cached;

        var gate = UfLocks.GetOrAdd(normalizedUf, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            cached = await ReadMunicipalitiesAsync(normalizedUf, ct);
            if (cached.Count > 0) return cached;

            List<IbgeMunicipality>? response;
            try
            {
                var codigoUf = Estados.Single(x => x.Uf == normalizedUf).CodigoIbge;
                response = await client.GetFromJsonAsync<List<IbgeMunicipality>>($"localidades/estados/{codigoUf}/municipios", ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
            {
                throw new MunicipalityCatalogUnavailableException("Não foi possível carregar a lista de municípios do IBGE. Tente novamente em instantes.", ex);
            }

            var municipalities = response?
                .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nome))
                .Select(x => new Municipality
                {
                    IbgeCode = x.Id,
                    Name = x.Nome!.Trim(),
                    Uf = normalizedUf,
                    NormalizedName = NormalizeName(x.Nome!)
                })
                .ToArray() ?? [];

            if (municipalities.Length == 0)
                throw new MunicipalityCatalogUnavailableException("O IBGE não retornou municípios para a unidade federativa informada.");

            db.Municipalities.AddRange(municipalities);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                cached = await ReadMunicipalitiesAsync(normalizedUf, ct);
                if (cached.Count == 0) throw;
                return cached;
            }

            logger.LogInformation("Catálogo de municípios do IBGE carregado para {Uf}: {Quantidade} municípios.", normalizedUf, municipalities.Length);
            return municipalities.Select(x => new MunicipalityOption(x.Name, x.Uf)).OrderBy(x => x.Nome).ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Municipality?> ResolveAsync(string nome, string uf, CancellationToken ct)
    {
        var municipalities = await GetMunicipalitiesAsync(uf, ct);
        var normalizedName = NormalizeName(nome);
        var option = municipalities.FirstOrDefault(x => NormalizeName(x.Nome) == normalizedName);
        if (option is null) return null;

        return await db.Municipalities.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Uf == option.Uf && x.NormalizedName == normalizedName, ct);
    }

    private async Task<IReadOnlyList<MunicipalityOption>> ReadMunicipalitiesAsync(string uf, CancellationToken ct) =>
        await db.Municipalities.AsNoTracking().Where(x => x.Uf == uf).OrderBy(x => x.Name)
            .Select(x => new MunicipalityOption(x.Name, x.Uf)).ToListAsync(ct);

    private static string NormalizeName(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);

        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    private sealed class IbgeMunicipality
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("nome")] public string? Nome { get; set; }
    }
}

public sealed record StateOption(string Uf, string Nome);
public sealed record MunicipalityOption(string Nome, string Uf);
