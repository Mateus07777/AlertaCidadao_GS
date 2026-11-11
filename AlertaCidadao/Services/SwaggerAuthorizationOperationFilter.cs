using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AlertaCidadao.Services;

public sealed class SwaggerAuthorizationOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.HttpMethod == "POST" && context.ApiDescription.RelativePath?.TrimEnd('/') == "api/ocorrencias")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Chave-Idempotencia",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Chave única por cidadão e operação. Repetir a chave com o mesmo conteúdo reproduz a resposta por 24 horas.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 100 }
            });
        }

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any())
            return;

        if (metadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }
    }
}
