using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AlertaCidadao.Tests;

public sealed class HttpEndpointIntegrationTests
{
    [Fact]
    public async Task CitizenEndpoints_RequireJwt_AndReturnCitizenIbgeCode()
    {
        using var factory = new IntegrationApiFactory();
        using var client = factory.CreateClient();
        await SeedCitizenAsync(factory, "cidadao@example.com", "Citizen", "senha-integration-2026");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cidadaos/eu")).StatusCode);

        var token = await LoginAsync(client, "cidadao@example.com", "senha-integration-2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/cidadaos/eu");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"codigoIbgeMunicipio\":3509502", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminEndpoints_RejectCitizenAndAcceptAdminToken()
    {
        using var factory = new IntegrationApiFactory();
        using var client = factory.CreateClient();
        await SeedCitizenAsync(factory, "cidadao@example.com", "Citizen", "senha-integration-2026");
        await SeedCitizenAsync(factory, "admin@example.com", "Admin", "senha-admin-2026");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "cidadao@example.com", "senha-integration-2026"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/execucoes-monitoramento")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "admin@example.com", "senha-admin-2026"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/execucoes-monitoramento")).StatusCode);
    }

    [Fact]
    public async Task OccurrencePost_ReplaysSameResponse_AndRejectsDifferentBodyForSameKey()
    {
        using var factory = new IntegrationApiFactory();
        using var client = factory.CreateClient();
        await SeedCitizenAsync(factory, "cidadao@example.com", "Citizen", "senha-integration-2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await LoginAsync(client, "cidadao@example.com", "senha-integration-2026"));

        const string request = "{\"titulo\":\"Alagamento\",\"descricao\":\"Água na via\",\"tipo\":\"Alagamento\",\"severidade\":\"Alta\",\"municipio\":\"Campinas\",\"bairro\":null,\"cep\":null,\"latitude\":null,\"longitude\":null}";
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ocorrencias") { Content = new StringContent(request, Encoding.UTF8, "application/json") };
        firstRequest.Headers.Add("Chave-Idempotencia", "integration-occurrence-1");
        var first = await client.SendAsync(firstRequest);
        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.DoesNotContain("\"id\":0", firstBody, StringComparison.OrdinalIgnoreCase);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ocorrencias") { Content = new StringContent(request, Encoding.UTF8, "application/json") };
        replayRequest.Headers.Add("Chave-Idempotencia", "integration-occurrence-1");
        var replay = await client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(firstBody, await replay.Content.ReadAsStringAsync());

        using var changedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ocorrencias") { Content = new StringContent(request.Replace("Água na via", "Risco diferente"), Encoding.UTF8, "application/json") };
        changedRequest.Headers.Add("Chave-Idempotencia", "integration-occurrence-1");
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(changedRequest)).StatusCode);

        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<AlertaDbContext>().CitizenOccurrences.Count());
    }

    [Fact]
    public async Task RateLimiter_Returns429WhenConfiguredLimitIsExceeded()
    {
        using var factory = new IntegrationApiFactory(rateLimit: 1);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/localidades/ufs")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/localidades/ufs")).StatusCode);
    }

    [Fact]
    public async Task LivenessEndpoint_IsReachableWithoutAuthentication()
    {
        using var factory = new IntegrationApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task SwaggerDocument_DescribesJwtAndIdempotencyHeader()
    {
        using var factory = new IntegrationApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadAsStringAsync();
        Assert.Contains("Bearer", document, StringComparison.Ordinal);
        Assert.Contains("Chave-Idempotencia", document, StringComparison.Ordinal);
        Assert.Contains("/api/ocorrencias", document, StringComparison.Ordinal);
        Assert.Contains("/health/live", document, StringComparison.Ordinal);
        Assert.Contains("/health/ready", document, StringComparison.Ordinal);
        Assert.Contains("/health/integrations", document, StringComparison.Ordinal);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/autenticacao/entrar", new { email, senha = password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("tokenAcesso").GetString()!;
    }

    private static async Task SeedCitizenAsync(IntegrationApiFactory factory, string email, string role, string password)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AlertaDbContext>();
        var citizen = new Citizen
        {
            Name = role == "Admin" ? "Administrador" : "Cidadão Teste",
            Email = email,
            Municipality = "Franca",
            MunicipalityIbgeCode = 3509502,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        citizen.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Citizen>>().HashPassword(citizen, password);
        db.Citizens.Add(citizen);
        await db.SaveChangesAsync();
    }
}
