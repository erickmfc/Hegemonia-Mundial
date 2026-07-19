# IA01: roteiro híbrido de construção

## Fluxo

O plano não simula o `MenuConstrucao`. A execução reutiliza o catálogo indexado pela IA01, o `IA01ConstructionGovernor`, a `IA01CommandQueue`, `Construtor.ConstruirEstruturaIA`, `SistemaGovernoMundial` e o `IA01WorldRegistry`.

```text
necessidade -> IA01BuildPlanRuntime -> governor -> slot/zona -> reserva
-> fila -> IA01BuildExecutor -> WorldRegistry -> confirmação
```

O caminho antigo permanece ativo quando não há plano, quando `Use Scripted Opening` está desligado, ou para intents sem passo correspondente.

## Preparar uma ficha

Crie ou selecione uma `DadosConstrucao`. Configure `itemId`, `prefabDaUnidade`, `capacidades` como estrutura e `strategicRole` explicitamente. Para a prefeitura use `Capital` e `capital.prefeitura`; não dependa do nome do prefab.

## Criar o roteiro

Use `Create > Hegemonia > IA01 > Build Plan`. Em cada passo arraste uma `DadosConstrucao`, defina o papel, a condição, o limite e um dos modos:

- `ExactSlot`: usa o `primarySlotId` exatamente;
- `SlotGroup`: seleciona o primeiro slot compatível do grupo;
- `AutonomousZone`: procura somente na zona configurada.

Para uma abertura equilibrada, crie passos para Capital, EnergyProduction, FoodProduction, Residential e Storage. As condições impedem a execução automática sem necessidade.

## Preparar o layout

Adicione `IA01CityLayout` na hierarquia da IA01. Cada `IA01BuildSlot` filho se registra uma vez no `IA01BuildSlotRegistry`; nenhum `Update` procura a cena. Preencha `slotId`, papel, domínio e footprint. Um `IA01BuildSlotGroup` pai fornece o grupo para seus slots filhos quando o grupo não é preenchido individualmente.

Associe o plano e o layout no `IA01Controller` e configure `Use Scripted Opening`, `Use Prepared Slots` e `Allow Autonomous Expansion`. Ao duplicar o objeto IA01, duplique também sua hierarquia de layout e configure a nova identidade; as reservas são locais ao layout e ao time.

## Pontos especiais

- Prefeitura: passo `Capital`, modo `ExactSlot`, `primarySlotId` do CapitalSlot e política `BlockMandatoryStep`.
- Estaleiro, pier e porto: use domínio `Coastal` ou `Water`, e adicione `IA01NavalBuildSlot` ao mesmo objeto do slot. Defina spawn e direção de saída; a verificação de água e corredor seguro é cacheada por versão do layout.
- Aeroporto: adicione `IA01AirportBuildSlot`, informe início/fim de pista, spawn e aproximação, e configure o footprint do `IA01BuildSlot` para cobrir toda a pista e o corredor reservado.

## Validação e diagnóstico

Abra `Window > Hegemonia > IA01 Build Plan Validator` para encontrar IDs duplicados, passos sem ficha/slot, incompatibilidade de papel, slots especiais ausentes e footprints sobrepostos. Os diagnósticos de runtime incluem plano, passo, modo, papel, slot, estado, alternativas e resultado da validação com o prefixo `ia01_`.

## Save/load

O estado salvo por IA inclui ID/versão do plano, ID do layout, passos concluídos/bloqueados, cooldowns, reservas/ocupação dos slots e o comando planejado pendente. Uma reserva que ainda não foi executada é reenfileirada pelo mesmo comando após o carregamento; uma obra já em construção é reconciliada com o `WorldRegistry` e mantém o slot ocupado.
