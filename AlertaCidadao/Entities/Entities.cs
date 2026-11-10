namespace AlertaCidadao.Entities;

public sealed class Citizen
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Municipality { get; set; } = "";
    public int? MunicipalityIbgeCode { get; set; }
    public string? Neighborhood { get; set; }
    public string? PostalCode { get; set; }
    public string Role { get; set; } = "Citizen";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<CitizenOccurrence> Occurrences { get; set; } = [];
    public List<CitizenNotification> Notifications { get; set; } = [];
}

public sealed class Municipality
{
    public int IbgeCode { get; set; }
    public string Name { get; set; } = "";
    public string Uf { get; set; } = "";
    public string NormalizedName { get; set; } = "";
}

public sealed class OfficialAlert
{
    public int Id { get; set; }
    public long ExternalIdentifier { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string EventType { get; set; } = "";
    public string Severity { get; set; } = "";
    public int? Level { get; set; }
    public bool? IsFuture { get; set; }
    public int? MunicipalityIbgeCode { get; set; }
    public string AffectedMunicipalityCodes { get; set; } = "[]";
    public string Source { get; set; } = "INMET via RadarMeteorológico";
    public string SourceUrl { get; set; } = "https://radarmeteorologico.com.br/api-publica";
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime RetrievedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<CitizenNotification> Notifications { get; set; } = [];
}

public sealed class CitizenOccurrence
{
    public int Id { get; set; }
    public int CitizenId { get; set; }
    public Citizen Citizen { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Status { get; set; } = "Open";
    public string Municipality { get; set; } = "";
    public string? Neighborhood { get; set; }
    public string? PostalCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CitizenNotification
{
    public int Id { get; set; }
    public int CitizenId { get; set; }
    public Citizen Citizen { get; set; } = null!;
    public int OfficialAlertId { get; set; }
    public OfficialAlert OfficialAlert { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public sealed class MonitoringExecution
{
    public int Id { get; set; }
    public string Provider { get; set; } = "RadarMeteorologicoAlertProvider";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";
    public int RecordsFound { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsDuplicated { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class IdempotencyRecord
{
    public int Id { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
