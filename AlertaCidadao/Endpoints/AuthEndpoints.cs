using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;
using AlertaCidadao.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AlertaCidadao.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/autenticacao").WithTags("Autenticação").RequireRateLimiting("publica");
        group.MapPost("/cadastro", async (RegisterRequest request, AlertaDbContext db, MunicipalityCatalogService municipalities, IPasswordHasher<Citizen> hasher, CancellationToken ct) =>
        {
            var errors = EndpointHelpers.ValidateRegistration(request); if (errors.Count != 0) return Results.ValidationProblem(errors);
            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.Citizens.AnyAsync(x => x.Email == email, ct)) return Results.Conflict(new { erro = "E-mail já cadastrado." });
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
            var citizen = new Citizen { Name = request.Name.Trim(), Email = email, Municipality = municipality.Name, MunicipalityIbgeCode = municipality.IbgeCode, Neighborhood = request.Neighborhood?.Trim(), PostalCode = request.PostalCode?.Trim() };
            citizen.PasswordHash = hasher.HashPassword(citizen, request.Password); db.Citizens.Add(citizen);
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Results.Conflict(new { erro = "E-mail já cadastrado." }); }
            return Results.Created($"/api/cidadaos/{citizen.Id}", EndpointHelpers.ToResponse(citizen, municipality.Uf));
        }).AllowAnonymous().WithSummary("Cadastra um cidadão");
        
        group.MapPost("/entrar", async (LoginRequest request, AlertaDbContext db, IPasswordHasher<Citizen> hasher, IConfiguration config, CancellationToken ct) =>
        {
            var email = request.Email.Trim().ToLowerInvariant(); var citizen = await db.Citizens.SingleOrDefaultAsync(x => x.Email == email, ct);
            if (citizen is null || hasher.VerifyHashedPassword(citizen, citizen.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return Results.Unauthorized();
            var expiry = DateTime.UtcNow.AddMinutes(config.GetValue("Jwt:ExpiryMinutes", 60));
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, citizen.Id.ToString()), new Claim(ClaimTypes.Name, citizen.Name), new Claim(ClaimTypes.Role, EndpointHelpers.PublicRole(citizen.Role)), new Claim(ClaimTypes.Email, citizen.Email) };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"] ?? "local-development-signing-key-change-before-use-2026"));
            var token = new JwtSecurityToken(config["Jwt:Issuer"] ?? "AlertaCidadao", config["Jwt:Audience"] ?? "AlertaCidadao.Client", claims, expires: expiry, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
            return Results.Ok(new AuthTokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expiry));
        }).AllowAnonymous().WithSummary("Autentica um cidadão e retorna JWT");
        
        
        return group;
    }
}
