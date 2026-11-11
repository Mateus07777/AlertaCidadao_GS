using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Endpoints;

public static class MonitoringEndpoints
{
    public static RouteGroupBuilder MapMonitoringEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/execucoes-monitoramento").WithTags("Execuções de monitoramento").RequireAuthorization("Administrador").RequireRateLimiting("publica");
        
        g.MapGet("", async (int? pagina, int? tamanhoPagina, string? situacao, AlertaDbContext db, CancellationToken ct) => { var q = db.MonitoringExecutions.AsNoTracking().AsQueryable(); if (situacao is not null) q = q.Where(x => x.Status == EndpointHelpers.StoredExecutionStatus(situacao)); var pg = Math.Max(pagina ?? 1, 1); var size = Math.Clamp(tamanhoPagina ?? 20, 1, 100); var rows = await q.OrderByDescending(x => x.StartedAt).Skip((pg - 1) * size).Take(size).ToListAsync(ct); return Results.Ok(rows.Select(EndpointHelpers.ToResponse)); });
        
        g.MapGet("/{id:int}", async (int id, AlertaDbContext db, CancellationToken ct) => { var x = await db.MonitoringExecutions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); return x is null ? Results.NotFound() : Results.Ok(EndpointHelpers.ToResponse(x)); });
        
        g.MapPost("", async (MonitoringExecutionRequest request, AlertaDbContext db, CancellationToken ct) => { var x = new MonitoringExecution { Provider = request.Provider, StartedAt = request.StartedAt, FinishedAt = request.FinishedAt, Status = EndpointHelpers.StoredExecutionStatus(request.Status), RecordsFound = request.RecordsFound, RecordsCreated = request.RecordsCreated, RecordsUpdated = request.RecordsUpdated, RecordsDuplicated = request.RecordsDuplicated, ErrorMessage = request.ErrorMessage }; db.Add(x); await db.SaveChangesAsync(ct); return Results.Created($"/api/execucoes-monitoramento/{x.Id}", EndpointHelpers.ToResponse(x)); });
        
        g.MapPut("/{id:int}", async (int id, MonitoringExecutionRequest request, AlertaDbContext db, CancellationToken ct) => { var x = await db.MonitoringExecutions.FindAsync([id], ct); if (x is null) return Results.NotFound(); x.Status = EndpointHelpers.StoredExecutionStatus(request.Status); x.ErrorMessage = request.ErrorMessage; x.FinishedAt = request.FinishedAt; await db.SaveChangesAsync(ct); return Results.NoContent(); });
        
        g.MapDelete("/{id:int}", async (int id, AlertaDbContext db, CancellationToken ct) => { var x = await db.MonitoringExecutions.FindAsync([id], ct); if (x is null) return Results.NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return Results.NoContent(); });
        
        
        return g;
    }
}


