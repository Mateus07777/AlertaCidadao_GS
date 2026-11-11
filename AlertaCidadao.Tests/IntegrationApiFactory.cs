using AlertaCidadao.Data;
using AlertaCidadao.Entities;
using AlertaCidadao.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AlertaCidadao.Tests;

internal sealed class IntegrationApiFactory(int rateLimit = 100) : WebApplicationFactory<Program>
{
    internal const string SigningKey = "integration-test-signing-key-with-at-least-32-bytes";
    private readonly string _databaseName = $"alertacidadao-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=localhost;Database=ignored;User=test;Password=test",
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = "AlertaCidadao",
                ["Jwt:Audience"] = "AlertaCidadao.Client",
                ["Monitoring:RunOnStartup"] = "false",
                ["RateLimiting:PermitLimit"] = rateLimit.ToString(),
                ["RateLimiting:WindowMinutes"] = "10"
            }));

        builder.ConfigureServices(services =>
        {
            var testConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = "AlertaCidadao",
                ["Jwt:Audience"] = "AlertaCidadao.Client",
                ["Jwt:ExpiryMinutes"] = "60"
            }).Build();
            services.AddSingleton<IConfiguration>(testConfiguration);
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
                options.TokenValidationParameters.ValidIssuer = "AlertaCidadao";
                options.TokenValidationParameters.ValidAudience = "AlertaCidadao.Client";
            });

            var hostedMonitor = services.FirstOrDefault(x => x.ServiceType == typeof(IHostedService)
                && x.ImplementationType == typeof(AlertMonitoringBackgroundService));
            if (hostedMonitor is not null) services.Remove(hostedMonitor);

            services.RemoveAll<DbContextOptions<AlertaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AlertaDbContext>>();
            services.RemoveAll<AlertaDbContext>();
            services.AddDbContext<AlertaDbContext>(options => options
                .UseInMemoryDatabase(_databaseName)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }
}
