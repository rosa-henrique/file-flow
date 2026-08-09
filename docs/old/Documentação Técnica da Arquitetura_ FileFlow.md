# Documentação Técnica da Arquitetura: FileFlow

**Autor:** Manus AI (Tech Lead)
**Data:** 16 de Maio de 2026 (Revisado)

Este documento detalha a arquitetura técnica do projeto **FileFlow - Sistema de Gestão de Uploads Assíncronos**, fornecendo diretrizes para a modelagem de dados, escolha de tecnologias para registro de operações, contratos de mensageria e estrutura de projetos. Esta revisão incorpora a abordagem de pré-registro em uma única tabela `MediaAsset`, que funcionará como uma máquina de estados para o ciclo de vida do arquivo, além de introduzir novos campos de negócio e refinar a lógica de reprocessamento e gestão de lotes.

## 1. Modelagem de Dados

Para o projeto FileFlow, utilizaremos o **PostgreSQL** como banco de dados principal, rodando em um contêiner Docker para facilitar o ambiente de desenvolvimento local. A modelagem de dados será consolidada para simplificar o rastreamento do estado do arquivo e agregar valor de negócio.

### 1.1. Banco de Dados Relacional (PostgreSQL)

#### Tabela: `UploadBatch`
Representa um lote de upload, agrupando múltiplos arquivos enviados em uma única operação pelo usuário. Seu status reflete o status agregado dos `MediaAssets` associados.

| Campo | Tipo de Dados | Restrições | Descrição |
| :--- | :--- | :--- | :--- |
| `Id` | `UUID` | `PK`, `NOT NULL` | Identificador único do lote de upload. |
| `UserId` | `UUID` | `NOT NULL` | Identificador do usuário que realizou o upload. |
| `Name` | `VARCHAR(255)` | `NOT NULL` | Nome amigável do lote (ex: "Campanha de Natal 2026"). |
| `Status` | `VARCHAR(50)` | `NOT NULL` | Status geral do lote (e.g., `PENDING`, `PROCESSING`, `COMPLETED`, `PARTIAL`, `FAILED`). `PARTIAL` indica que alguns arquivos falharam. |
| `CreatedAt` | `TIMESTAMP` | `NOT NULL` | Data e hora da criação do lote. |
| `CompletedAt` | `TIMESTAMP` | `NULLABLE` | Data e hora da conclusão do processamento do lote. |

#### Tabela: `MediaAsset`
Representa um arquivo de mídia que está sendo processado ou que já foi finalizado. Esta tabela atua como uma **máquina de estados**, registrando o pré-registro, o progresso, o resultado final (link) ou o erro. O frontend consultará esta tabela para exibir o status, o link final e as informações de negócio.

| Campo | Tipo de Dados | Restrições | Descrição |
| :--- | :--- | :--- | :--- |
| `Id` | `UUID` | `PK`, `NOT NULL` | Identificador único do ativo de mídia. |
| `UploadBatchId` | `UUID` | `FK`, `NOT NULL` | Chave estrangeira para a tabela `UploadBatch`. |
| `UserId` | `UUID` | `NOT NULL` | Identificador do usuário proprietário do ativo. |
| `OriginalFileName` | `VARCHAR(255)` | `NOT NULL` | Nome original do arquivo. |
| `Title` | `VARCHAR(255)` | `NULLABLE` | Título amigável do arquivo, definido pelo usuário. |
| `MimeType` | `VARCHAR(100)` | `NOT NULL` | Tipo MIME do arquivo. |
| `Size` | `BIGINT` | `NOT NULL` | Tamanho do arquivo em bytes. |
| `FinalRustFSPath` | `VARCHAR(500)` | `NULLABLE` | Caminho completo do arquivo no bucket definitivo do RustFS após a migração. |
| `Status` | `VARCHAR(50)` | `NOT NULL` | Status atual do arquivo (e.g., `PENDING`, `UPLOADING`, `UPLOADED`, `MIGRATING`, `MIGRATED`, `FAILED`, `DELETION_PENDING`, `DELETED`). |
| `RetryCount` | `INT` | `NOT NULL`, `DEFAULT 0` | Número de tentativas de reprocessamento do arquivo. |
| `CreatedAt` | `TIMESTAMP` | `NOT NULL` | Data e hora da criação do registro (pré-registro). |
| `LastAttemptAt` | `TIMESTAMP` | `NULLABLE` | Data e hora da última tentativa de processamento/migração. |
| `CompletedAt` | `TIMESTAMP` | `NULLABLE` | Data e hora da conclusão do processamento (sucesso ou falha). |
| `ErrorMessage` | `TEXT` | `NULLABLE` | Mensagem de erro da última falha, se houver. |
| `Tags` | `JSONB` | `NULLABLE` | Tags associadas ao arquivo (ex: `["marketing", "campanha"]`). |
| `Metadata` | `JSONB` | `NULLABLE` | Metadados técnicos extraídos automaticamente (ex: `{ "resolution": "1920x1080", "duration": "120s" }`). |

#### Tabela: `ProcessingEvent`
Registra eventos detalhados do ciclo de vida de processamento de cada arquivo, fornecendo um histórico imutável para auditoria, depuração e análise.

| Campo | Tipo de Dados | Restrições | Descrição |
| :--- | :--- | :--- | :--- |
| `Id` | `BIGSERIAL` | `PK`, `NOT NULL` | Identificador único do log. |
| `MediaAssetId` | `UUID` | `FK`, `NOT NULL` | Chave estrangeira para a tabela `MediaAsset`. |
| `Timestamp` | `TIMESTAMP` | `NOT NULL` | Data e hora do evento. |
| `EventType` | `VARCHAR(100)` | `NOT NULL` | Tipo do evento (e.g., `PRE_REGISTERED`, `UPLOAD_CONFIRMED`, `MIGRATION_STARTED`, `MIGRATION_COMPLETED`, `MIGRATION_FAILED`, `RETRY_INITIATED`, `DELETION_REQUESTED`, `DELETED`). |
| `Message` | `TEXT` | `NOT NULL` | Mensagem descritiva do evento. |
| `Details` | `JSONB` | `NULLABLE` | Detalhes adicionais do evento em formato JSON (e.g., `ErrorMessage`, `AttemptNumber`). |

## 2. Análise e Recomendação para Banco de Logs/Status Temporário

**Recomendação:** Para o projeto **FileFlow**, o **PostgreSQL** é a escolha mais equilibrada e recomendada para todas as camadas de dados (Domínio, Orquestração e Observabilidade).

**Justificativa:**
*   **Simplicidade de Infraestrutura:** Utilizar um único banco de dados (PostgreSQL em Docker) para todas as necessidades de persistência reduz a complexidade de setup, gerenciamento e consumo de recursos em ambiente local. Não há necessidade de introduzir MongoDB ou Elasticsearch, que adicionariam sobrecarga desnecessária para um projeto de portfólio.
*   **Flexibilidade com JSONB:** O PostgreSQL, com seu tipo de dado `JSONB`, oferece a flexibilidade de armazenar dados semi-estruturados (como os `Details` dos eventos de processamento, `Tags` e `Metadata` dos `MediaAssets`) dentro de um modelo relacional, combinando o melhor dos dois mundos.
*   **Integridade Referencial:** A capacidade de definir chaves estrangeiras (`FKs`) entre as tabelas (`MediaAsset` referenciando `UploadBatch`, `ProcessingEvent` referenciando `MediaAsset`) garante a integridade dos dados e facilita a rastreabilidade e depuração.
*   **Consistência e Transacionalidade:** O PostgreSQL oferece garantias ACID, o que é crucial para manter a consistência do estado dos ativos de mídia. Além disso, o **CAP (DotNetCore.CAP)** utilizará o mesmo banco de dados PostgreSQL para suas tabelas internas (`cap.published`, `cap.received`), garantindo a atomicidade do Outbox Pattern e a consistência eventual dos eventos. Esta abordagem de **banco de dados unificado** para dados de aplicação e tabelas do CAP simplifica a infraestrutura local, mantendo a robustez necessária para o projeto.

## 3. Contratos de Mensageria (RabbitMQ)

Os contratos de mensageria definem a estrutura das mensagens que serão publicadas e consumidas via RabbitMQ. É crucial que esses contratos sejam bem definidos e versionados para garantir a compatibilidade entre os microsserviços. Utilizaremos classes C# para representar esses contratos no projeto `FileFlow.Shared`.

### 3.1. `FilePreRegisteredEvent`
Publicado pela **Web API** após o pré-registro de um arquivo na tabela `MediaAsset`, antes mesmo do upload para o RustFS temporário. Este evento pode ser usado para inicializar o estado no frontend.

```csharp
public class FilePreRegisteredEvent
{
    public Guid MediaAssetId { get; set; }
    public Guid UploadBatchId { get; set; }
    public Guid UserId { get; set; }
    public string OriginalFileName { get; set; }
    public string MimeType { get; set; }
    public long Size { get; set; }
    public string TempRustFSPath { get; set; } // Caminho temporário já definido pela API
    public string Title { get; set; }
    public List<string> Tags { get; set; } // Ou outro tipo para JSONB
}
```

### 3.2. `FileUploadedEvent`
Publicado pela **Web API** após a confirmação do upload de um arquivo para o bucket temporário do RustFS. Este evento inicia a tarefa de migração.

```csharp
public class FileUploadedEvent
{
    public Guid MediaAssetId { get; set; }
    public Guid UploadBatchId { get; set; }
    public Guid UserId { get; set; }
    public string OriginalFileName { get; set; }
    public string MimeType { get; set; }
    public long Size { get; set; }
    public string TempRustFSPath { get; set; }
    public int RetryCount { get; set; }
    public string Title { get; set; }
    public List<string> Tags { get; set; }
}
```

### 3.3. `FileMigrationCompletedEvent`
Publicado pelo **Microsserviço de Migração de Arquivos** após a migração bem-sucedida de um arquivo para o bucket definitivo. Este evento aciona a atualização do `MediaAsset` com o link final e metadados.

```csharp
public class FileMigrationCompletedEvent
{
    public Guid MediaAssetId { get; set; }
    public string FinalRustFSPath { get; set; }
    public DateTime CompletedAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } // Metadados extraídos
}
```

### 3.4. `FileMigrationFailedEvent`
Publicado pelo **Microsserviço de Migração de Arquivos** em caso de falha na migração de um arquivo. Este evento pode ser roteado para uma **Dead Letter Queue (DLQ)** para reprocessamento ou análise manual.

```csharp
public class FileMigrationFailedEvent
{
    public Guid MediaAssetId { get; set; }
    public string ErrorMessage { get; set; }
    public DateTime FailedAt { get; set; }
    public int RetryCount { get; set; }
}
```

### 3.5. `FileDeletionRequestedEvent`
Publicado pela **Web API** quando o usuário solicita a exclusão de um arquivo (`MediaAsset`).

```csharp
public class FileDeletionRequestedEvent
{
    public Guid MediaAssetId { get; set; }
    public Guid UserId { get; set; }
    public string FinalRustFSPath { get; set; } // Caminho para o arquivo no bucket definitivo (se existir)
    public string TempRustFSPath { get; set; } // Caminho para o arquivo no bucket temporário (se existir)
}
```

### 3.6. `FileDeletedEvent`
Publicado pelo **Microsserviço de Deleção de Arquivos** após a remoção bem-sucedida de um arquivo do bucket definitivo e/ou da tarefa de upload.

```csharp
public class FileDeletedEvent
{
    public Guid MediaAssetId { get; set; }
    public DateTime DeletedAt { get; set; }
}
```

### 3.7. `FileCleanedEvent`
Publicado pelo **Microsserviço de Limpeza de Arquivos Temporários** após a remoção de um arquivo do bucket temporário.

```csharp
public class FileCleanedEvent
{
    public Guid MediaAssetId { get; set; }
    public string TempRustFSPath { get; set; }
    public DateTime CleanedAt { get; set; }
}
```

## 4. Estrutura de Pastas/Projetos

Para um projeto de portfólio, uma estrutura de monorepo simplifica o gerenciamento e a execução local. A sugestão é organizar os projetos .NET e o projeto Angular sob uma pasta raiz `FileFlow/`.

```
FileFlow/
├── src/
│   ├── FileFlow.AppHost/                 # Projeto Aspire AppHost (orquestra todos os serviços)
│   │   └── Program.cs
│   ├── FileFlow.ServiceDefaults/         # Configurações padrão do Aspire (telemetria, health checks)
│   ├── FileFlow.Api/                     # ASP.NET Core Minimal API (Gateway, Upload Prepare/Confirm, SignalR Hub)
│   │   └── appsettings.json
│   ├── FileFlow.Application/             # Camada de Aplicação (CQRS: Commands, Queries, Handlers com MediatR)
│   │   ├── Commands/
│   │   ├── Queries/
│   │   └── Handlers/
│   ├── FileFlow.Data/                    # Camada de Persistência (Entidades, DbContext, Migrations)
│   │   ├── Entities/
│   │   ├── Context/
│   │   └── Migrations/
│   ├── FileFlow.Workers/                 # Projeto único para todos os .NET Worker Services (Migração, Logging, Limpeza, Deleção)
│   │   ├── Services/                     # Implementações dos BackgroundService
│   │   ├── Consumers/                    # Consumidores RabbitMQ
│   │   └── appsettings.json
│   ├── FileFlow.Shared/                  # Biblioteca de classes compartilhadas (Contratos, DTOs, Eventos)
│   │   ├── Contracts/
│   │   └── Events/
│   └── FileFlow.Frontend/                # Projeto Angular
│       ├── src/
│       ├── angular.json
│       └── package.json
├── README.md                             # Descrição geral do projeto e instruções de setup
└── docs/                                 # Documentação adicional (como este arquivo)
    └── arquitetura_tecnica_fileflow.md
```

### 4.1. Orquestração com .NET Aspire

O **.NET Aspire** será utilizado para orquestrar e gerenciar os serviços do FileFlow em ambiente de desenvolvimento local. Ele simplifica a configuração de dependências como RustFS, RabbitMQ e PostgreSQL, além de fornecer telemetria e *service discovery* de forma integrada.

O projeto `FileFlow.AppHost` será o ponto central para definir e executar todos os serviços da aplicação. Um exemplo de `Program.cs` no `FileFlow.AppHost` incluiria:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Configuração do PostgreSQL
var postgres = builder.AddPostgres("postgres").WithVolume("pg_data");
builder.AddPostgresContainer("fileflow_db", postgres)
	       .AddDatabase("fileflow_db");

// Configuração do RustFS (exemplo de um serviço de container genérico)
// Assumindo que RustFS pode ser executado como um container Docker
var rustfs = builder.AddContainer("rustfs", "rustfs/rustfs")
	       .WithEndpoint(targetPort: 9000, name: "rustfs-api", is      Http: true) // Porta da API do RustFS
	       .WithVolume("rustfs_data", "/data"); // Volume para persistir os dados

// Configuração do RabbitMQ
var rabbitmq = builder.AddRabbitMQ("rabbitmq");

// Configuração do RustFS (exemplo, pode exigir um componente Aspire customizado ou configuração manual)
// builder.AddContainer("rustfs", "rustfs/rustfs")
//        .WithEndpoint(targetPort: 9000, name: "rustfs-api")
//        .WithEndpoint(targetPort: 9001, name: "rustfs-console")
//        .WithEnvironment("MINIO_ROOT_USER", "rustfsadmin")
//        .WithEnvironment("MINIO_ROOT_PASSWORD", "rustfsadmin")
//        .WithVolume("rustfs_data", "/data");

// Adição dos projetos de microsserviços
var apiService = builder.AddProject<Projects.FileFlow_Api>("fileflow-api")
		                        .WithReference(postgres)
		                        .WithReference(rabbitmq)
		                        .WithReference(rustfs); // API precisa do RustFS para gerar links pré-assinados
		
		builder.AddProject<Projects.FileFlow_Workers>("fileflow-workers")
		       .WithReference(postgres)
		       .WithReference(rabbitmq)
		       .WithReference(rustfs); // Workers precisam do RustFS para migrar e limpar arquivos

// O frontend Angular será executado separadamente ou integrado via proxy reverso no Aspire
// builder.AddNpmApp("frontend", "../FileFlow.Frontend", "start");

builder.Build().Run();
```

O Aspire se encarregará de iniciar e conectar todos esses serviços, simplificando o ambiente de desenvolvimento e fornecendo uma experiência de depuração unificada.

### 4.2. Fluxo de Erro e Reprocessamento (Com Idempotência)

1.  **Detecção de Falha:** Durante a migração, se o `FileFlow.MigrationWorker` encontrar um erro, ele publica um `FileMigrationFailedEvent` no RabbitMQ.
2.  **DLQ (Dead Letter Queue):** O RabbitMQ é configurado para rotear mensagens `FileMigrationFailedEvent` para uma fila de *Dead Letter Queue* (DLQ) após um número configurável de tentativas de reprocessamento automáticas (configurado no *consumer* do worker).
3.  **Atualização de Status e Log:** O `FileFlow.LoggingWorker` consome o `FileMigrationFailedEvent`. Ele atualiza o `Status` do `MediaAsset` para `FAILED`, incrementa o `RetryCount` e registra a `ErrorMessage`. Uma nova entrada é adicionada à `ProcessingEvent` detalhando a falha.
4.  **Interface do Usuário:** O frontend Angular, via SignalR, é notificado da falha. O usuário vê o `MediaAsset` com status `FAILED` e um botão **"Reprocessar"**.
5.  **Reprocessamento Manual (Idempotente):** Ao clicar em "Reprocessar", o frontend envia uma requisição para a `FileFlow.Api`. A API atualiza o `Status` do `MediaAsset` para `PENDING` (ou `UPLOADED` se o arquivo temporário ainda estiver lá), incrementa o `RetryCount` e publica um novo `FileUploadedEvent` (ou um evento específico de reprocessamento) no RabbitMQ, reiniciando o fluxo de migração para aquele arquivo. Um novo `ProcessingEvent` é registrado para `RETRY_INITIATED`.
    *   **Lógica Idempotente no Worker:** O `FileFlow.MigrationWorker` (e outros workers) antes de processar, verifica o `Status` atual do `MediaAsset` no banco de dados. Se o status já for `MIGRATED` (ou `COMPLETED`), o worker simplesmente ignora a mensagem ou a marca como processada com sucesso, evitando trabalho duplicado e garantindo que apenas arquivos pendentes ou falhos sejam reprocessados.

### 4.3. Fluxo de Deleção de Arquivos

1.  **Solicitação de Deleção:** O usuário seleciona um `MediaAsset` no frontend Angular e clica em "Deletar".
2.  **API Gateway:** O Angular envia uma requisição para a `FileFlow.Api` (ex: `DELETE /api/mediaassets/{id}`).
3.  **Marcação para Deleção e Evento:** A `FileFlow.Api` atualiza o `Status` do `MediaAsset` para `DELETION_PENDING` e publica um `FileDeletionRequestedEvent` no RabbitMQ. Um `ProcessingEvent` é registrado para `DELETION_REQUESTED`.
4.  **Microsserviço de Deleção:** O `FileFlow.DeletionWorker` consome o `FileDeletionRequestedEvent`.
5.  **Remoção do Object Storage:** O `FileFlow.DeletionWorker` remove o arquivo do **RustFS definitivo** (se `FinalRustFSPath` existir) e do **RustFS temporário** (se `TempRustFSPath` existir).
6.  **Confirmação de Deleção:** O `FileFlow.DeletionWorker` atualiza o `Status` do `MediaAsset` para `DELETED` (ou remove o registro da tabela, dependendo da política de retenção) e publica um `FileDeletedEvent` no RabbitMQ.
7.  **Atualização de Logs e UI:** O `FileFlow.LoggingWorker` e o SignalR atualizam o status e a interface do usuário, respectivamente. Um `ProcessingEvent` é registrado para `DELETED`.

### 4.4. Gestão de Lotes com Erro (`PARTIAL` Status)

*   **Status `PARTIAL`:** O `UploadBatch` terá o status `PARTIAL` se um ou mais `MediaAssets` dentro do lote falharem, mas outros tiverem sido migrados com sucesso. Isso permite que o usuário veja que o lote não está totalmente concluído, mas também não falhou por completo.
*   **Ações do Usuário para Lotes `PARTIAL`:**
    *   **"Reprocessar Lote"**: A API reenfileira `FileUploadedEvent` para todos os `MediaAssets` do lote que não estão com status `MIGRATED`. A lógica idempotente nos workers garante que apenas os arquivos falhos ou pendentes sejam reprocessados.
    *   **"Limpar Erros"**: A API envia `FileDeletionRequestedEvent` para todos os `MediaAssets` do lote que estão com status `FAILED`. Após a deleção, o status do `UploadBatch` pode ser atualizado para `COMPLETED` (se não houver mais falhas) ou permanecer `PARTIAL` (se ainda houver arquivos pendentes).

Esta documentação técnica fornece uma base sólida para iniciar o desenvolvimento do **FileFlow**, garantindo que as decisões arquiteturais estejam claras e alinhadas com os objetivos do projeto e as restrições de ambiente local.

## 5. Matriz de Transição de Estados e Responsabilidades

A tabela `MediaAsset` atua como uma máquina de estados. A tabela abaixo detalha cada transição, o gatilho e o serviço responsável por realizar a alteração no banco de dados.

| Status Atual | Ação / Gatilho | Novo Status | Serviço Responsável pela Alteração | Observações |
| :--- | :--- | :--- | :--- | :--- |
| `(Nenhum)` | Usuário inicia o upload no Frontend | `PENDING` | **Web API** | Cria o registro inicial (pré-registro) com os links pré-assinados gerados. |
| `PENDING` | Frontend confirma o upload para o RustFS temporário | `UPLOADED` | **Web API** | A API recebe a confirmação, atualiza o status e publica o `FileUploadedEvent`. |
| `UPLOADED` | `MigrationWorker` inicia o processamento da mensagem | `MIGRATING` | **LoggingWorker** | O `MigrationWorker` publica um evento de início, e o `LoggingWorker` atualiza o status para refletir que o trabalho começou. |
| `MIGRATING` | `MigrationWorker` conclui a cópia para o bucket definitivo | `MIGRATED` | **LoggingWorker** | O `MigrationWorker` publica `FileMigrationCompletedEvent`. O `LoggingWorker` atualiza o status e o `FinalRustFSPath`. |
| `MIGRATING` | `MigrationWorker` encontra um erro (ex: falha de rede) | `FAILED` | **LoggingWorker** | O `MigrationWorker` publica `FileMigrationFailedEvent`. O `LoggingWorker` atualiza o status e o `ErrorMessage`. |
| `FAILED` | Usuário clica em "Reprocessar" no Frontend | `PENDING` | **Web API** | A API reseta o status, incrementa o `RetryCount` e republica o `FileUploadedEvent`. |
| `(Qualquer)` | Usuário solicita a deleção do arquivo | `DELETION_PENDING` | **Web API** | A API marca para deleção e publica `FileDeletionRequestedEvent`. |
| `DELETION_PENDING` | `DeletionWorker` conclui a remoção física | `DELETED` | **LoggingWorker** | O `DeletionWorker` publica `FileDeletedEvent`. O `LoggingWorker` atualiza o status final. |

## 6. Estratégia de Processamento Assíncrono

O FileFlow adota uma estratégia híbrida para o consumo de eventos, combinando **consumidores paralelos (fan-out)** para eventos iniciais críticos e uma **cadeia de mensagens (pipeline)** para eventos subsequentes, garantindo rastreabilidade e eficiência.

### 6.1. Evento Inicial: `FileUploadedEvent` (Consumo Paralelo)

1.  **Publicação:** A **Web API** publica o `FileUploadedEvent` (após a confirmação do upload para o RustFS temporário).
2.  **Consumo Paralelo:** Dois consumidores distintos recebem e processam a mesma mensagem de forma independente:
    *   **`LoggingWorker`:** Consome o `FileUploadedEvent` e registra imediatamente no banco de dados (`ProcessingEvent`) que o arquivo foi recebido e está aguardando migração. Isso garante que, mesmo que o `MigrationWorker` falhe antes de iniciar, há um registro de que o processamento foi tentado.
    *   **`MigrationWorker`:** Consome o `FileUploadedEvent` e inicia a migração física do arquivo no RustFS temporário para o RustFS definitivo.

**Vantagens:** Garante que o log de "Início de Processamento" seja registrado de forma confiável e independente do sucesso da migração, fornecendo rastreabilidade completa desde o primeiro momento.

### 6.2. Eventos Subsequentes (Cadeia de Mensagens)

Após o evento inicial, o fluxo segue uma cadeia de mensagens, onde um worker publica um evento que é consumido por outro para continuar o pipeline:

1.  **`MigrationWorker`** (após migração): Publica `FileMigrationCompletedEvent` (sucesso) ou `FileMigrationFailedEvent` (falha).
2.  **`LoggingWorker`** (ou um consumidor dedicado na API): Consome esses eventos de conclusão e atualiza o status final do `MediaAsset` e registra o `ProcessingEvent` correspondente.

**Vantagens:** Mantém o fluxo lógico e a responsabilidade clara para cada etapa do processamento.

### 6.3. Mensagens Individuais vs. Arrays (Batch)

O FileFlow adota a estratégia de processar cada arquivo como uma mensagem individual no RabbitMQ, em vez de agrupar múltiplos arquivos em uma única mensagem (array). Esta decisão é fundamental para a resiliência e escalabilidade do sistema:

*   **Paralelismo:** Múltiplos *workers* podem consumir e processar arquivos simultaneamente, acelerando o processamento de grandes lotes.
*   **Isolamento de Falhas:** A falha no processamento de um arquivo específico não impede ou afeta o processamento dos demais arquivos do lote. Cada falha é isolada.
*   **Reprocessamento Cirúrgico:** Em caso de erro, apenas o arquivo que falhou precisa ser reprocessado, economizando recursos e tempo.
*   **Simplicidade do Worker:** O código do *worker* se torna mais simples, pois ele sempre lida com um único arquivo por vez, sem a necessidade de lógica complexa para iterar sobre arrays e gerenciar estados internos de um lote de mensagens.

Esta abordagem, combinada com o **Outbox Pattern** do CAP, garante que o lote de arquivos seja registrado atomicamente no banco de dados e que cada arquivo seja enfileirado de forma independente para processamento assíncrono.

## 7. Gerenciamento de Lotes e Reprocessamento

### 7.1. Status `PARTIAL` para o Lote (`UploadBatch`)

O status `PARTIAL` na tabela `UploadBatch` é um indicador crucial para a experiência do usuário e a gestão do sistema. Ele reflete a situação de um lote onde:

*   **Definição:** Pelo menos um `MediaAsset` dentro daquele lote falhou no processamento, mas outros `MediaAssets` do mesmo lote foram processados com sucesso ou ainda estão pendentes.
*   **Determinação:** O `Status` do `UploadBatch` é derivado dos `MediaAssets` associados. Um mecanismo (que pode ser uma *Query* específica na camada `FileFlow.Application` ou um *Worker* dedicado para agregação de status) será responsável por:
    *   Definir o `UploadBatch.Status` como `COMPLETED` se **todos** os `MediaAssets` estiverem `COMPLETED`.
    *   Definir o `UploadBatch.Status` como `FAILED` se **todos** os `MediaAssets` estiverem `FAILED`.
    *   Definir o `UploadBatch.Status` como `PARTIAL` se houver uma **mistura** de `COMPLETED`, `FAILED` e/ou `PENDING` entre os `MediaAssets`.
*   **Valor para o Usuário:** No frontend, o usuário verá que o lote não foi totalmente processado, podendo identificar rapidamente quais arquivos precisam de atenção.

### 7.2. Fluxo de Reprocessamento

O reprocessamento é projetado para ser **cirúrgico e eficiente**, focando apenas nos `MediaAssets` que falharam, sem criar novos registros.

1.  **Identificação da Falha:** O usuário visualiza um `UploadBatch` com status `PARTIAL` ou `FAILED` no dashboard do Angular.
2.  **Ação do Usuário:** O usuário clica em um botão **"Reprocessar Lote"** (ou "Reprocessar Arquivos com Erro"). Essa ação dispara um `ReprocessUploadBatchCommand` (ou `ReprocessFailedMediaAssetsCommand`) para a **Web API**.
3.  **Handler de Reprocessamento (`ReprocessUploadBatchCommandHandler`):**
    *   **Fonte da Verdade Híbrida:** A entidade `MediaAsset` atua como a máquina de estados de negócio, enquanto a tabela de logs (`ProcessingEvent` ou `MediaAssetLog`) atua como o repositório de metadados técnicos temporários (como o `TempRustFSPath` ou `ObjectKey`).
    *   **Consulta ao Banco:** O Handler consulta a tabela `MediaAsset` para identificar os ativos falhos e, em seguida, recupera o último metadado técnico válido na tabela de logs (geralmente o log do tipo `PRE_REGISTERED`).
    *   **Atualização de Estado:** O status na `MediaAsset` é resetado para `PENDING` e o `RetryCount` é incrementado.
    *   **Publicação via CAP:** O novo evento é montado combinando os dados de negócio da `MediaAsset` com o caminho temporário recuperado do log.

    *Exemplo de código do Handler de Reprocessamento:*
    ```csharp
    public async Task<Unit> Handle(ReprocessUploadBatchCommand command, CancellationToken cancellationToken)
    {
        var failedAssets = await dbContext.MediaAssets
            .Where(m => m.UploadBatchId == command.UploadBatchId && m.Status == "FAILED")
            .ToListAsync(cancellationToken);

        if (!failedAssets.Any()) return Unit.Value;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(capPublisher, cancellationToken);

        foreach (var asset in failedAssets)
        {
            // Busca o metadado técnico (TempPath) no log
            var techLog = await dbContext.ProcessingEvents
                .Where(e => e.MediaAssetId == asset.Id && e.EventType == "PRE_REGISTERED")
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

            if (techLog == null) continue;

            asset.Status = "PENDING";
            asset.RetryCount++;
            asset.LastAttemptAt = DateTime.UtcNow;

            var @event = new FileUploadedEvent
            {
                MediaAssetId = asset.Id,
                UploadBatchId = asset.UploadBatchId,
                OriginalFileName = asset.OriginalFileName,
                MimeType = asset.MimeType,
                Size = asset.Size,
                TempPath = techLog.Details["ObjectKey"].ToString(), // Recuperado do JSONB do log
                RetryCount = asset.RetryCount,
                Title = asset.Title,
                Tags = asset.Tags
            };

            await capPublisher.PublishAsync("file.uploaded", @event, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Unit.Value;
    }
    ```
4.  **Consumo pelos Workers:**
    *   Os Workers (como o `MigrationWorker` e `LoggingWorker`) recebem esses novos eventos de reprocessamento.
    *   Devido à **idempotência** do processamento, se um Worker receber uma mensagem para um arquivo que já foi processado com sucesso, ele simplesmente ignora ou confirma a mensagem sem fazer nada.
    *   O Worker foca apenas nos arquivos que estão com `Status = PENDING` ou `RETRY_INITIATED`.
5.  **Atualização do Status do Lote:** Após o reprocessamento e a conclusão dos `MediaAssets` falhos, o status do `UploadBatch` será reavaliado e atualizado (potencialmente para `COMPLETED` se todas as falhas forem resolvidas).
