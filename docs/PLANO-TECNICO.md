# AlertaCidadão — plano técnico de implementação

## Diagnóstico e arquitetura

O projeto utiliza Minimal APIs em ASP.NET Core `net10.0`, EF Core 10 e MySQL 8.4. Para manter compatibilidade entre o provider e a versão do EF Core, foi escolhido `MySql.EntityFrameworkCore` 10.0.12. O projeto tem SDK .NET 10.0.403 instalado.

O sistema é organizado em endpoints por grupo, entidades/DbContext, contratos DTO, serviços, provider externo e BackgroundService. MySQL é obrigatório e a configuração de conexão usa `ConnectionStrings__Default`; no Compose, o host é `mysql`. O DDL executável está em `schema.sql`.

## Modelo de dados

- `Citizen`: identidade, hash de senha, perfil, município e código IBGE opcional.
- `OfficialAlert`: id externo único, evento, severidade, nível, indicador `futuro`, origem, URL e datas recebidas. `AffectedMunicipalityCodes` guarda os códigos IBGE efetivamente informados pelo provider, sem inventar nomes/bairros.
- `CitizenOccurrence`: ocorrência real vinculada ao cidadão, com status e localização facultativa.
- `CitizenNotification`: referência a cidadão e alerta, única por par, persistida sem envio externo.
- `MonitoringExecution`: resultado auditável de cada sincronização.
- `IdempotencyRecord`: chave única, hash do pedido, resposta original e expiração.

Relacionamentos usam FKs e exclusão restrita para preservar dados relacionados. Há índices de e-mail, IDs externos, região, status e expiração de idempotência. Datas operacionais são UTC; datas externas sem fuso são preservadas como `DATETIME` sem inferir conversão de zona.

## Endpoints e segurança

- `/api/autenticacao/cadastro`, `/api/autenticacao/entrar`.
- `/api/cidadaos`: CRUD e `/eu`; cidadão acessa apenas o próprio cadastro, administração exige perfil `Administrador`.
- `/api/ocorrencias`: CRUD; cidadão vê e altera apenas as próprias ocorrências; criação exige `Chave-Idempotencia`.
- `/api/alertas-oficiais`: consulta pública, CRUD administrativo e `/sincronizar` protegido.
- `/api/notificacoes`: acesso do proprietário, criação e administração com deduplicação por cidadão/alerta.
- `/api/execucoes-monitoramento`: CRUD administrativo para demonstração e consulta do histórico.
- `/api/saude/viva` e `/api/saude/prontidao`; Swagger/OpenAPI disponível no ambiente Development.

JWT usa segredo injetado por variável de ambiente, validade configurável e perfis `Cidadão`/`Administrador`. Senhas são armazenadas por `PasswordHasher<T>`. O rate limiter usa janela fixa configurável e devolve HTTP 429. Erros inesperados retornam Problem Details sem detalhes internos.

## RadarMeteorológico e monitoramento

O provider chama exclusivamente `GET https://radarmeteorologico.com.br/api/v1/alertas?uf=SP`, com `IHttpClientFactory` e timeout de 15 segundos. O mapeamento limita-se aos campos observados/documentados: `id`, `evento`, `severidade`, `nivel`, `inicio`, `fim`, `futuro` e `geocodes`. O título/evento e severidade vêm da resposta; a fonte é armazenada como `INMET via RadarMeteorológico` com URL de atribuição. Alertas externos não são ocorrências confirmadas.

O serviço em segundo plano roda a cada 10 minutos por padrão, registra execução, faz upsert pelo identificador externo e persiste no máximo uma notificação por cidadão/alerta quando o código IBGE do cadastro aparece nos códigos afetados. Falha externa é registrada sem derrubar a API. A API do provedor não oferece garantia de disponibilidade e não é fonte única para segurança crítica.

## Testes e critérios FIAP

O projeto de testes xUnit cobre validação de cadastro, hash de idempotência, verificação de hash de senha, metadados relacionais do EF e provider com HTTP falso (mapeamento, campos opcionais, erro HTTP, JSON inválido e cancelamento/timeout). Cobertura de ponta a ponta de JWT/autorização, rate limiting, health checks e persistência com servidor MySQL ainda precisa de integração com banco disponível. Os testes existentes não dependem do endpoint público.

| Critério | Evidência de entrega |
|---|---|
| Minimal API, REST, validação, HTTP e Swagger — 20 | Grupos de rotas, DTOs, filtros, paginação e Swagger |
| JWT e autorização — 20 | Login, token, roles e isolamento por proprietário |
| Idempotência — 15 | Registro persistido, replay e conflito de conteúdo |
| Rate Limiting — 15 | Policy configurável e resposta 429 |
| xUnit — 20 | Suítes unitária e de integração |
| Health Checks — 10 | Liveness e readiness com MySQL |

## Etapas e operação local

1. Restaurar pacotes e confirmar compatibilidade efetiva provider/EF.
2. Criar modelo, DDL, contexto e configurar conexão.
3. Implementar autenticação, autorização, DTOs e CRUD.
4. Completar idempotência e rate limiting.
5. Implementar provider, persistência de alertas, notificações e monitoramento.
6. Adicionar Swagger, health checks e testes automatizados.
7. Validar Docker Compose e completar README e exemplos.

Com Docker: `docker compose up --build`. O script `schema.sql` é carregado na primeira inicialização do volume MySQL. Para reexecutar a inicialização do zero, remover o volume local de desenvolvimento com `docker compose down -v` (isso apaga os dados locais). Para promover um cidadão a Admin, use SQL administrativo: `UPDATE Citizens SET Role='Admin' WHERE Email='email cadastrado';`.

Fora do escopo: outros provedores externos, envio real de notificações, Azure e implantação em nuvem. O TXT de identificação FIAP deve ser preenchido conforme o modelo oficial da disciplina, ainda não presente no repositório.

