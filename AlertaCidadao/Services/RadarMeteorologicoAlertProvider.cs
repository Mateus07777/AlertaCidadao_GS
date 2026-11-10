using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlertaCidadao.Services;

public sealed class RadarMeteorologicoAlertProvider(HttpClient client, ILogger<RadarMeteorologicoAlertProvider> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] Estados = ["AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"];

    public async Task<RadarAlertFetchResult> GetAlertsAsync(CancellationToken ct)
    {
        var alertas = new Dictionary<long, RadarAlertDto>();
        var estadosComFalha = new List<string>();
        Exception? primeiraFalha = null;
        var estadosConsultados = 0;

        foreach (var uf in Estados)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync($"api/v1/alertas?uf={uf}", HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<RadarResponse>(JsonOptions, ct)
                    ?? throw new JsonException("Resposta vazia do RadarMeteorológico.");
                if (payload.Alertas is null) throw new JsonException("A resposta não contém a coleção alertas.");
                estadosConsultados++;

                foreach (var item in payload.Alertas.Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Evento)))
                {
                    var alerta = new RadarAlertDto(
                        item.Id, item.Evento!, item.Severidade ?? "Não informada", item.Nivel,
                        ParseLocal(item.Inicio), ParseLocal(item.Fim), item.Futuro,
                        (item.Geocodes ?? []).Distinct().Where(code => code > 0).ToArray());

                    if (alertas.TryGetValue(alerta.Id, out var existente))
                        alertas[alerta.Id] = existente with { Geocodes = existente.Geocodes.Concat(alerta.Geocodes).Distinct().ToArray() };
                    else
                        alertas.Add(alerta.Id, alerta);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
            {
                primeiraFalha ??= ex;
                estadosComFalha.Add(uf);
                logger.LogWarning(ex, "Falha ao consultar alertas do RadarMeteorológico para a UF {Uf}.", uf);
            }
        }

        if (estadosConsultados == 0)
        {
            if (primeiraFalha is JsonException erroJson) throw erroJson;
            if (primeiraFalha is OperationCanceledException erroTempo) throw erroTempo;
            throw new HttpRequestException("Não foi possível consultar o RadarMeteorológico em nenhuma UF.", primeiraFalha);
        }

        logger.LogInformation("Consulta ao RadarMeteorológico concluída: {EstadosConsultados} UFs, {AlertasUnicos} alertas únicos e {EstadosComFalha} UFs com falha.", estadosConsultados, alertas.Count, estadosComFalha.Count);
        return new RadarAlertFetchResult(alertas.Values.ToArray(), estadosComFalha);
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
public sealed record RadarAlertFetchResult(IReadOnlyList<RadarAlertDto> Alerts, IReadOnlyList<string> FailedStates);
