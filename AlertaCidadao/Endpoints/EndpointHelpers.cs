using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlertaCidadao.Entities;
using AlertaCidadao.Models;

namespace AlertaCidadao.Endpoints;

public static class EndpointHelpers
{
    public static int UserId(ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole("Administrador");
    public static string PublicRole(string role) => role == "Admin" ? "Administrador" : "Cidadão";
    public static string PublicOccurrenceStatus(string status) => status switch { "Open" => "Aberta", "InProgress" => "Em andamento", "Resolved" => "Resolvida", "Cancelled" => "Cancelada", _ => status };
    public static string StoredOccurrenceStatus(string status) => status switch { "Aberta" => "Open", "Em andamento" => "InProgress", "Resolvida" => "Resolved", "Cancelada" => "Cancelled", _ => status };
    public static string PublicNotificationStatus(string status) => status switch { "Pending" => "Pendente", "Read" => "Lida", "Cancelled" => "Cancelada", "Sent" => "Enviada", _ => status };
    public static string StoredNotificationStatus(string status) => status switch { "Pendente" => "Pending", "Lida" => "Read", "Cancelada" => "Cancelled", "Enviada" => "Sent", _ => status };
    public static string PublicExecutionStatus(string status) => status switch { "Running" => "Em execução", "Succeeded" => "Concluída", "Partial" => "Concluída parcialmente", "Failed" => "Falhou", "Cancelled" => "Cancelada", _ => status };
    public static string StoredExecutionStatus(string status) => status switch { "Em execução" => "Running", "Concluída" => "Succeeded", "Concluída parcialmente" => "Partial", "Falhou" => "Failed", "Cancelada" => "Cancelled", _ => status };
    public static OccurrenceResponse ToResponse(CitizenOccurrence x) => new(x.Id, x.CitizenId, x.Title, x.Description, x.Type, x.Severity, PublicOccurrenceStatus(x.Status), x.Municipality, x.Neighborhood, x.PostalCode, x.Latitude, x.Longitude, x.CreatedAt);
    public static CitizenResponse ToResponse(Citizen x, string? uf = null) => new(x.Id, x.Name, x.Email, x.Municipality, uf, x.MunicipalityIbgeCode, x.Neighborhood, x.PostalCode, PublicRole(x.Role), x.CreatedAt);
    public static OfficialAlertResponse ToResponse(OfficialAlert x) => new(x.Id, x.ExternalIdentifier, x.Title, x.Description, x.EventType, x.Severity, x.Level, x.IsFuture, x.MunicipalityIbgeCode, x.AffectedMunicipalityCodes, x.Source, x.SourceUrl, x.StartsAt, x.ExpiresAt, x.RetrievedAt, x.CreatedAt, x.UpdatedAt);
    public static NotificationResponse ToResponse(CitizenNotification x) => new(x.Id, x.CitizenId, x.OfficialAlertId, x.Title, x.Message, PublicNotificationStatus(x.Status), x.CreatedAt, x.SentAt);
    public static MonitoringExecutionResponse ToResponse(MonitoringExecution x) => new(x.Id, x.Provider, x.StartedAt, x.FinishedAt, PublicExecutionStatus(x.Status), x.RecordsFound, x.RecordsCreated, x.RecordsUpdated, x.RecordsDuplicated, x.ErrorMessage);
    public static string Hash<T>(T input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
    public static Dictionary<string, string[]> ValidateRegistration(RegisterRequest x)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(x.Name)) errors["nome"] = ["O nome é obrigatório."];
        if (string.IsNullOrWhiteSpace(x.Email) || !x.Email.Contains('@')) errors["email"] = ["O e-mail é inválido."];
        if (x.Password is null || x.Password.Length < 10) errors["senha"] = ["A senha deve ter ao menos 10 caracteres."];
        if (string.IsNullOrWhiteSpace(x.Municipality)) errors["municipio"] = ["O município é obrigatório."];
        if (string.IsNullOrWhiteSpace(x.Uf) || x.Uf.Trim().Length != 2) errors["uf"] = ["Informe a sigla da unidade federativa com duas letras."];
        return errors;
    }
    public static Dictionary<string, string[]> ValidateOccurrence(CreateOccurrenceRequest x)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(x.Title)) errors["titulo"] = ["O título é obrigatório."];
        if (string.IsNullOrWhiteSpace(x.Description)) errors["descricao"] = ["A descrição é obrigatória."];
        if (string.IsNullOrWhiteSpace(x.Type)) errors["tipo"] = ["O tipo é obrigatório."];
        if (string.IsNullOrWhiteSpace(x.Severity)) errors["severidade"] = ["A severidade é obrigatória."];
        if (string.IsNullOrWhiteSpace(x.Municipality)) errors["municipio"] = ["O município é obrigatório."];
        if (x.Latitude is < -90 or > 90 || x.Longitude is < -180 or > 180) errors["coordenadas"] = ["As coordenadas estão fora da faixa válida."];
        return errors;
    }
}
