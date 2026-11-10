using System.Net;
using AlertaCidadao.Endpoints;
using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;
using AlertaCidadao.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlertaCidadao.Tests;

public sealed class ProviderAndValidationTests
{
    [Fact]
    public async Task Provider_MapsDocumentedFieldsAndMunicipalityCodes()
    {
        const string json = """{"fonte":"INMET — Instituto Nacional de Meteorologia","alertas":[{"id":56340,"evento":"Tempestade","severidade":"Perigo","nivel":2,"inicio":"2026-11-07 15:47","fim":"2026-11-07 23:59","futuro":false,"geocodes":[3509502,3550308]}]}""";
        var handler = new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var provider = new RadarMeteorologicoAlertProvider(new HttpClient(handler) { BaseAddress = new Uri("https://radarmeteorologico.com.br/") }, NullLogger<RadarMeteorologicoAlertProvider>.Instance);
        var alerts = await provider.GetAlertsAsync(CancellationToken.None);
        Assert.Single(alerts); Assert.Equal(56340, alerts[0].Id); Assert.Equal("Tempestade", alerts[0].Evento); Assert.Equal(new[] { 3509502, 3550308 }, alerts[0].Geocodes);
    }

    [Fact]
    public async Task Provider_RejectsInvalidPayload()
    {
        var provider = new RadarMeteorologicoAlertProvider(new HttpClient(new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") })) { BaseAddress = new Uri("https://radarmeteorologico.com.br/") }, NullLogger<RadarMeteorologicoAlertProvider>.Instance);
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => provider.GetAlertsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Provider_PropagatesHttpFailure()
    {
        var provider = new RadarMeteorologicoAlertProvider(new HttpClient(new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))) { BaseAddress = new Uri("https://radarmeteorologico.com.br/") }, NullLogger<RadarMeteorologicoAlertProvider>.Instance);
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetAlertsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Provider_ToleratesOptionalFieldsMissing()
    {
        const string json = """{"alertas":[{"id":42,"evento":"Chuva forte"}]}""";
        var provider = new RadarMeteorologicoAlertProvider(new HttpClient(new StaticHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) })) { BaseAddress = new Uri("https://radarmeteorologico.com.br/") }, NullLogger<RadarMeteorologicoAlertProvider>.Instance);
        var alerts = await provider.GetAlertsAsync(CancellationToken.None);
        Assert.Single(alerts); Assert.Equal("Não informada", alerts[0].Severidade); Assert.Empty(alerts[0].Geocodes); Assert.Null(alerts[0].Inicio);
    }

    [Fact]
    public async Task Provider_PropagatesTimeoutCancellation()
    {
        var provider = new RadarMeteorologicoAlertProvider(new HttpClient(new StaticHandler(_ => throw new TaskCanceledException("timeout"))) { BaseAddress = new Uri("https://radarmeteorologico.com.br/") }, NullLogger<RadarMeteorologicoAlertProvider>.Instance);
        await Assert.ThrowsAsync<TaskCanceledException>(() => provider.GetAlertsAsync(CancellationToken.None));
    }

    [Fact]
    public void Registration_ValidatesRequiredFieldsAndPasswordLength()
    {
        var errors = EndpointHelpers.ValidateRegistration(new RegisterRequest("", "invalid", "123", "", null, null, null));
        Assert.Contains("name", errors.Keys); Assert.Contains("email", errors.Keys); Assert.Contains("password", errors.Keys); Assert.Contains("municipality", errors.Keys);
    }

    [Fact]
    public void IdempotencyHash_IsStableAndChangesWithRequest()
    {
        var a = new CreateOccurrenceRequest("Alagamento", "Rua inundada", "Flood", "High", "Campinas", null, null, null, null);
        Assert.Equal(EndpointHelpers.Hash(a), EndpointHelpers.Hash(a));
        Assert.NotEqual(EndpointHelpers.Hash(a), EndpointHelpers.Hash(a with { Severity = "Medium" }));
    }

    [Fact]
    public void PasswordHasher_StoresVerifiableHashInsteadOfPlaintext()
    {
        var hasher = new PasswordHasher<Citizen>(); var citizen = new Citizen { Email = "test@example.com" };
        var hash = hasher.HashPassword(citizen, "senha-segura-123");
        Assert.NotEqual("senha-segura-123", hash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(citizen, hash, "senha-segura-123"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(citizen, hash, "outra-senha"));
    }

    [Fact]
    public void DbModel_DeclaresUniquenessAndRestrictedOwnershipRelations()
    {
        var options = new DbContextOptionsBuilder<AlertaDbContext>().UseMySQL("Server=localhost;Database=metadata_only;User=test;Password=test").Options;
        using var db = new AlertaDbContext(options);
        var citizen = db.Model.FindEntityType(typeof(Citizen))!;
        Assert.Contains(citizen.GetIndexes(), index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(Citizen.Email)));
        var notification = db.Model.FindEntityType(typeof(CitizenNotification))!;
        Assert.Contains(notification.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(CitizenNotification.CitizenId), nameof(CitizenNotification.OfficialAlertId)]));
        var occurrence = db.Model.FindEntityType(typeof(CitizenOccurrence))!;
        Assert.Equal(DeleteBehavior.Restrict, occurrence.GetForeignKeys().Single().DeleteBehavior);
    }

    private sealed class StaticHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
