# SanchesTV 8.2 — Durable Orchestration Engine

## Objetivo

A SanchesTV continua sendo um aplicativo Windows WPF/.NET. A versão 8.2 adiciona uma camada
local de orquestração durável inspirada nos padrões públicos do Netflix Conductor, sem
incorporar o servidor Java do Conductor e sem adicionar Redis ou Elasticsearch.

## Mapeamento de arquitetura

| Conductor | SanchesTV 8.2 |
| --- | --- |
| Workflow definition/version | `WorkflowDefinition` + `Version` |
| Definition snapshot per run | JSON persistido junto da execução |
| Workflow execution | `WorkflowExecutionState` |
| Task execution | `WorkflowTaskState` |
| Persistent state | SQLite em `%LocalAppData%\\SanchesTV\\orchestration.db` |
| Durable event history | `workflow_events` |
| SCHEDULED / IN_PROGRESS / COMPLETED | estados equivalentes em `WorkflowTaskStatus` |
| FAILED / TIMED_OUT / CANCELED | estados equivalentes em `WorkflowTaskStatus` |
| COMPLETED_WITH_ERRORS | tarefas opcionais |
| FIXED / LINEAR / EXPONENTIAL retry | `WorkflowRetryLogic` |
| Retry jitter | `UseJitter` |
| Task timeout | `Timeout` por tarefa |
| FORK/JOIN | DAG por dependências + execução paralela |
| Worker concurrency | `ConcurrencyLimit` |
| Correlation ID | `CorrelationId` |
| Pause / Resume | `PauseAsync` / `ResumeAsync` |
| Terminate | `TerminateAsync` |
| Restart | `RestartAsync` |
| Retry failed execution | `RetryFailedAsync` |
| Rerun from task | `RerunFromAsync` |
| Crash recovery / redelivery | `RecoverIncompleteAsync` |
| Execution history retention | `PurgeHistoryAsync` |

## Garantias

O estado é salvo antes do motor avançar para a próxima decisão. Se o processo fechar enquanto
uma tarefa estiver `InProgress`, a recuperação a devolve para `Scheduled` e a executa
novamente. Por isso handlers com efeitos externos devem ser idempotentes.

A definição completa usada pela execução é persistida junto do estado. Alterações futuras da
definição não mudam execuções antigas já armazenadas.

## Integração atual

O boot do aplicativo é o primeiro fluxo real:

`database -> seed -> channels -> home`

Cada etapa tem política de retry/timeout própria e o fluxo fica registrado localmente. O
pipeline de vídeo permanece desacoplado; libmpv, LibVLC e Playback Intelligence não foram
substituídos pelo orquestrador.

## Segurança e privacidade

O histórico do motor é local. Os eventos do motor registram estado operacional e mensagens de
erro limitadas; não devem receber tokens, senhas ou URLs privadas como payload de diagnóstico.

## Diferenças intencionais do servidor Conductor

A SanchesTV é um único aplicativo desktop. Portanto esta implementação não replica componentes
de cluster que não agregariam valor no Windows local, como Redis/Dynomite, Elasticsearch,
Zookeeper, gRPC server ou workers remotos. O objetivo é fidelidade dos mecanismos de execução,
não carregar infraestrutura de datacenter dentro do instalador.

## Proveniência

A arquitetura foi estudada nos repositórios públicos Netflix/conductor e
conductor-oss/conductor, ambos associados ao ecossistema Apache-2.0. Nenhum servidor Java do
Conductor é empacotado na SanchesTV.
