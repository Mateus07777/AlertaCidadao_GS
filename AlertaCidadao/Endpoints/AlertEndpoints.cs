using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;
using AlertaCidadao.Services;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Endpoints;

public static class AlertEndpoints
{
    public static RouteGroupBuilder MapAlertEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/alertas-oficiais").WithTags("Alertas meteorológicos externos").RequireRateLimiting("publica");
        
        g.MapGet("", async (int? pagina, int? tamanhoPagina, int? codigoIbgeMunicipio, string? severidade, string? tipoEvento, AlertaDbContext db, CancellationToken ct) =>
        {
            var q = db.OfficialAlerts.AsNoTracking().AsQueryable(); if (codigoIbgeMunicipio.HasValue) q = db.OfficialAlerts.FromSqlInterpolated($"SELECT * FROM OfficialAlerts WHERE JSON_CONTAINS(AffectedMunicipalityCodes, CAST({codigoIbgeMunicipio.Value} AS JSON))").AsNoTracking(); if (!string.IsNullOrWhiteSpace(severidade)) q = q.Where(x => x.Severity == severidade); if (!string.IsNullOrWhiteSpace(tipoEvento)) q = q.Where(x => x.EventType == tipoEvento);
            var pg = Math.Max(pagina ?? 1, 1); var size = Math.Clamp(tamanhoPagina ?? 20, 1, 100); var items = await q.OrderByDescending(x => x.RetrievedAt).Skip((pg - 1) * size).Take(size).ToListAsync(ct); return Results.Ok(items.Select(EndpointHelpers.ToResponse));
        }).AllowAnonymous();
        
        g.MapGet("/{id:int}", async (int id, AlertaDbContext db, CancellationToken ct) => { var x = await db.OfficialAlerts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return x is null ? Results.NotFound() : Results.Ok(EndpointHelpers.ToResponse(x)); }).AllowAnonymous();
        
        g.MapPost("/sincronizar", async (AlertSyncService sync, CancellationToken ct) => Results.Ok(EndpointHelpers.ToResponse(await sync.RunAsync(ct)))).RequireAuthorization("Administrador");
        
        g.MapPost("", async (OfficialAlertRequest request, AlertaDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.EventType) || string.IsNullOrWhiteSpace(request.Severity)) return Results.BadRequest(new { erro = "Título, evento e severidade são obrigatórios." });
            var alert = new OfficialAlert { ExternalIdentifier = request.ExternalIdentifier, Title = request.Title.Trim(), Description = request.Description, EventType = request.EventType.Trim(), Severity = request.Severity.Trim(), Level = request.Level, IsFuture = request.IsFuture, MunicipalityIbgeCode = request.MunicipalityIbgeCode, AffectedMunicipalityCodes = request.AffectedMunicipalityCodes, StartsAt = request.StartsAt, ExpiresAt = request.ExpiresAt, Source = "INMET via RadarMeteorológico", SourceUrl = "https://radarmeteorologico.com.br/api-publica", CreatedAt = DateTime.UtcNow, RetrievedAt = DateTime.UtcNow };
            db.Add(alert); await db.SaveChangesAsync(ct); return Results.Created($"/api/alertas-oficiais/{alert.Id}", EndpointHelpers.ToResponse(alert));
        }).RequireAuthorization("Administrador");
        
        g.MapPut("/{id:int}", async (int id, OfficialAlertRequest request, AlertaDbContext db, CancellationToken ct) =>
        {
            var x = await db.OfficialAlerts.FindAsync([id], ct); if (x is null) return Results.NotFound(); x.Title = request.Title; x.Description = request.Description; x.EventType = request.EventType; x.Severity = request.Severity; x.Level = request.Level; x.IsFuture = request.IsFuture; x.MunicipalityIbgeCode = request.MunicipalityIbgeCode; x.AffectedMunicipalityCodes = request.AffectedMunicipalityCodes; x.StartsAt = request.StartsAt; x.ExpiresAt = request.ExpiresAt; x.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("Administrador");
        
        g.MapDelete("/{id:int}", async (int id, AlertaDbContext db, CancellationToken ct) =>
        {
            if (await db.CitizenNotifications.AnyAsync(x => x.OfficialAlertId == id, ct)) return Results.Conflict(new { erro = "Alerta possui notificações associadas." }); var x = await db.OfficialAlerts.FindAsync([id], ct); if (x is null) return Results.NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("Administrador");
        
        
        return g;
    }
}

