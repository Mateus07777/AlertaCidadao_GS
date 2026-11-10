using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;
using AlertaCidadao.Services;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Endpoints;

public static class OccurrenceEndpoints
{
    public static RouteGroupBuilder MapOccurrenceEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/ocorrencias").WithTags("Ocorrências").RequireAuthorization();
        
        g.MapGet("", async (HttpContext c, int? pagina, int? tamanhoPagina, string? situacao, AlertaDbContext db, CancellationToken ct) =>
        {
            var q = db.CitizenOccurrences.AsNoTracking().AsQueryable(); if (!EndpointHelpers.IsAdmin(c.User)) q = q.Where(x => x.CitizenId == EndpointHelpers.UserId(c.User)); if (!string.IsNullOrWhiteSpace(situacao)) q = q.Where(x => x.Status == EndpointHelpers.StoredOccurrenceStatus(situacao));
            var pg = Math.Max(pagina ?? 1, 1); var size = Math.Clamp(tamanhoPagina ?? 20, 1, 100); var rows = await q.OrderByDescending(x => x.CreatedAt).Skip((pg - 1) * size).Take(size).ToListAsync(ct); return Results.Ok(rows.Select(EndpointHelpers.ToResponse));
        });
        
        g.MapGet("/{id:int}", async (int id, HttpContext c, AlertaDbContext db, CancellationToken ct) =>
        {
            var x = await db.CitizenOccurrences.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return Results.NotFound(); if (!EndpointHelpers.IsAdmin(c.User) && x.CitizenId != EndpointHelpers.UserId(c.User)) return Results.Forbid(); return Results.Ok(EndpointHelpers.ToResponse(x));
        });
        
        g.MapPost("", async (CreateOccurrenceRequest request, HttpContext c, AlertaDbContext db, IdempotencyService idem, CancellationToken ct) =>
        {
            var errors = EndpointHelpers.ValidateOccurrence(request); if (errors.Count != 0) return Results.ValidationProblem(errors);
            var key = c.Request.Headers["Chave-Idempotencia"].FirstOrDefault(); if (string.IsNullOrWhiteSpace(key) || key.Length > 100) return Results.BadRequest(new { erro = "Informe Chave-Idempotencia (até 100 caracteres)." });
            var hash = EndpointHelpers.Hash(request); var prior = await idem.FindAsync(key, hash, ct); if (prior is not null) return Results.Content(prior.ResponseBody, "application/json", statusCode: prior.ResponseStatusCode);
            if (await idem.KeyExistsAsync(key, ct)) return Results.Conflict(new { erro = "Chave já utilizada com outro conteúdo." });
            var x = new CitizenOccurrence { CitizenId = EndpointHelpers.UserId(c.User), Title = request.Title.Trim(), Description = request.Description.Trim(), Type = request.Type.Trim(), Severity = request.Severity.Trim(), Municipality = request.Municipality.Trim(), Neighborhood = request.Neighborhood, PostalCode = request.PostalCode, Latitude = request.Latitude, Longitude = request.Longitude, Status = "Open", CreatedAt = DateTime.UtcNow };
            var body = System.Text.Json.JsonSerializer.Serialize(EndpointHelpers.ToResponse(x));
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                db.Add(x);
                db.IdempotencyRecords.Add(new IdempotencyRecord { IdempotencyKey = key, RequestHash = hash, ResponseStatusCode = 201, ResponseBody = body, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(24) });
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
                return Results.Content(body, "application/json", statusCode: 201);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
                var concurrent = await idem.FindAsync(key, hash, ct);
                if (concurrent is not null) return Results.Content(concurrent.ResponseBody, "application/json", statusCode: concurrent.ResponseStatusCode);
                if (await idem.KeyExistsAsync(key, ct)) return Results.Conflict(new { erro = "Chave já utilizada com outro conteúdo." });
                throw;
            }
        }).RequireRateLimiting("publica").WithSummary("Registra uma ocorrência; exige Chave-Idempotencia").WithDescription("Envie Chave-Idempotencia com até 100 caracteres. A mesma chave e o mesmo conteúdo reproduzem a resposta original por 24 horas.");
        
        g.MapPut("/{id:int}", async (int id, UpdateOccurrenceRequest request, HttpContext c, AlertaDbContext db, CancellationToken ct) =>
        {
            var x = await db.CitizenOccurrences.FindAsync([id], ct); if (x is null) return Results.NotFound(); if (!EndpointHelpers.IsAdmin(c.User) && x.CitizenId != EndpointHelpers.UserId(c.User)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description) || string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Severity) || string.IsNullOrWhiteSpace(request.Municipality)) return Results.BadRequest(new { erro = "Título, descrição, tipo, severidade e município são obrigatórios." });
            x.Title = request.Title.Trim(); x.Description = request.Description.Trim(); x.Type = request.Type.Trim(); x.Severity = request.Severity.Trim(); x.Municipality = request.Municipality.Trim(); x.Neighborhood = request.Neighborhood; x.PostalCode = request.PostalCode; x.Latitude = request.Latitude; x.Longitude = request.Longitude; if (EndpointHelpers.IsAdmin(c.User) && request.Status is not null) x.Status = EndpointHelpers.StoredOccurrenceStatus(request.Status); x.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        
        g.MapDelete("/{id:int}", async (int id, HttpContext c, AlertaDbContext db, CancellationToken ct) =>
        {
            var x = await db.CitizenOccurrences.FindAsync([id], ct); if (x is null) return Results.NotFound(); if (!EndpointHelpers.IsAdmin(c.User) && x.CitizenId != EndpointHelpers.UserId(c.User)) return Results.Forbid(); db.Remove(x); await db.SaveChangesAsync(ct); return Results.NoContent();
        });
        
        
        return g;
    }
}


