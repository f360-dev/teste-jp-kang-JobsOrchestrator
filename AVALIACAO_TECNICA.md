# Avaliação Técnica - Orquestrador de Jobs Distribuído

**Candidato:** (Nome não informado)  
**Vaga:** Desenvolvedor Sênior C#/.NET  
**Avaliador:** Tech Lead  
**Data:** Fevereiro de 2026

---

## Sumário Executivo

O candidato entregou um projeto funcional que atende à maioria dos requisitos propostos. A solução demonstra conhecimento sólido de .NET 8, padrões de arquitetura e boas práticas de desenvolvimento. Entretanto, existem pontos de melhoria importantes que seriam esperados de um desenvolvedor sênior, especialmente em relação à separação de responsabilidades no domínio e documentação arquitetural.

**Nota Geral: 7.5/10**

---

## Análise Detalhada por Requisito

### 1. API de Ingestão (Gateway)

#### ✅ Segurança - Autenticação JWT
**Implementação:** Correta  
**Nota:** 8/10

O candidato implementou autenticação JWT de forma adequada:
- Configuração completa no `Program.cs` com validação de issuer, audience e signing key
- Endpoint de geração de token em `AuthController`
- Proteção dos endpoints com atributo `[Authorize]`

**Pontos de atenção:**
```csharp
// AuthController.cs - Credenciais hardcoded
if (request.ClientId != "test-client" || request.ClientSecret != "test-secret")
    return Unauthorized();
```
- Credenciais fixas no código (aceitável para demonstração, mas deveria haver comentário ou TODO indicando que em produção seria via banco de dados)
- Existe um `ApiKeyAuthenticationHandler.cs` criado mas não utilizado - código morto que deveria ser removido ou implementado

#### ✅ Idempotência
**Implementação:** Correta  
**Nota:** 9/10

Excelente implementação da idempotência:
- Header `Idempotency-Key` obrigatório
- Verificação no handler antes de criar o job
- Índice criado no MongoDB para busca eficiente

```csharp
// CreateJobCommandHandler.cs
var existing = await _repo.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
if (existing != null) return existing.Id;
```

#### ✅ Validação com FluentValidation
**Implementação:** Correta  
**Nota:** 8/10

- Validador bem estruturado com regras claras
- Auto-validação configurada no pipeline
- Testes unitários cobrindo cenários de validação

**Sugestão de melhoria:** Adicionar validação para o campo `ScheduledAt` (não permitir datas no passado).

---

### 2. Gestão de Tarefas

#### ✅ Prioridade
**Implementação:** Correta  
**Nota:** 8/10

- Enum `JobPriority` bem definido (Low=0, Medium=1, High=2)
- Ordenação por prioridade no `AcquireNextJobAsync`
- Índice composto criado: `Status + Priority (desc) + ScheduledAt`

#### ✅ Agendamento (ScheduledAt)
**Implementação:** Correta  
**Nota:** 8/10

Jobs agendados são filtrados corretamente:
```csharp
Builders<Job>.Filter.Or(
    Builders<Job>.Filter.Eq(j => j.ScheduledAt, null),
    Builders<Job>.Filter.Lte(j => j.ScheduledAt, now)
)
```

#### ⚠️ Cancelamento
**Implementação:** Parcial  
**Nota:** 6/10

O cancelamento está implementado, mas com problemas:

**Positivo:**
- Endpoint `POST /api/jobs/{id}/cancel` funcional
- Flag `CancelRequested` para jobs em processamento

**Problemas identificados:**
```csharp
// JobWorkerHostedService.cs - O CancellationToken não está sendo propagado corretamente
for (int i = 0; i < 10; i++)
{
    if (job.CancelRequested)  // Verifica flag no objeto em memória, não no banco
    {
        // ...
    }
    await Task.Delay(200, stoppingToken);
}
```
O worker verifica `job.CancelRequested` no objeto em memória, mas o cancelamento atualiza o banco. O worker deveria buscar o status atualizado do banco periodicamente durante o processamento.

---

### 3. Processamento (Workers)

#### ✅ Padrão Outbox
**Implementação:** Excelente  
**Nota:** 9/10

Implementação sólida do padrão Outbox com transações MongoDB:

```csharp
// JobRepository.cs
using var session = await _client.StartSessionAsync(cancellationToken: ct);
session.StartTransaction();
try
{
    await _jobs.InsertOneAsync(session, job, cancellationToken: ct);
    await _outbox.InsertOneAsync(session, outbox, cancellationToken: ct);
    await session.CommitTransactionAsync(ct);
    return job;
}
catch
{
    await session.AbortTransactionAsync(ct);
    throw;
}
```

O `OutboxProcessor` também implementa lock distribuído com token de processamento para evitar duplicação.

#### ✅ Concorrência Distribuída / Lock Distribuído
**Implementação:** Correta  
**Nota:** 8/10

- `FindOneAndUpdateAsync` garante atomicidade na aquisição de jobs
- `LockToken` e `LockedUntil` implementados
- Expiração de locks para recovery em caso de falha

#### ⚠️ Circuit Breaker
**Implementação:** Parcial  
**Nota:** 6/10

Circuit breaker implementado com Polly, porém:
- Configuração básica (5 falhas, 30 segundos)
- Aplicado apenas no worker, não nas chamadas externas específicas
- O processamento simulado dentro do circuit breaker é artificial

```csharp
// Deveria envolver chamadas a serviços externos específicos, não o loop inteiro
await _circuitBreaker.ExecuteAsync(async () =>
{
    for (int i = 0; i < 10; i++) { ... }  // Simulação artificial
});
```

#### ✅ Dead Letter Queue
**Implementação:** Correta  
**Nota:** 8/10

- Máximo de 3 tentativas configurado
- Jobs movidos para collection separada (`deadletters`)
- Status `DeadLetter` no enum
- Erro armazenado no campo `LastError`

---

### 4. Requisitos Não-Funcionais

#### ⚠️ Clean Architecture / DDD
**Implementação:** Parcial  
**Nota:** 6/10

**Estrutura de pastas adequada:**
```
├── Application/     (Handlers, Commands, Queries)
├── Domain/          (Models)
├── Infrastructure/  (Repositories, Configuration)
├── Presentation/    (Controllers)
```

**Problemas identificados:**

1. **Domain Anêmico:** O modelo `Job` é puramente anêmico - apenas propriedades, sem comportamentos encapsulados:
```csharp
// Domain/Models/Job.cs - Sem métodos de domínio
public class Job
{
    public string Id { get; set; } = null!;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    // ... apenas propriedades
}
```

Esperado para um sênior:
```csharp
public class Job
{
    // ... propriedades privadas
    
    public Result Cancel()
    {
        if (Status != JobStatus.Pending && Status != JobStatus.Processing)
            return Result.Fail("Job cannot be cancelled in current state");
        
        Status = JobStatus.Cancelled;
        CancelRequested = true;
        return Result.Ok();
    }
    
    public Result MarkAsProcessing(string lockToken)
    {
        if (Status != JobStatus.Pending)
            return Result.Fail("Job is not pending");
        // ...
    }
}
```

2. **Lógica de negócio no Repository:**
```csharp
// JobRepository.cs - Regra de negócio (max attempts) no repositório
const int MaxAttempts = 3;
if (job.Attempts >= MaxAttempts)
{
    await AddToDeadLetterAsync(...);
}
```
Esta lógica deveria estar em um Domain Service ou no próprio modelo.

3. **Falta de Value Objects:** Não há Value Objects no domínio (ex: `JobId`, `IdempotencyKey`).

#### ✅ SOLID
**Implementação:** Boa  
**Nota:** 7/10

- **S (SRP):** Controllers delegam para MediatR ✅
- **O (OCP):** Uso de interfaces permite extensão ✅
- **L (LSP):** Não há hierarquias para avaliar
- **I (ISP):** Interface `IJobRepository` poderia ser dividida (query/command) ⚠️
- **D (DIP):** Injeção de dependência bem utilizada ✅

#### ✅ IoC / Inversão de Controle
**Implementação:** Correta  
**Nota:** 8/10

- Uso correto do container de DI do .NET
- Interfaces abstraindo implementações
- Scoped services para repositórios
- IOptions pattern para configurações

#### ✅ CQRS
**Implementação:** Correta  
**Nota:** 8/10

Separação clara usando MediatR:
- `CreateJobCommand` / `CancelJobCommand` para escrita
- `GetJobByIdQuery` para leitura

---

### 5. Observabilidade

#### ✅ Logs Estruturados
**Implementação:** Boa  
**Nota:** 7/10

- Serilog configurado com console e arquivo
- Correlation ID propagado via middleware
- Template customizado com `{CorrelationId}`

**Sugestão:** Usar formato JSON (CompactJsonFormatter) para facilitar indexação em ferramentas como ELK.

#### ⚠️ Health Checks
**Implementação:** Parcial  
**Nota:** 5/10

**Problema crítico:** O health check do RabbitMQ está implementado (`RabbitMqHealthCheck.cs`) mas **não está registrado** no container de DI:

```csharp
// Program.cs - Apenas MongoDB registrado
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb");
    // Falta: .AddCheck<RabbitMqHealthCheck>("rabbitmq")
```

---

### 6. Testes

#### ⚠️ Testes Unitários
**Implementação:** Básica  
**Nota:** 5/10

Apenas dois arquivos de teste:
- `CreateJobCommandHandlerTests.cs` (4 testes)
- `CreateJobRequestValidatorTests.cs` (6 testes)

**Faltam testes para:**
- `CancelJobCommandHandler`
- `GetJobByIdQueryHandler`
- `OutboxProcessor`
- `JobWorkerHostedService`
- `JobRepository` (testes de integração)
- Cenários de concorrência

**Cobertura estimada:** ~20-30%

Para um desenvolvedor sênior, seria esperado:
- Maior cobertura do domínio
- Testes de integração
- Testes de comportamento de resiliência

---

### 7. Stack Tecnológica

#### ✅ .NET 8
**Nota:** 9/10

- Uso correto de recursos modernos
- Records para DTOs
- Nullable reference types habilitados

#### ✅ MongoDB
**Nota:** 8/10

- Transações utilizadas corretamente
- Índices criados para performance
- Replica Set configurado no Docker Compose

#### ✅ RabbitMQ com MassTransit
**Nota:** 8/10

- Abstração via MassTransit bem configurada
- Message retry configurado
- Publicação assíncrona via Outbox

#### ✅ Docker e Docker Compose
**Nota:** 8/10

- Multi-stage build no Dockerfile
- Health checks nos containers
- Networks e volumes configurados
- Dependências corretas entre serviços

---

### 8. Entregáveis

| Entregável | Status | Nota |
|------------|--------|------|
| Código Fonte | ✅ Entregue | 8/10 |
| ARCHITECTURE.md (ADR) | ❌ Não entregue | 0/10 |
| Diagramas C4 | ❌ Não entregue | 0/10 |
| README.md | ✅ Entregue | 7/10 |
| IaC (Terraform) | ❌ Não entregue | 0/10 |

**Impacto na avaliação:** A falta de documentação arquitetural é uma lacuna significativa para uma vaga sênior. Esperava-se ao menos um `ARCHITECTURE.md` explicando decisões técnicas.

---

## Resumo das Notas por Categoria

| Categoria | Peso | Nota | Nota Ponderada |
|-----------|------|------|----------------|
| API de Ingestão | 15% | 8.3/10 | 1.25 |
| Gestão de Tarefas | 15% | 7.3/10 | 1.10 |
| Processamento (Workers) | 20% | 7.8/10 | 1.56 |
| Arquitetura (Clean/DDD/SOLID) | 20% | 7.0/10 | 1.40 |
| Observabilidade | 10% | 6.0/10 | 0.60 |
| Testes | 10% | 5.0/10 | 0.50 |
| Documentação | 10% | 3.0/10 | 0.30 |
| **TOTAL** | **100%** | | **6.71/10** |

---

## Pontos Fortes

1. **Padrão Outbox bem implementado** - Demonstra compreensão de consistência eventual e mensageria confiável
2. **Lock distribuído funcional** - Solução pragmática usando MongoDB
3. **Estrutura de código organizada** - Fácil de navegar e entender
4. **Idempotência correta** - Importante para sistemas distribuídos
5. **Docker Compose completo** - Facilita setup e demonstração
6. **Uso de MediatR** - Desacoplamento adequado entre camadas
7. **FluentValidation** - Validações expressivas e testáveis

---

## Pontos de Melhoria

### Críticos (devem ser corrigidos)
1. **Health Check do RabbitMQ não registrado** - Bug que afeta monitoramento em produção
2. **Cancelamento não funciona corretamente** - Worker não verifica banco durante processamento
3. **Falta documentação arquitetural** - Requisito explícito não atendido

### Importantes (esperados de um sênior)
4. **Domínio anêmico** - Falta encapsulamento de regras de negócio no modelo
5. **Lógica de negócio no Repository** - Viola responsabilidades da camada
6. **Cobertura de testes insuficiente** - Apenas cenários básicos cobertos
7. **Código morto** - `ApiKeyAuthenticationHandler` não utilizado

### Desejáveis (diferenciais)
8. Logs em formato JSON para indexação
9. Métricas expostas (Prometheus)
10. Testes de integração
11. Terraform para IaC

---

## Recomendação Final

### Parecer: **APROVADO COM RESSALVAS**

O candidato demonstra conhecimento técnico sólido em .NET e padrões de arquitetura distribuída. A implementação do Outbox pattern e lock distribuído mostra maturidade técnica. Entretanto, a falta de documentação arquitetural e os problemas no domínio (modelo anêmico) são lacunas importantes para o nível sênior.

**Sugestão:** Avançar para entrevista técnica focando em:
1. Por que optou por modelo anêmico e como evoluiria para Rich Domain
2. Como implementaria testes de integração
3. Debugging do problema de cancelamento identificado
4. Discussão sobre observabilidade em produção

---

*Este documento foi gerado como parte da avaliação técnica do processo seletivo.*
