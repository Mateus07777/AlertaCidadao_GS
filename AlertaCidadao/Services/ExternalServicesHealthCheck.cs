using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Memory;

namespace AlertaCidadao.Services;

public sealed class ExternalServicesHealthCheck(IHttpClientFactory clients, IMemoryCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue<HealthCheckResult>("external-services-health", out var cached))
            return cached;

        var failures = new List<string>();
        await CheckAsync("RadarMeteorologico", "radar-health", "api/v1/alertas?uf=SP", failures, cancellationToken);
        await CheckAsync("IBGE", "ibge-health", "localidades/estados/35/municipios", failures, cancellationToken);

        var result = failures.Count == 0
            ? HealthCheckResult.Healthy("RadarMeteorológico e IBGE acessíveis.")
            : HealthCheckResult.Degraded("Uma ou mais integrações externas estão indisponíveis: " + string.Join(", ", failures));
        cache.Set("external-services-health", result, TimeSpan.FromMinutes(5));
        return result;
    }

    private async Task CheckAsync(string name, string clientName, string path, List<string> failures, CancellationToken ct)
    {
        try
        {
            using var response = await clients.CreateClient(clientName).GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) failures.Add($"{name} (HTTP {(int)response.StatusCode})");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            failures.Add(name);
        }
    }
}
