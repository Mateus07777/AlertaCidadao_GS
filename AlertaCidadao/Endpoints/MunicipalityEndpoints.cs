using AlertaCidadao.Services;

namespace AlertaCidadao.Endpoints;

public static class MunicipalityEndpoints
{
    public static RouteGroupBuilder MapMunicipalityEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/localidades").WithTags("Localidades").RequireRateLimiting("publica");

        group.MapGet("/ufs", () => Results.Ok(MunicipalityCatalogService.GetStates()))
            .AllowAnonymous()
            .WithSummary("Lista as unidades federativas");

        group.MapGet("/municipios", async (string? uf, MunicipalityCatalogService catalog, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(uf))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["uf"] = ["Informe a sigla da unidade federativa."] });

            try
            {
                return Results.Ok(await catalog.GetMunicipalitiesAsync(uf, ct));
            }
            catch (ArgumentException ex)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["uf"] = [ex.Message] });
            }
            catch (MunicipalityCatalogUnavailableException ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Catálogo de municípios indisponível", detail: ex.Message);
            }
        })
            .AllowAnonymous()
            .WithSummary("Lista os municípios de uma unidade federativa")
            .WithDescription("A lista é obtida do catálogo oficial do IBGE na primeira consulta para cada UF e depois fica armazenada no MySQL.");

        return group;
    }
}
