using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlertaCidadao.Services;

public sealed class RadarMeteorologicoAlertProvider(HttpClient client, ILogger<RadarMeteorologicoAlertProvider> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<RadarAlertDto>> GetAlertsAsync(CancellationToken ct)
    {
        using var response = await client.GetAsync("api/v1/alertas?uf=SP", HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<RadarResponse>(JsonOptions, ct) ?? throw new JsonException("Resposta vazia do RadarMeteorológico.");
        if (payload.Alertas is null) throw new JsonException("A resposta não contém a coleção alertas.");
        logger.LogInformation("RadarMeteorológico retornou {Count} alertas; fonte informada: {Source}", payload.Alertas.Count, payload.Fonte);
        return payload.Alertas.Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Evento)).Select(x => new RadarAlertDto(
            x.Id, x.Evento!, x.Severidade ?? "Não informada", x.Nivel, ParseLocal(x.Inicio), ParseLocal(x.Fim), x.Futuro,
            (x.Geocodes ?? []).Distinct().Where(code => code > 0).ToArray())).ToArray();
    }

    private static DateTime? ParseLocal(string? value) => DateTime.TryParseExact(value, ["yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm:ss.fffZ", "yyyy-MM-ddTHH:mm:ssZ"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? DateTime.SpecifyKind(result, DateTimeKind.Unspecified) : null;

    private sealed class RadarResponse { [JsonPropertyName("fonte")] public string? Fonte { get; set; } [JsonPropertyName("alertas")] public List<RadarAlert>? Alertas { get; set; } }
    private sealed class RadarAlert
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("evento")] public string? Evento { get; set; }
        [JsonPropertyName("severidade")] public string? Severidade { get; set; }
        [JsonPropertyName("nivel")] public int? Nivel { get; set; }
        [JsonPropertyName("inicio")] public string? Inicio { get; set; }
        [JsonPropertyName("fim")] public string? Fim { get; set; }
        [JsonPropertyName("futuro")] public bool? Futuro { get; set; }
        [JsonPropertyName("geocodes")] public int[]? Geocodes { get; set; }
    }
}

public sealed record RadarAlertDto(long Id, string Evento, string Severidade, int? Nivel, DateTime? Inicio, DateTime? Fim, bool? Futuro, int[] Geocodes);
