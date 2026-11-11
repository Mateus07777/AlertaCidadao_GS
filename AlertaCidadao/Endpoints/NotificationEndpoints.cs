using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/notificacoes").WithTags("Notificações preventivas").RequireAuthorization().RequireRateLimiting("publica");
        
        g.MapGet("", async (HttpContext c, int? pagina, int? tamanhoPagina, string? situacao, AlertaDbContext db, CancellationToken ct) =>
        {
            var q = db.CitizenNotifications.AsNoTracking().AsQueryable(); if (!EndpointHelpers.IsAdmin(c.User)) q = q.Where(x => x.CitizenId == EndpointHelpers.UserId(c.User)); if (!string.IsNullOrWhiteSpace(situacao)) q = q.Where(x => x.Status == EndpointHelpers.StoredNotificationStatus(situacao));
            var pg = Math.Max(pagina ?? 1, 1); var size = Math.Clamp(tamanhoPagina ?? 20, 1, 100); var items = await q.OrderByDescending(x => x.CreatedAt).Skip((pg - 1) * size).Take(size).ToListAsync(ct); return Results.Ok(items.Select(EndpointHelpers.ToResponse));
        });
        
        g.MapGet("/{id:int}", async (int id, HttpContext c, AlertaDbContext db, CancellationToken ct) => { var x = await db.CitizenNotifications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return Results.NotFound(); return !EndpointHelpers.IsAdmin(c.User) && x.CitizenId != EndpointHelpers.UserId(c.User) ? Results.Forbid() : Results.Ok(EndpointHelpers.ToResponse(x)); });
        
        g.MapPost("", async (CreateNotificationRequest request, HttpContext c, AlertaDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message)) return Results.BadRequest(new { erro = "Título e mensagem são obrigatórios." });
            var citizenId = EndpointHelpers.IsAdmin(c.User) ? request.CitizenId : EndpointHelpers.UserId(c.User); if (!await db.OfficialAlerts.AnyAsync(x => x.Id == request.OfficialAlertId, ct)) return Results.NotFound(new { erro = "Alerta não encontrado." });
            if (await db.CitizenNotifications.AnyAsync(x => x.CitizenId == citizenId && x.OfficialAlertId == request.OfficialAlertId, ct)) return Results.Conflict(new { erro = "Notificação já existe para este alerta." });
            var x = new CitizenNotification { CitizenId = citizenId, OfficialAlertId = request.OfficialAlertId, Title = request.Title.Trim(), Message = request.Message.Trim(), Status = "Pending", CreatedAt = DateTime.UtcNow }; db.Add(x); await db.SaveChangesAsync(ct); return Results.Created($"/api/notificacoes/{x.Id}", EndpointHelpers.ToResponse(x));
        });
        
        g.MapPut("/{id:int}", async (int id, UpdateNotificationRequest request, HttpContext c, AlertaDbContext db, CancellationToken ct) =>
        {
            var estado = EndpointHelpers.StoredNotificationStatus(request.Status); if (estado is not ("Pending" or "Read" or "Cancelled" or "Sent")) return Results.BadRequest(new { erro = "Situação inválida." }); var x = await db.CitizenNotifications.FindAsync([id], ct); if (x is null) return Results.NotFound(); if (!EndpointHelpers.IsAdmin(c.User) && x.CitizenId != EndpointHelpers.UserId(c.User)) return Results.Forbid(); x.Status = estado; x.SentAt = request.SentAt; await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        
        g.MapDelete("/{id:int}", async (int id, AlertaDbContext db, CancellationToken ct) => { var x = await db.CitizenNotifications.FindAsync([id], ct); if (x is null) return Results.NotFound(); db.Remove(x); await db.SaveChangesAsync(ct); return Results.NoContent(); }).RequireAuthorization("Administrador");
        
        
        return g;
    }
}

