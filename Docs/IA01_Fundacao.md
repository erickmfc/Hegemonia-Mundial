# Fundacao IA01

## Objetivo

IA01 esta isolada como uma camada-base de IA. Esta entrega fecha a fundacao segura e integrada:

- bootstrap de identidade e perfil em runtime
- orquestracao por controller e manager
- eventos isolados por nacao
- registro e indices do mundo
- agendamento, telemetria e orcamento por fatia
- integracao de save/load
- testes de isolamento no modo de edicao

Planejamento de cidades, guerra, comercio autonomo e os demais diretores sao proximas camadas. A fundacao nao inventa regras para esses sistemas nem executa acoes sem uma fila e uma validacao oficial.

## Auditoria De Servicos

The existing project already exposes several services that IA01 can reuse safely:

- `SistemaGovernoMundial`: source of country identity, government state, and economic snapshot data.
- `SistemaSaveGame`: now stores and restores `SaveIA01NationState` data.
- `GerenciadorTempo`: provides the day counter used by the save system and the IA01 runtime context.
- `GameDifficultyManager`: supplies the difficulty profile that IA01 stores in runtime state.
- `DiagnosticoDesempenhoJogo`: receives telemetry and restore logs when available.
- `RegistroEntidadesJogo`: still available for legacy entity registration, but not coupled directly to IA01 foundation logic.
- `SistemaIndustrialNacional`: read through `IA01GameStateBridge`, mirroring per-country industrial stocks and operational metrics into the private IA01 context.
- `SistemaMercadoGlobal`: read through `IA01GameStateBridge`, mirroring aggregate market supply, demand, and catalog size into IA01 metrics.
- `InteractionModeService`: available for future command and UI arbitration, but not part of the initial IA01 slice.

## Estrutura IA01

All new foundation code lives under the isolated namespace `Hegemonia.AI.IA01`.

Files:

- `Assets/scripts/IA01/IA01CoreTypes.cs`
- `Assets/scripts/IA01/IIA01Module.cs`
- `Assets/scripts/IA01/IA01NationProfile.cs`
- `Assets/scripts/IA01/IA01ServiceDiagnostics.cs`
- `Assets/scripts/IA01/IA01SaveState.cs`
- `Assets/scripts/IA01/IA01EventBus.cs`
- `Assets/scripts/IA01/IA01WorldRegistry.cs`
- `Assets/scripts/IA01/IA01Telemetry.cs`
- `Assets/scripts/IA01/IA01Scheduler.cs`
- `Assets/scripts/IA01/IA01RuntimeContext.cs`
- `Assets/scripts/IA01/IA01Controller.cs`
- `Assets/scripts/IA01/IA01Manager.cs`

Supporting validation:

- `Assets/Tests/EditMode/IA01FoundationTests.cs`

## Fluxo Em Runtime

1. A controller boots a private `IA01RuntimeContext`.
2. Identity is built from controller overrides or from `SistemaGovernoMundial`.
3. The manager binds controllers, registers them in the world registry, and subscribes them to the shared event bus.
4. `IA01GameStateBridge` reads government, industrial, market, and time services into each controller's private context; it never writes to those services.
5. The scheduler selects dirty or due controllers and gives each one a budgeted slice.
5. Telemetry records slice counts, frame counts, and per-controller execution data.
6. Save state captures each nation independently, including profile snapshot, caches, timers, metrics, and service diagnostics.

## Garantias De Isolamento

The foundation is intentionally isolated at the controller level:

- each controller keeps its own `IA01RuntimeContext`
- each controller publishes to its own nation route on the shared event bus
- the world registry indexes records by nation, team, kind, domain, and entity id
- save state is captured and restored per controller, not as one global blob
- the scheduler works on controller instances, not on shared global AI state

The edit mode tests validate:

- distinct resource and memory values do not bleed between two controllers
- nation-scoped events only reach the matching controller
- save capture and restore preserve two different nations independently
- both controllers can be scheduled in the same tick when marked dirty
- reapplying an unchanged identity does not reset the deterministic random sequence or create false dirty work

## Integracao Com Save

`SistemaSaveGame` now persists IA01 data in `DadosDoJogo.estadosIA01`.

Behavior:

- save version was bumped to `10`
- capture occurs during `SalvarJogo()`
- restore occurs during the post-scene restoration pass
- restoration also triggers when IA01 data exists even if entity restoration is empty

## Telemetria Inicial

IA01 now records:

- `LastFrameMs`
- `AverageFrameMs`
- per-controller slice milliseconds
- event counts

The first numeric runtime measurement is produced when the manager executes a real slice in Unity. I did not run the Unity editor in this shell session, so no live benchmark value is recorded here.

## Limites Desta Entrega

- city simulation logic
- war resolution
- market trading logic
- production chains beyond the save/load and context hooks
- changes to legacy services outside the minimum integration path

## Proximas Camadas

- `IA01Controller` can already read government snapshots and service diagnostics.
- `IA01Manager` can already restore save states and spawn missing controllers later if needed.
- `IA01Telemetry` already exposes the data needed for a first production benchmark.
