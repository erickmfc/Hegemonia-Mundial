# Hegemonia Mundial — validação visual e Play Mode

Data: 04/11/2026  
Cena: `GlobalMapRTS`  
Estado final: Play Mode encerrado; cena não salva.

## Capturas

- `00_editor_cena_carregada.jpg` a `04_play_mode_mapa_celeste_sem_tiles.jpg`: sequência inicial do Editor e do Play Mode.
- `09_playmode_transport.xml` e `11_playmode_coast.xml`: resultados NUnit dos testes aprovados.
- `12_playmode_movement_perf.log`: evidência UTP parcial de movimento e identidade no mapa.
- `16_playmode_mapa_global_estado_visual.jpg`: captura do Play Mode com painel de desempenho.
- `17_editor_playmode_encerrado.jpg`: Editor após encerrar o Play Mode.
- `../01_mapa_global_cena_aberta.png`: visão da ilha no Scene View, fora do Game View.

## Resultado dos testes

1. **Desembarque de transporte — aprovado (1/1):** `PontoDeSaidaAbaixoDoTerrenoDesembarcaNaAlturaDoSolo`. O ponto de saída abaixo do terreno foi ajustado para a altura do solo.
2. **Tanque na costa — aprovado (1/1):** `TankMovesToTheReachableShoreWhenTheClickedPointIsClassifiedAsWater`. O cenário confirma deslocamento de mais de 500 m, chegada a até 100 m da transição água/terra, destino alcançável e contagem de árvores igual a zero.
3. **Identidade e movimento no mapa global — evidência parcial:** o log UTP registra `state=4` para `GlobalMapKeepsValdoriaIdentityAndAllManualConstructionAreasResolve` e `InfantryAndTankMoveOnGeneratedGlobalMapLand`. O XML persistido ficou com zero casos, então esses dois resultados não são contabilizados como confirmação pelo relatório XML.
4. **Métricas contínuas/performance — inconclusivo:** o caso `GlobalMapRecordsLiveFrameTimingAndRenderCounters` iniciou, mas não registrou evento de término. A tentativa isolada também terminou com código 1 e não gerou XML válido.

## Observação visual e desempenho

Na captura `16`, o overlay mostra 6 tiles de terreno, 0 árvores, 576 GameObjects ativos, cerca de 72,8 FPS e 15,89 ms de CPU main/render. É apenas uma amostra pontual, sem baseline, portanto não é uma aprovação de desempenho.

Embora haja tiles carregados, o Game View mostra céu azul e a interface informa que não há unidade selecionada. Assim, a captura não permite inspecionar visualmente o solo, a costa nem um veículo chegando à borda. A visão do terreno aparece no Scene View da captura `../01_mapa_global_cena_aberta.png`, mas isso não substitui a validação visual em runtime.

## Preservação

Nesta etapa não alterei scripts, prefabs ou cenas. Encerrei o Play Mode e descartei as alterações temporárias; não salvei a cena. As capturas e este relatório foram gravados nesta pasta.

## Validação adicional — água, costa e desembarque

- **Costa no mapa global — aprovado em Play Mode:** o tanque foi criado no cenário real `GlobalMapRTS`, recebeu destino classificado como água e chegou à faixa costeira alcançável; o teste registrou deslocamento superior a 500 m, distância final menor que 100 m da margem e 0 árvores. Veja `43_playmode_mapa_global_camera_ajustada.jpg`, `44_teste_costa_em_execucao.jpg`, `45_resultado_teste_costa_aprovado.jpg` e `46_resultado_teste_costa.xml`.
- **Desembarque — aprovado em Play Mode:** `PontoDeSaidaAbaixoDoTerrenoDesembarcaNaAlturaDoSolo` passou duas vezes. No cenário de validação, o navio está em Y=0, o solo em Y=20 e não há NavMesh perto; o veículo é solto e termina no nível Y=20. Veja `47_resultado_desembarque_aprovado.jpg`, `51_playmode_desembarque_reteste_em_execucao.jpg`, `52_resultado_desembarque_playmode_aprovado.jpg` e `53_resultado_desembarque_playmode.xml`.
- **Água sobre solo — aprovado em Edit Mode:** `AguaSobreTerrenoNaoTransformaFundoMarinhoEmPontoDeDesembarque` confirmou que um collider de água sobreposto impede tratar o terreno submerso como ponto de desembarque. Veja `49_resultado_teste_agua_overlay_aprovado.jpg` e `50_resultado_teste_agua_overlay.xml`.
- `48_resultado_runner_desembarque_apos_teste.jpg` registra outra captura aprovada do Test Runner depois do teste de desembarque.

**Limite do cenário:** o teste do navio usa um terreno de validação sintético e não reproduz uma travessia completa de um navio real até uma praia do mapa global. O tanque, por sua vez, foi validado no mapa global real. Os testes acima aprovam o cálculo da altura do solo e a rejeição da camada de água sobre o fundo.

Nesta rodada não alterei scripts, prefabs ou cenas. A pasta contém capturas e resultados NUnit; Play Mode foi encerrado pelo Test Runner e a cena não foi salva.

A captura 43_playmode_mapa_global_camera_ajustada.jpg complementa a observação antiga sobre a captura 16: nela, o Game View em Play Mode mostra o solo e a costa do mapa global, com 	rees: 0 no painel de desempenho.
