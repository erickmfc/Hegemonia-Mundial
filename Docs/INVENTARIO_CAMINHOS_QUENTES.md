# Inventário de caminhos quentes

Este inventário é a regra de triagem para travamentos: nenhuma alteração de
desempenho é considerada concluída sem uma medição no `DiagnosticoDesempenhoJogo`
e uma decisão explícita para o caminho analisado.

## Métricas de aceite

- P95 de frame, pior frame e quantidade de frames acima de 100 ms/250 ms.
- CPU/GPU, GC, memória e `top_offenders` da mesma janela de captura.
- Número de entidades restauradas, bases do Cartel e controladores IA01.

## Caminhos instrumentados

| Caminho | Decisão atual | Prova de validação |
| --- | --- | --- |
| `IA01Manager.ExecuteTick` | manter scheduler por orçamento; resumo limitado a 4 Hz | `ia01_manager_ms`, orçamento e filas da IA01 |
| `IA_BrainMaster` scheduler | manter coordenador global e orçamento por cérebro | `brainmaster_scheduler_ms` |
| `CartelAIController` | manter cadência e cache de Creates; medir direção/decisão separadamente | `cartel_naval_steering_ms`, `cartel_decision_ms` |
| restauração de save | cachear prefabs e repartir spawn/ordens entre frames | `save_restore_spawn_ms`, `save_restore_orders_ms` |
| construção, spawn e NavMesh | manter métricas existentes e atacar apenas o maior ofensor medido | CSV/overlay: `top_offenders` e campos de spawn |
| UI, unidades, combate e efeitos | auditar em cenário carregado antes de alterar comportamento | P95, GC e métricas por subsistema |

## Procedimento obrigatório

1. Capture 20 segundos após o warm-up em campanha normal e em carga.
2. Associe todo frame acima de 250 ms a eventos e ao maior ofensor da janela.
3. Para cada `Update`, `FixedUpdate`, `LateUpdate` ou busca global que apareça
   no perfil, registre aqui: responsável, decisão (`manter`, `agendar`,
   `cachear` ou `remover`) e o cenário que comprovou a alteração.
4. Repita os mesmos cenários após cada correção; não reduza conteúdo, regras de
   IA, saves ou entradas do jogador sem uma regressão comprovada.

## Build reproduzível

Use `Hegemonia.EditorTools.BuildValidationWindows.BuildWindows64` com
`-executeMethod`. A saída pode ser definida por `HEGEMONIA_BUILD_OUTPUT`; o log
só é válido quando contiver `BuildValidation: SUCCESS`.
