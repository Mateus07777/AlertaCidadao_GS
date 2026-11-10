using System.Text.Json.Serialization;

namespace AlertaCidadao.Models;

public sealed record RegisterRequest(
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("senha")] string Password,
    [property: JsonPropertyName("municipio")] string Municipality,
    [property: JsonPropertyName("uf")] string Uf,
    [property: JsonPropertyName("bairro")] string? Neighborhood,
    [property: JsonPropertyName("cep")] string? PostalCode);

public sealed record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("senha")] string Password);

public sealed record AuthTokenResponse(
    [property: JsonPropertyName("tokenAcesso")] string AccessToken,
    [property: JsonPropertyName("expiraEm")] DateTime ExpiresAt);

public sealed record UpdateCitizenRequest(
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("municipio")] string Municipality,
    [property: JsonPropertyName("uf")] string Uf,
    [property: JsonPropertyName("bairro")] string? Neighborhood,
    [property: JsonPropertyName("cep")] string? PostalCode);

public sealed record CitizenResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("nome")] string Name,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("municipio")] string Municipality,
    [property: JsonPropertyName("uf")] string? Uf,
    [property: JsonPropertyName("codigoIbgeMunicipio")] int? MunicipalityIbgeCode,
    [property: JsonPropertyName("bairro")] string? Neighborhood,
    [property: JsonPropertyName("cep")] string? PostalCode,
    [property: JsonPropertyName("perfil")] string Role,
    [property: JsonPropertyName("criadoEm")] DateTime CreatedAt);

public sealed record CreateOccurrenceRequest(
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("descricao")] string Description,
    [property: JsonPropertyName("tipo")] string Type,
    [property: JsonPropertyName("severidade")] string Severity,
    [property: JsonPropertyName("municipio")] string Municipality,
    [property: JsonPropertyName("bairro")] string? Neighborhood,
    [property: JsonPropertyName("cep")] string? PostalCode,
    [property: JsonPropertyName("latitude")] decimal? Latitude,
    [property: JsonPropertyName("longitude")] decimal? Longitude);

public sealed record UpdateOccurrenceRequest(
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("descricao")] string Description,
    [property: JsonPropertyName("tipo")] string Type,
    [property: JsonPropertyName("severidade")] string Severity,
    [property: JsonPropertyName("municipio")] string Municipality,
    [property: JsonPropertyName("bairro")] string? Neighborhood,
    [property: JsonPropertyName("cep")] string? PostalCode,
    [property: JsonPropertyName("latitude")] decimal? Latitude,
    [property: JsonPropertyName("longitude")] decimal? Longitude,
    [property: JsonPropertyName("situacao")] string? Status);

public sealed record OccurrenceResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("cidadaoId")] int CitizenId,
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("descricao")] string Description,
    [property: JsonPropertyName("tipo")] string Type,
    [property: JsonPropertyName("severidade")] string Severity,
    [property: JsonPropertyName("situacao")] string Status,
    [property: JsonPropertyName("municipio")] string Municipality,
    [property: JsonPropertyName("bairro")] string? Neighborhood,
    [property: JsonPropertyName("cep")] string? PostalCode,
    [property: JsonPropertyName("latitude")] decimal? Latitude,
    [property: JsonPropertyName("longitude")] decimal? Longitude,
    [property: JsonPropertyName("criadoEm")] DateTime CreatedAt);

public sealed record CreateNotificationRequest(
    [property: JsonPropertyName("cidadaoId")] int CitizenId,
    [property: JsonPropertyName("alertaOficialId")] int OfficialAlertId,
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("mensagem")] string Message);

public sealed record UpdateNotificationRequest(
    [property: JsonPropertyName("situacao")] string Status,
    [property: JsonPropertyName("enviadaEm")] DateTime? SentAt);

public sealed record OfficialAlertRequest(
    [property: JsonPropertyName("identificadorExterno")] long ExternalIdentifier,
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("descricao")] string? Description,
    [property: JsonPropertyName("tipoEvento")] string EventType,
    [property: JsonPropertyName("severidade")] string Severity,
    [property: JsonPropertyName("nivel")] int? Level,
    [property: JsonPropertyName("futuro")] bool? IsFuture,
    [property: JsonPropertyName("codigoIbgeMunicipio")] int? MunicipalityIbgeCode,
    [property: JsonPropertyName("codigosIbgeMunicipiosAfetados")] string AffectedMunicipalityCodes,
    [property: JsonPropertyName("inicioEm")] DateTime? StartsAt,
    [property: JsonPropertyName("expiraEm")] DateTime? ExpiresAt);

public sealed record OfficialAlertResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("identificadorExterno")] long ExternalIdentifier,
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("descricao")] string? Description,
    [property: JsonPropertyName("tipoEvento")] string EventType,
    [property: JsonPropertyName("severidade")] string Severity,
    [property: JsonPropertyName("nivel")] int? Level,
    [property: JsonPropertyName("futuro")] bool? IsFuture,
    [property: JsonPropertyName("codigoIbgeMunicipio")] int? MunicipalityIbgeCode,
    [property: JsonPropertyName("codigosIbgeMunicipiosAfetados")] string AffectedMunicipalityCodes,
    [property: JsonPropertyName("origem")] string Source,
    [property: JsonPropertyName("urlOrigem")] string SourceUrl,
    [property: JsonPropertyName("inicioEm")] DateTime? StartsAt,
    [property: JsonPropertyName("expiraEm")] DateTime? ExpiresAt,
    [property: JsonPropertyName("obtidoEm")] DateTime RetrievedAt,
    [property: JsonPropertyName("criadoEm")] DateTime CreatedAt,
    [property: JsonPropertyName("atualizadoEm")] DateTime? UpdatedAt);

public sealed record NotificationResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("cidadaoId")] int CitizenId,
    [property: JsonPropertyName("alertaOficialId")] int OfficialAlertId,
    [property: JsonPropertyName("titulo")] string Title,
    [property: JsonPropertyName("mensagem")] string Message,
    [property: JsonPropertyName("situacao")] string Status,
    [property: JsonPropertyName("criadaEm")] DateTime CreatedAt,
    [property: JsonPropertyName("enviadaEm")] DateTime? SentAt);

public sealed record MonitoringExecutionRequest(
    [property: JsonPropertyName("provedor")] string Provider,
    [property: JsonPropertyName("iniciadaEm")] DateTime StartedAt,
    [property: JsonPropertyName("finalizadaEm")] DateTime? FinishedAt,
    [property: JsonPropertyName("situacao")] string Status,
    [property: JsonPropertyName("registrosEncontrados")] int RecordsFound,
    [property: JsonPropertyName("registrosCriados")] int RecordsCreated,
    [property: JsonPropertyName("registrosAtualizados")] int RecordsUpdated,
    [property: JsonPropertyName("registrosDuplicados")] int RecordsDuplicated,
    [property: JsonPropertyName("mensagemErro")] string? ErrorMessage);

public sealed record MonitoringExecutionResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("provedor")] string Provider,
    [property: JsonPropertyName("iniciadaEm")] DateTime StartedAt,
    [property: JsonPropertyName("finalizadaEm")] DateTime? FinishedAt,
    [property: JsonPropertyName("situacao")] string Status,
    [property: JsonPropertyName("registrosEncontrados")] int RecordsFound,
    [property: JsonPropertyName("registrosCriados")] int RecordsCreated,
    [property: JsonPropertyName("registrosAtualizados")] int RecordsUpdated,
    [property: JsonPropertyName("registrosDuplicados")] int RecordsDuplicated,
    [property: JsonPropertyName("mensagemErro")] string? ErrorMessage);
