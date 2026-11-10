using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;
using AlertaCidadao.Services;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Endpoints;

public static class CitizenEndpoints
{
    public static RouteGroupBuilder MapCitizenEndpoints(this IEndpointRouteBuilder routes)
    {
        var g = routes.MapGroup("/api/cidadaos").WithTags("Cidadãos").RequireAuthorization();

        g.MapGet("/eu", async (HttpContext c, AlertaDbContext db, CancellationToken ct) => await Get(EndpointHelpers.UserId(c.User), db, ct));

        g.MapGet("", async (int? pagina, int? tamanhoPagina, AlertaDbContext db, CancellationToken ct) =>
        {
            var pg = Math.Max(pagina ?? 1, 1); var size = Math.Clamp(tamanhoPagina ?? 20, 1, 100);
            var rows = await (from citizen in db.Citizens.AsNoTracking()
                              join municipality in db.Municipalities.AsNoTracking() on citizen.MunicipalityIbgeCode equals (int?)municipality.IbgeCode into catalog
                              from municipality in catalog.DefaultIfEmpty()
                              orderby citizen.Id
                              select new CitizenResponse(citizen.Id, citizen.Name, citizen.Email, citizen.Municipality, municipality == null ? null : municipality.Uf, citizen.MunicipalityIbgeCode, citizen.Neighborhood, citizen.PostalCode, EndpointHelpers.PublicRole(citizen.Role), citizen.CreatedAt))
                .Skip((pg - 1) * size).Take(size).ToListAsync(ct);
            return Results.Ok(rows);
        }).RequireAuthorization("Administrador");

        g.MapGet("/{id:int}", async (int id, HttpContext c, AlertaDbContext db, CancellationToken ct) => !EndpointHelpers.IsAdmin(c.User) && EndpointHelpers.UserId(c.User) != id ? Results.Forbid() : await Get(id, db, ct));
       
        g.MapPut("/{id:int}", async (int id, UpdateCitizenRequest request, HttpContext c, AlertaDbContext db, MunicipalityCatalogService municipalities, CancellationToken ct) =>
        {
            if (!EndpointHelpers.IsAdmin(c.User) && EndpointHelpers.UserId(c.User) != id) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Municipality) || string.IsNullOrWhiteSpace(request.Uf)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["nome"] = ["Nome, município e UF são obrigatórios."] });
            var citizen = await db.Citizens.FindAsync([id], ct); if (citizen is null) return Results.NotFound();
            Municipality municipality;
            try
            {
                municipality = await municipalities.ResolveAsync(request.Municipality, request.Uf, ct)
                    ?? throw new ArgumentException("O município não foi encontrado na unidade federativa informada.");
            }
            catch (MunicipalityCatalogUnavailableException ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Catálogo de municípios indisponível", detail: ex.Message);
            }
            catch (ArgumentException ex)
            {
                var campo = ex.ParamName == "uf" ? "uf" : "municipio";
                return Results.ValidationProblem(new Dictionary<string, string[]> { [campo] = [ex.Message] });
            }
            citizen.Name = request.Name.Trim(); citizen.Municipality = municipality.Name; citizen.MunicipalityIbgeCode = municipality.IbgeCode; citizen.Neighborhood = request.Neighborhood; citizen.PostalCode = request.PostalCode; citizen.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); return Results.NoContent();
        });

        g.MapDelete("/{id:int}", async (int id, HttpContext c, AlertaDbContext db, CancellationToken ct) =>
        {
            if (!EndpointHelpers.IsAdmin(c.User) && EndpointHelpers.UserId(c.User) != id) return Results.Forbid();
            var citizen = await db.Citizens.FindAsync([id], ct); if (citizen is null) return Results.NotFound();
            if (await db.CitizenOccurrences.AnyAsync(x => x.CitizenId == id, ct) || await db.CitizenNotifications.AnyAsync(x => x.CitizenId == id, ct)) return Results.Conflict(new { erro = "Cidadão possui registros associados." });
            db.Citizens.Remove(citizen); await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("Administrador");


        return g;
    }

    private static async Task<IResult> Get(int id, AlertaDbContext db, CancellationToken ct)
    {
        var x = await (from citizen in db.Citizens.AsNoTracking()
                       join municipality in db.Municipalities.AsNoTracking() on citizen.MunicipalityIbgeCode equals (int?)municipality.IbgeCode into catalog
                       from municipality in catalog.DefaultIfEmpty()
                       where citizen.Id == id
                       select new CitizenResponse(citizen.Id, citizen.Name, citizen.Email, citizen.Municipality, municipality == null ? null : municipality.Uf, citizen.MunicipalityIbgeCode, citizen.Neighborhood, citizen.PostalCode, EndpointHelpers.PublicRole(citizen.Role), citizen.CreatedAt))
            .SingleOrDefaultAsync(ct);
        return x is null ? Results.NotFound() : Results.Ok(x);
    }
}


