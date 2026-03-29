# Integracao Gradual - IA_BrainMaster

Este documento define como substituir a IA antiga sem quebrar partidas.

## 1) Fase Shadow (sem comando)

- Adicione `IA_BrainMaster` em um GameObject de IA.
- Configure `TeamId` para o time inimigo correto.
- Deixe `IntegrationMode = ShadowReadOnly`.
- Resultado: a IA nova apenas analisa mapa, perfil e ameacas. Nao envia ordens.

Checklist:
- Sem erro no Console.
- `RuntimeSummary` atualizando.
- Fila de comandos permanece vazia.

## 2) Fase Hybrid (controle parcial)

- Mude para `IntegrationMode = Hybrid`.
- Mantenha IA antiga ativa.
- Resultado: IA nova passa a construir, produzir e reposicionar tropas sem tomar controle total.

Checklist:
- Estruturas sendo erguidas pelo backend (sem abrir menu UI).
- Producao saindo de Fabrica/Estaleiro/Heliporto/Aeroporto.
- Sem travamentos de frame em partidas longas.

## 3) Fase Full (controle total)

- Mude para `IntegrationMode = Full`.
- Ative `DisableLegacyAIWhenFull = true`.
- Resultado: IA antiga e desativada automaticamente (`IA_Suprema`, `IA_Dominadora`, `IA_Comandante`).

Checklist:
- Comandos de ataque/defesa emitidos apenas pela fila central.
- Sem conflito de ordens entre IAs.
- Logs de `IA_DebugMonitor` estaveis.

## 4) Rollback seguro

Se houver comportamento inesperado:
- Volte para `IntegrationMode = Hybrid` ou `ShadowReadOnly`.
- Reative componentes legados desativados manualmente no Inspector, se necessario.

## 5) Regras tecnicas aplicadas

- Sem dependencia de clique na UI de menu para a IA.
- Toda acao passa por servicos internos:
  - `BuildService`
  - `ProductionService`
  - `SquadService`
  - `AbilityService`
  - `CommandService`
- Todas as ordens passam por `IA_CommandQueue` com:
  - prioridade
  - status
  - cooldown
  - deduplicacao
- Scheduler por subciclos:
  - `IA_PerformanceScheduler` com budget por modulo
  - processamento em lote
  - cache de reconhecimento

