# Documento de Visão e Arquitetura: FileFlow - Sistema de Gestão de Uploads Assíncronos

**Autor:** Manus AI (PO & Tech Lead)
**Data:** 16 de Maio de 2026

## 1. Visão do Produto (Visão do PO)

### 1.1. O Problema
O upload e a gestão de arquivos de mídia (fotos, PDFs, vídeos) são operações que, quando realizadas de forma síncrona, causam uma experiência de usuário insatisfatória e sobrecarregam os servidores. A necessidade de suportar múltiplos uploads por página, mantendo a distinção de tipos de arquivo, agrava o problema. Além disso, a gestão de arquivos temporários no *object storage* sem um mecanismo de limpeza pode levar ao acúmulo de dados desnecessários e custos.

### 1.2. A Solução Proposta
Desenvolver o **FileFlow - Sistema de Gestão de Uploads Assíncronos** que permita o upload de múltiplos arquivos (fotos, PDFs, vídeos) por página. O sistema oferecerá feedback imediato ao usuário, enquanto o processamento (migração do bucket temporário para o definitivo) e a gestão de *lifecycle* dos arquivos temporários ocorrerão em *background*. A arquitetura será baseada em microsserviços .NET, Angular no frontend, RustFS para *object storage* e RabbitMQ para mensageria, com foco em execução local e poucas dependências externas.

### 1.3. Escopo e Funcionalidades Principais

| Funcionalidade | Descrição | Valor para o Negócio |
| :--- | :--- | :--- |
| **Upload Múltiplo de Arquivos (Lotes)** | Interface no Angular para upload de várias fotos, PDFs e vídeos por página, organizados em lotes nomeados. | Aumenta a produtividade do usuário e a flexibilidade na gestão de conteúdo. |
| **Feedback Imediato** | A API .NET retorna um `Protocolo de Upload` instantaneamente. | Elimina a frustração da espera e previne *timeouts*. |
| **Catálogo de Mídia e Dashboard** | Tela para acompanhar o progresso de cada arquivo/lote e gerenciar os ativos de mídia (com Título e Tags). | Transparência, controle e organização avançada para o usuário. |
| **Processamento Assíncrono** | Microsserviços .NET processam a migração de arquivos do bucket temporário para o definitivo em *background*. | Escalabilidade, resiliência e estabilidade do sistema principal. |
| **Notificação em Tempo Real** | Alertas via WebSockets (SignalR) quando o processamento de um arquivo ou lote é concluído. | Engajamento e agilidade na entrega da informação. |
| **Worker de Limpeza de Arquivos Temporários** | Serviço em *background* que remove arquivos antigos do bucket temporário. | Otimização de custos de armazenamento e gestão eficiente de recursos. |
| **Armazenamento de Arquivos** | Utilização de RustFS para buckets temporário e definitivo. | Solução de *object storage* robusta e compatível com ambiente local. |
| **Registro de Operações** | Utilização de PostgreSQL (ou Elastic para cenários mais complexos) para registro e acompanhamento de status. | Facilita a depuração e o monitoramento do fluxo de processamento. |

### 1.4. Backlog Inicial (Épicos e Histórias de Usuário)

**Épico 1: Infraestrutura Base e Upload Múltiplo**
*   **US01:** Como usuário, quero fazer upload de múltiplas fotos em uma única operação, para que eu possa adicionar várias imagens de uma vez.
*   **US02:** Como usuário, quero fazer upload de múltiplos PDFs e vídeos na mesma página, para que eu possa gerenciar diferentes tipos de mídia simultaneamente.
*   **US03:** Como sistema, devo gerar links pré-assinados para o frontend para cada arquivo a ser carregado, permitindo o upload direto para o RustFS temporário.
*   **US04:** Como sistema, devo receber a confirmação do upload do frontend, salvar os metadados (Título, Tags, caminho temporário) e enviar uma mensagem para a fila de processamento.

**Épico 2: Processamento e Migração de Arquivos**
*   **US05:** Como *worker* de migração, devo consumir mensagens da fila de processamento, migrar um arquivo por vez do bucket temporário para o definitivo no RustFS.
*   **US06:** Como *worker* de migração, devo enviar uma mensagem para a fila de notificação a cada arquivo processado, indicando sucesso ou falha.
*   **US07:** Como *worker* de migração, devo atualizar o banco de dados relacional com o link definitivo do arquivo após a migração bem-sucedida.
*   **US08:** Como *worker* de registro, devo consumir mensagens da fila de notificação e atualizar o status detalhado de cada arquivo no banco de dados de registro (PostgreSQL/Elastic).

**Épico 3: Acompanhamento, Notificação e Limpeza**
*   **US09:** Como usuário, quero ver o status individual de cada arquivo, o status geral do lote de upload e os metadados (Título, Tags) em um dashboard no Angular.
*   **US10:** Como usuário, quero receber notificações em tempo real (via SignalR) sobre o progresso e a conclusão dos meus uploads.
*   **US11:** Como *worker* de limpeza, devo periodicamente escanear o bucket temporário do RustFS e remover arquivos com mais de X dias de idade.
*   **US12:** Como usuário, quero poder reprocessar arquivos que falharam na migração, para não perder o lote inteiro.
*   **US13:** Como usuário, quero poder deletar arquivos de mídia do meu catálogo, removendo-os do armazenamento definitivo.
*   **US14:** Como sistema, devo garantir que a comunicação entre microsserviços seja feita exclusivamente via RabbitMQ.

---

## 2. Arquitetura Técnica (Visão do Tech Lead)

Para suportar os requisitos de negócio e resolver o problema de *long requests* no upload e processamento de mídia, adotaremos uma arquitetura de microsserviços orientada a eventos, com foco em execução local. A stack principal será **.NET (Backend)** e **Angular (Frontend)**.

### 2.1. Desenho da Arquitetura

A arquitetura será composta pelos seguintes componentes principais:

| Componente | Tecnologia Escolhida | Responsabilidade |
| :--- | :--- | :--- |
| **Frontend SPA** | Angular 18+ | Interface do usuário para upload de múltiplos arquivos, dashboard de status e conexão WebSocket para notificações. |
| **API Gateway / Web API | ASP.NET Core 8 (Minimal API) | Ponto de entrada para requisições HTTP do frontend. Gera links pré-assinados para upload direto no RustFS. Recebe confirmações de upload, valida *payloads*, autentica/autoriza, persiste metadados (Título, Tags) e publica eventos na mensageria. |
| **Mensageria (Message Broker)** | RabbitMQ (Docker) | Atua como *buffer* e roteador de mensagens. Desacopla os serviços, garante durabilidade e entrega confiável das tarefas. |
| **Microsserviços Workers | .NET 8 Worker Service (projeto único) | Consome mensagens da fila de processamento (migração, registro, limpeza, deleção). Realiza a migração de arquivos, atualiza o status no banco de dados e remove arquivos temporários. |
| **Banco de Dados Relacional** | PostgreSQL (Docker) | Armazena metadados definitivos dos arquivos (após migração), links definitivos, e informações de usuário. |
| **Banco de Dados de Registro/Temporário** | PostgreSQL (Docker) | Armazena o estado das requisições de upload, logs detalhados de processamento, status intermediários e atua como máquina de estados para os ativos de mídia. Para ambiente local, PostgreSQL é recomendado pela simplicidade em relação ao Elastic. |
| **Object Storage** | RustFS (Docker) | Armazenamento persistente e compatível com S3 para buckets temporário e definitivo. |
| **Serviço de Notificação em Tempo Real** | ASP.NET Core SignalR | Mantém conexões persistentes (WebSockets) com o frontend Angular para enviar *pushes* de atualização de status e progresso.

### 2.2. Fluxo de Execução (O Caminho Feliz)

1.  O usuário seleciona múltiplos arquivos (fotos, PDFs, vídeos) no **Angular** e clica em "Upload".
2.  O Angular faz uma requisição `POST /api/uploads/prepare` para a **Web API (.NET)**, enviando a lista de arquivos a serem carregados.
3.  A Web API gera um `UploadBatchId` (GUID) e, para cada arquivo, um link pré-assinado para upload direto no **RustFS (bucket temporário)**. Salva metadados iniciais do lote e dos arquivos no **Banco de Dados de Registro/Temporário** (PostgreSQL) com status `PENDING`.
4.  A Web API responde imediatamente ao Angular com `HTTP 202 Accepted`, o `UploadBatchId` e os links pré-assinados.
5.  O Angular exibe o dashboard de upload e, para cada arquivo, faz o upload direto para o **RustFS temporário** usando o link pré-assinado.
6.  Após o upload de cada arquivo para o RustFS, o Angular envia uma requisição `POST /api/uploads/confirm` para a **Web API (.NET)**, confirmando o upload do arquivo e seus metadados (caminho temporário no RustFS).
7.  A Web API atualiza o status do arquivo no **Banco de Dados de Registro/Temporário** e publica uma mensagem (`FileUploadedEvent`) no **RabbitMQ** para iniciar o processamento.
8.  O **Microsserviço de Migração de Arquivos** consome a mensagem do RabbitMQ.
9.  O Microsserviço de Migração copia o arquivo do **RustFS temporário** para o **RustFS definitivo**.
10. O Microsserviço de Migração atualiza o **Banco de Dados Relacional** com o link definitivo do arquivo e o status `MIGRATED`.
11. O Microsserviço de Migração publica uma mensagem (`FileMigratedEvent`) no **RabbitMQ**.
12. O **Microsserviço de Registro de Operações** consome o `FileMigratedEvent` e atualiza o status detalhado do arquivo no **Banco de Dados de Registro/Temporário**.
13. O **Serviço de Notificação em Tempo Real (SignalR)**, que pode ser parte da Web API ou um serviço dedicado, recebe o evento e envia uma notificação via WebSocket para o cliente Angular específico, atualizando o dashboard de status.
14. O **Microsserviço de Limpeza de Arquivos Temporários** executa periodicamente, escaneia o **RustFS temporário** e remove arquivos que excedem um tempo de vida configurável (ex: 24 horas).

### 2.3. Decisões Técnicas e Justificativas (Foco Local)

*   **RustFS (Docker):** Escolha ideal para *object storage* local. É compatível com a API S3, o que facilita uma futura migração para serviços de nuvem (AWS S3, Azure Blob Storage) se necessário. Rodar em Docker simplifica a configuração e isolamento.
*   **RabbitMQ (Docker):** Um *message broker* robusto e amplamente utilizado, com excelente suporte para .NET. Rodar em Docker garante um ambiente de desenvolvimento consistente e fácil de configurar localmente. Essencial para desacoplar os microsserviços e garantir a resiliência do fluxo assíncrono.
*   **PostgreSQL (Docker) para Banco de Dados Relacional e de Registro:** Para o ambiente local, utilizar PostgreSQL para ambos os propósitos (metadados definitivos e registro/temporário) simplifica a infraestrutura. O Elastic, embora poderoso para logs e buscas, adiciona complexidade desnecessária para um projeto local focado em aprendizado. O PostgreSQL, com indexação adequada, pode gerenciar o registro de operações de forma eficiente para este escopo.
*   **ASP.NET Core SignalR:** Permite a comunicação bidirecional em tempo real entre o backend .NET e o frontend Angular, crucial para fornecer feedback instantâneo sobre o progresso dos uploads e processamentos sem a necessidade de *polling* constante.
*   **Microsserviços .NET Worker Services:** A abordagem de *Worker Services* no .NET é perfeita para implementar os *background tasks* (migração, registro, limpeza). Eles são leves, eficientes e podem ser facilmente implantados como contêineres Docker.
*   **Comunicação HTTP (Frontend-Backend) e Mensageria (Backend-Backend):** Esta separação é fundamental. HTTP para interações síncronas e diretas com o usuário, e mensageria para operações assíncronas e desacopladas entre os serviços, garantindo a escalabilidade e resiliência do sistema.
*   **Remoção de FFmpeg/ImageMagick:** Como o foco principal é a migração e gestão de arquivos, e não a transformação de mídia, essas ferramentas foram removidas para simplificar a arquitetura e reduzir dependências.

### 2.4. Considerações para o Ambiente Local

*   **.NET Aspire:** Será a ferramenta chave para orquestrar todos os serviços (RustFS, RabbitMQ, PostgreSQL) em um ambiente de desenvolvimento local, facilitando o setup, a execução e fornecendo telemetria integrada.
*   **Configuração Simplificada:** As configurações dos serviços .NET e Angular serão adaptadas para apontar para as instâncias locais do RustFS, RabbitMQ e PostgreSQL.

Esta arquitetura revisada atende aos requisitos de processamento assíncrono, múltiplos uploads e execução local, fornecendo uma base sólida para o desenvolvimento do projeto.

### 1.5. Fluxo de Processamento e Transições de Estado (Visão Geral)

Para garantir uma experiência de usuário fluida e um sistema robusto, o FileFlow opera com um fluxo de processamento assíncrono bem definido, onde cada arquivo (`MediaAsset`) transita por diferentes estados. O frontend fornece feedback em tempo real sobre o progresso, enquanto os microsserviços trabalham em *background*.

**Principais Etapas do Fluxo:**
1.  **Pré-registro:** Ao iniciar o upload, o sistema cria um registro inicial (`MediaAsset` com status `PENDING`), garantindo feedback imediato ao usuário.
2.  **Confirmação de Upload:** Após o upload direto para o RustFS temporário, a API confirma e o status do `MediaAsset` muda para `UPLOADED`, disparando o processamento assíncrono.
3.  **Migração:** Um *worker* dedicado move o arquivo do bucket temporário para o definitivo. Durante este processo, o `MediaAsset` pode ter o status `MIGRATING`.
4.  **Conclusão/Falha:** Ao final da migração, o `MediaAsset` é atualizado para `MIGRATED` (com o link final) ou `FAILED` (com a mensagem de erro).
5.  **Reprocessamento:** Em caso de falha, o usuário pode solicitar o reprocessamento, e o sistema tentará novamente, atualizando o `RetryCount`.
6.  **Deleção:** Um fluxo separado permite a remoção completa do arquivo e seus metadados.
7.  **Limpeza:** Um *worker* automático remove arquivos antigos do bucket temporário.

**Estratégia de Processamento:** Cada arquivo é tratado como uma mensagem individual no RabbitMQ. Isso garante:
*   **Paralelismo:** Múltiplos *workers* podem processar arquivos simultaneamente, acelerando o lote.
*   **Isolamento de Falhas:** A falha de um arquivo não afeta o processamento dos demais.
*   **Reprocessamento Cirúrgico:** Apenas o arquivo com erro é reprocessado, economizando recursos.

Para um detalhamento completo da **Matriz de Transição de Estados**, **Estratégia de Processamento Assíncrono** e **Responsabilidades dos Serviços**, consulte a seção **5. Matriz de Transição de Estados e Responsabilidades** e **6. Estratégia de Processamento Assíncrono** no documento `arquitetura_tecnica_fileflow.md`.

### 2.5. Estrutura de Projetos e Padrões

O projeto será organizado em um monorepo, utilizando **.NET Aspire** para orquestração e **CQRS (Command Query Responsibility Segregation)** com **MediatR** para a camada de aplicação. Esta abordagem garante uma separação clara de responsabilidades, testabilidade e escalabilidade.

**Principais Projetos:**
*   **`FileFlow.AppHost`**: Orquestra todos os serviços e recursos (PostgreSQL, RabbitMQ, RustFS).
*   **`FileFlow.ServiceDefaults`**: Configurações compartilhadas de telemetria e *health checks*.
*   **`FileFlow.Shared`**: Contratos de mensageria e DTOs compartilhados.
*   **`FileFlow.Data`**: Entidades do banco de dados, `DbContext` e migrações.
*   **`FileFlow.Application`**: Comandos, Queries e Handlers do CQRS, contendo a lógica de negócio centralizada.
*   **`FileFlow.Workers`**: Projeto único para todos os .NET Worker Services (Migração, Logging, Limpeza, Deleção).
*   **`FileFlow.Api`**: API Gateway com *controllers* que disparam comandos e queries.
*   **`FileFlow.Workers`**: Microsserviços *Worker* (Migração, Limpeza, Deleção, Logging) que consomem eventos e executam comandos/queries.
*   **`FileFlow.Frontend`**: Aplicação Angular para a interface do usuário.

Para um detalhamento completo da estrutura de pastas e responsabilidades de cada projeto, consulte a seção **4. Estrutura de Pastas/Projetos** no documento `arquitetura_tecnica_fileflow.md`.
