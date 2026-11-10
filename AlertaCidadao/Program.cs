using System.Text;
using AlertaCidadao.Data;
using AlertaCidadao.Endpoints;
using AlertaCidadao.Entities;
using AlertaCidadao.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
var connection = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Configure a connection string ConnectionStrings:Default via variável de ambiente.");
var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Configure Jwt:SigningKey via variável de ambiente.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32) throw new InvalidOperationException("Jwt:SigningKey deve conter pelo menos 32 bytes.");
builder.Services.AddDbContext<AlertaDbContext>(o => o.UseMySQL(connection));
builder.Services.AddScoped<IPasswordHasher<Citizen>, PasswordHasher<Citizen>>();
builder.Services.AddScoped<IdempotencyService>();
builder.Services.AddScoped<AlertSyncService>();
builder.Services.AddHttpClient<RadarMeteorologicoAlertProvider>(client =>
{
    client.BaseAddress = new Uri("https://radarmeteorologico.com.br/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AlertaCidadao/1.0");
});
builder.Services.AddHttpClient<MunicipalityCatalogService>(client =>
{
    client.BaseAddress = new Uri("https://servicodados.ibge.gov.br/api/v1/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AlertaCidadao/1.0");
});
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.AddHostedService<AlertMonitoringBackgroundService>();
builder.Services.AddHealthChecks().AddDbContextCheck<AlertaDbContext>("bancoMySQL");
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "AlertaCidadão — API de riscos urbanos", Version = "v1", Description = "API de registro de ocorrências cidadãs e consulta de alertas meteorológicos externos. Alertas externos não representam ocorrências confirmadas." });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    o.OperationFilter<SwaggerAuthorizationOperationFilter>();
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new()
{
    ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "AlertaCidadao",
    ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "AlertaCidadao.Client",
    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
    ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30)
});
builder.Services.AddAuthorization(o => o.AddPolicy("Administrador", p => p.RequireRole("Administrador")));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("publica", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 60), Window = TimeSpan.FromMinutes(builder.Configuration.GetValue("RateLimiting:WindowMinutes", 1)), QueueLimit = 0, AutoReplenishment = true }));
});

var app = builder.Build();
app.UseExceptionHandler(e => e.Run(async c => { c.Response.StatusCode = 500; c.Response.ContentType = "application/problem+json"; await c.Response.WriteAsJsonAsync(new { title = "Erro interno", status = 500 }); }));
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
if (app.Environment.IsDevelopment()) { app.MapOpenApi(); app.UseSwagger(); app.UseSwaggerUI(); }
app.MapGet("/health/live", () => Results.Ok(new { situacao = "Saudável" })).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var situacao = report.Status switch
        {
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => "Saudável",
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => "Degradada",
            _ => "Indisponível"
        };
        await context.Response.WriteAsJsonAsync(new { situacao, verificacoes = report.Entries.ToDictionary(x => x.Key, x => x.Value.Status switch { Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => "Saudável", Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => "Degradada", _ => "Indisponível" }) });
    }
}).AllowAnonymous();
app.MapMunicipalityEndpoints(); app.MapAuthEndpoints(); app.MapCitizenEndpoints(); app.MapOccurrenceEndpoints(); app.MapAlertEndpoints(); app.MapNotificationEndpoints(); app.MapMonitoringEndpoints();
app.Run();

public partial class Program;
