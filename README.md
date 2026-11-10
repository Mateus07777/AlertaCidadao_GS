# AlertaCidadão

API para registro de ocorrências urbanas e consulta de alertas meteorológicos externos, com notificações preventivas persistidas. Alertas de terceiros permanecem distintos de ocorrências confirmadas por cidadãos.

## Requisitos e execução

- .NET SDK 10.0; MySQL 8.4 (ou Docker Desktop com Compose).
- Na raiz: copie `.env.example` para `.env`, substitua os valores de exemplo por credenciais locais fortes e execute `docker compose up --build`. O Compose falha se as variáveis obrigatórias não estiverem definidas.
- API: `http://localhost:8080`; Swagger: `http://localhost:8080/swagger`; liveness: `/health/live`; readiness/MySQL: `/health/ready`.
- O volume MySQL recebe `docs/schema.sql` na primeira inicialização. Para criar a estrutura manualmente, conecte-se ao MySQL e execute esse arquivo. O script cria `alerta_cidadao` e todas as tabelas.
- Para rodar a API localmente, inicie MySQL e execute `dotnet run --project AlertaCidadao`. Configure `ConnectionStrings__Default` e `Jwt__SigningKey` no ambiente; o .NET não carrega automaticamente o arquivo `.env` fora do Docker Compose.

Variáveis principais: `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`, `MYSQL_ROOT_PASSWORD`, `JWT_SIGNING_KEY`, `JWT_EXPIRY_MINUTES`, `MONITORING_INTERVAL_MINUTES`, `RATE_LIMIT_PERMIT_LIMIT` e `RATE_LIMIT_WINDOW_MINUTES`. Não use credenciais reais versionadas.

## API

Grupos: `/api/autenticacao`, `/api/cidadaos`, `/api/ocorrencias`, `/api/alertas-oficiais`, `/api/notificacoes` e `/api/execucoes-monitoramento`. Use `pagina` e `tamanhoPagina` nas listas. Cadastro e acesso são públicos; as demais rotas requerem token JWT Bearer, exceto consulta pública de alertas meteorológicos. Os contratos JSON utilizam nomes de campos em português.

Exemplo de cadastro:

```http
POST /api/autenticacao/cadastro
Content-Type: application/json

{"nome":"Ana Silva","email":"ana@example.com","senha":"senha-segura-123","municipio":"Campinas","codigoIbgeMunicipio":3509502,"bairro":"Centro","cep":"13010-000"}
```

Para criar ocorrência, use `Authorization: Bearer <token>` e uma chave única no cabeçalho `Chave-Idempotencia`. Repetir a chave e o mesmo conteúdo retorna a resposta original; reaproveitar a chave com conteúdo diferente retorna conflito. O limite padrão é 60 chamadas por minuto e o excesso retorna HTTP 429.

Para testar rotas administrativas, cadastre um cidadão e promova-o manualmente no banco: `UPDATE Citizens SET Role='Admin' WHERE Email='ana@example.com';` Depois faça login novamente para obter JWT com a role atualizada.

## Integração e atribuição

O único provider é RadarMeteorológico: `GET https://radarmeteorologico.com.br/api/v1/alertas?uf=SP`. Atualiza a cada 10 minutos por padrão. A origem é identificada como **INMET via RadarMeteorológico**, com [atribuição e documentação do serviço](https://radarmeteorologico.com.br/api-publica). O vínculo preventivo usa somente códigos IBGE efetivamente recebidos; não há inferência de bairros ou confirmação de ocorrência.

O RadarMeteorológico reproduz alertas do INMET, mas é um serviço intermediário sem garantia de disponibilidade. A API não depende dele para iniciar nem o trata como fonte única para segurança crítica.

## Testes

```sh
dotnet test
```

Os testes atuais usam respostas HTTP controladas para o provider, sem chamar a API pública, e verificam validação, hashing e configuração relacional. Para acrescentar testes de integração com o provider MySQL, disponibilize uma instância de teste separada antes de executar esses cenários.

## Dependências principais

- ASP.NET Core Minimal APIs / .NET 10.
- EF Core 10.0.14 e `MySql.EntityFrameworkCore` 10.0.12 (provider Oracle/MySQL compatível com EF Core 10). O Pomelo estável consultado suporta EF Core 9; não misturar essas versões.
- JWT Bearer, Swagger/OpenAPI, Rate Limiting e Health Checks.

O pacote `MySql.EntityFrameworkCore` declara licença GPL-2.0-only com Universal-FOSS exception; considere essa licença ao redistribuir o projeto.

Consulte [o plano técnico](docs/PLANO-TECNICO.md) e o [DDL MySQL](docs/schema.sql).
