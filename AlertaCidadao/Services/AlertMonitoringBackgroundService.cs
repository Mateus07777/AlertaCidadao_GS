using System.Text.Json;
using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlertaCidadao.Services;

public sealed class MonitoringOptions { public int IntervalMinutes { get; set; } = 10; public bool RunOnStartup { get; set; } = true; }

public sealed class AlertSyncService(AlertaDbContext db, RadarMeteorologicoAlertProvider provider, ILogger<AlertSyncService> logger)
{
    private static readonly SemaphoreSlim ExecutionGate = new(1, 1);

    public async Task<MonitoringExecution> RunAsync(CancellationToken ct)
    {
        await ExecutionGate.WaitAsync(ct);
        try { return await RunCoreAsync(ct); }
        finally { ExecutionGate.Release(); }
    }

    private async Task<MonitoringExecution> RunCoreAsync(CancellationToken ct)
    {
        var execution = new MonitoringExecution { StartedAt = DateTime.UtcNow, Status = "Running" }; db.MonitoringExecutions.Add(execution); await db.SaveChangesAsync(ct);
        try
        {
            var consulta = await provider.GetAlertsAsync(ct);
            var incoming = consulta.Alerts;
            execution.RecordsFound = incoming.Count;
            foreach (var data in incoming)
            {
                var codes = JsonSerializer.Serialize(data.Geocodes);
                var alert = await db.OfficialAlerts.SingleOrDefaultAsync(x => x.ExternalIdentifier == data.Id, ct);
                var isNew = alert is null;
                if (alert is null)
                {
                    alert = new OfficialAlert { ExternalIdentifier = data.Id, Title = data.Evento, EventType = data.Evento, Severity = data.Severidade, Source = "INMET via RadarMeteorológico", SourceUrl = "https://radarmeteorologico.com.br/api-publica", CreatedAt = DateTime.UtcNow };
                    db.OfficialAlerts.Add(alert); execution.RecordsCreated++;
                }
                else execution.RecordsUpdated++;
                alert.Title = data.Evento; alert.EventType = data.Evento; alert.Severity = data.Severidade; alert.Level = data.Nivel; alert.IsFuture = data.Futuro; alert.StartsAt = data.Inicio; alert.ExpiresAt = data.Fim; alert.AffectedMunicipalityCodes = codes; alert.RetrievedAt = DateTime.UtcNow; alert.UpdatedAt = isNew ? null : DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                var affected = data.Geocodes.Length == 0 ? [] : await db.Citizens.Where(c => c.MunicipalityIbgeCode != null && data.Geocodes.Contains(c.MunicipalityIbgeCode.Value)).Select(c => c.Id).ToListAsync(ct);
                foreach (var citizenId in affected)
                {
                    if (await db.CitizenNotifications.AnyAsync(n => n.CitizenId == citizenId && n.OfficialAlertId == alert.Id, ct)) { execution.RecordsDuplicated++; continue; }
                    db.CitizenNotifications.Add(new CitizenNotification { CitizenId = citizenId, OfficialAlertId = alert.Id, Title = $"Alerta meteorológico: {alert.EventType}", Message = $"O RadarMeteorológico informa severidade {alert.Severity} para o município associado ao seu código IBGE. Esta é uma informação externa, não uma ocorrência confirmada.", Status = "Pending", CreatedAt = DateTime.UtcNow });
                }
                await db.SaveChangesAsync(ct);
            }
            execution.Status = consulta.FailedStates.Count == 0 ? "Succeeded" : "Partial";
            if (consulta.FailedStates.Count > 0)
                execution.ErrorMessage = $"Não foi possível consultar: {string.Join(", ", consulta.FailedStates)}.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { execution.Status = "Cancelled"; execution.ErrorMessage = "Execução cancelada."; throw; }
        catch (Exception ex) { execution.Status = "Failed"; execution.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 4000)]; logger.LogError(ex, "Falha na execução de monitoramento {ExecutionId}", execution.Id); }
        finally { execution.FinishedAt = DateTime.UtcNow; await db.SaveChangesAsync(CancellationToken.None); }
        var situacao = execution.Status switch { "Running" => "Em execução", "Succeeded" => "Concluída", "Partial" => "Concluída parcialmente", "Failed" => "Falhou", "Cancelled" => "Cancelada", _ => execution.Status };
        logger.LogInformation("Monitoramento {ExecutionId} concluído: {Situacao}; encontrados={RecordsFound}, criados={RecordsCreated}, atualizados={RecordsUpdated}, duplicados={RecordsDuplicated}.", execution.Id, situacao, execution.RecordsFound, execution.RecordsCreated, execution.RecordsUpdated, execution.RecordsDuplicated);
        return execution;
    }
}

public sealed class AlertMonitoringBackgroundService(IServiceScopeFactory scopes, IOptions<MonitoringOptions> options, ILogger<AlertMonitoringBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.Value.IntervalMinutes, 1, 1440));
        if (!options.Value.RunOnStartup) await Task.Delay(interval, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var sync = scope.ServiceProvider.GetRequiredService<AlertSyncService>();
                await sync.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Erro no serviço de monitoramento; nova tentativa em {Intervalo}", interval); }
            await Task.Delay(interval, stoppingToken);
        }
    }
}
