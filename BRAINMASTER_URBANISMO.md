# BrainMaster Urbanismo

Este documento adapta o plano de urbanismo da IA para a arquitetura real do projeto.

## Camadas novas

- `IA_SemanticMapPlanner`
  - usa o `IA_MapAnalyzer` atual e gera uma malha logica com terreno, ocupacao, reserva, risco, clearance e distancia da costa.
- `IA_ZonePlanner`
  - transforma a malha semantica em setores da base: comando, civil, industrial, militar, aerodromo, naval e logistica.
- `IA_LotPlanner`
  - procura lotes validos por zona, pontua candidatos e resolve rotacao basica.
- `IA_UrbanBuildValidator`
  - centraliza a validacao antes da construcao usando semantica + setor + `BuildService.ValidatePlacement`.
- `IA_ConstructionPlanner`
  - prepara o blueprint ativo da base e expoe uma API unica para planejar construcao.

## Como isso encaixa no fluxo atual

- `IA_BrainMaster`
  - instancia os novos modulos e registra no scheduler.
- `IA_Context`
  - agora carrega os planejadores novos junto com os modulos antigos.
- `IA_BuildDirector`
  - continua funcionando do jeito atual.
  - o passo seguinte e trocar as buscas impulsivas por chamadas ao `IA_ConstructionPlanner`.

## Responsabilidade de cada modulo antigo

- `IA_MapAnalyzer`
  - continua como leitura bruta de terreno e footprint.
- `IA_WorldState`
  - continua como fonte de estruturas, unidades, inimigos e centro da base.
- `IA_BackendBridge.BuildService`
  - continua como validacao final e construcao real.
- `IA_BuildDirector`
  - vira o orquestrador, mas deixa de escolher lotes diretamente quando a migracao terminar.

## Ordem segura para migrar

1. Usar `IA_ConstructionPlanner.TryPlanBuild(...)` primeiro em construcoes aereas e navais.
2. Migrar `prefeitura`, `quartel`, `fabrica` e `armazem` para lotes por zona.
3. Reservar corredores e buffers por setor.
4. Trocar o bootstrap para usar blueprint em vez de ponto por impulso.
5. So depois automatizar ruas, quarteiroes e expansao por bandeira.

## Regra pratica para a proxima etapa

Em vez de:

```text
buscar ponto livre -> validar -> construir
```

ficara:

```text
escolher setor -> pedir lote ao IA_LotPlanner -> validar no IA_UrbanBuildValidator -> reservar -> construir
```

## Observacao importante

Nesta etapa a arquitetura nova foi adicionada sem desmontar a `BrainMaster`.
Isso evita uma troca brusca e deixa a migracao controlada, modulo por modulo.
