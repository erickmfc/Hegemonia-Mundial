# Auditoria de integração do checkout

Data: 11/10/2026. Esta auditoria registra evidência estática do checkout atual e separa alterações locais de validação que ainda depende do Unity Test Runner.

## Correção desta continuação

`SistemaSaveGame.PossuiEstadoRestauravel` só agendava a restauração depois da cena quando havia entidades, estados IA01/IA02, cidades abstratas ou pedidos logísticos. Governo, indústria, fila de produção, estado de quartel, manifesto, Deusa, tempo/câmera e outros snapshots diferidos podiam existir sozinhos no save e não iniciar a coroutine que os aplica.

O predicado agora reconhece os estados diferidos efetivamente usados pelo restaurador, inclusive reservas ativas de produção automática. A limpeza das entidades da cena só ocorre quando o save contém entidades para substituir, e remove apenas entidades da cena ativa; quando não há snapshot de entidades, os `SaveableEntity` presentes são mapeados sem apagar o mundo carregado. Foi acrescentado `SaveSemEntidadesAgendaRestauracaoParaEstadoNacionalAdiado`, que cobre save vazio, snapshot de Governo, fila de produção, histórico industrial e ordem automática ativa. O teste foi escrito, mas não executado nesta continuação.

## Gate ofensivo N1

`TentarIniciarMissao` já adiava N2 quando `PodeEscalarParaN2` era falso. A revisão do caminho de N1 encontrou que a transição automática exigia prontidão, mas `AoAbaterPresidente` podia definir `GuerraTotal` diretamente; em seguida, a porta de missão não repetia o gate. Agora a missão N1 chama `PodeEscalarParaGuerraTotal` antes de reservar grupo ou enfileirar Attack. O estado de intenção permanece e o loop estratégico tenta novamente no intervalo normal. `MissaoOfensivaN2EN1AguardaProntidaoEconomica` cobre recusa de N2 por combustível e N1 por snapshot indisponível; teste escrito, não executado nesta continuação.

## Matriz de requisitos e evidência

| Área | Evidência atual | Estado de validação |
|---|---|---|
| Tesouro, produção, manpower, combustível operacional e prontidão IA03 | [AuditoriaRotaProducaoMilitar.md](AuditoriaRotaProducaoMilitar.md); rotas de jogador, BrainMaster, Nova IA, IA01/02 e Soberana inspecionadas | Reserva configurável conectada à mobilização central e aos gates N2/N1; EditMode da reserva 1/1, perfis legados 2/2, produção/manpower PlayMode 12/12 e combustível nacional PlayMode 10/10 passaram. A regressão N2 com perdas e reposição após as mudanças ainda está pendente |
| Petróleo e indústria | plataforma, petroleiro, pier, estoque nacional e refino existentes; `SistemaIndustrialNacional` tem save próprio | A suíte PlayMode de combustível/petróleo passou 10/10 nesta continuação; o caso de navegação do petroleiro ainda usa atualização direta, sem provar a simulação física em tempo real |
| Governo e diplomacia | `SaveGovernoMundialData` serializa países, relações, propostas e notícias; acordos temporários são reconciliados após restauração; há testes de acordos IA03 e regressões de menu/equipe | Código/testes localizados; suites não reexecutadas |
| Cidades e população | `CidadeComplexoUrbano` integra cidade materializada; `GerenciadorDivisaoTerritorial` mantém cidade abstrata, registro urbano, sincronização proporcional da população e materialização por limiar | Testes EditMode/PlayMode existem para persistência, migração, prefeitura, crescimento e diretório; sem resultado atual do Runner |
| Persistência de mundo | save versão 22 captura entidades, Governo, cidades abstratas, IA, indústria, logística, produção, manifestos e estado RTS | Falha no agendamento tardio corrigida; regressão adicionada e pendente de execução |
| Performance de campanha longa | [AuditoriaCampanhaLonga.md](AuditoriaCampanhaLonga.md): A/B completos em 100%; C cobre 19 min a 200%; D não tem captura. A/B não mostram crescimento monotônico nos checkpoints, mas registram hitches longos | Sem nova campanha; A/B/C/D de 90 min permanecem fora desta etapa |
| Reposição após perda em N2 | a cadeia N2 anterior foi validada pelo usuário; existe deduplicação/cooldown na produção automática | perda real → necessidade → pedido único → produção real não foi observada nesta continuação |

## Pendências e limites

- Medição às 14:28 UTC de 11/10/2026: 2,57 GiB de RAM livre. O Editor principal (PID 19652) usa aproximadamente 3,77 GB; há seis outros processos `Unity.exe` menores. A ponte MCP não está disponível nesta sessão; não iniciei outro Editor nem abri/salvei `GlobalMapRTS`.
- Foram preservados os XMLs da execução individual `NationalTreasuryBuild_ChargesSuccessAndRefundsRejectedBuild`: [primeira tentativa 0/1](../ValidationResults/ia03-national-treasury-build-20261011.xml) e [resultado final 1/1](../ValidationResults/ia03-national-treasury-build-20261011-pass.xml), após a fixture passar a começar com saldo suficiente. Também estão preservados os XMLs da falha pré-correção do cálculo de reserva e dos resultados finais 1/1 e 2/2. A recompilação após o ajuste de arredondamento concluiu sem erro.
- A trilha antiga `CerebroIA`/`AnalistaExecutivo` continua usando orçamento local e spawn direto em `SampleScene`; essa cena não está em `EditorBuildSettings`, que lista `GlobalMapRTS`. Migrá-la só é necessário se voltar a ser uma cena jogável.
- Persistência ainda depende de cada `prefabKey` do save resolver para um prefab disponível no catálogo/Resources no build. A execução de round-trip em build continua pendente.
- Não houve reforma visual de cidade, reescrita do menu Governo, inclusão de receitas ou campanha longa nesta continuação. As alterações locais preexistentes do checkout foram preservadas.

## Atualização posterior da sessão Unity

Às 11:07 locais de 11/10/2026, o Editor existente concluiu `AISovereignTerritoryTests.NationalTreasuryBuild_ChargesSuccessAndRefundsRejectedBuild` com 0/1: a fixture não chegava à callback recusada porque o saldo era menor que o preço efetivo. Depois de ajustar o saldo inicial e recompilar a fixture, a execução final terminou 1/1 às 11:12 locais; ver os dois XMLs acima. Às 14:28 UTC, a máquina tinha 2,57 GiB livres, com o processo principal usando 3,77 GB e outros seis processos Unity ativos; nenhum outro Editor foi iniciado.

## Acesso ao Editor e decisão de segurança

Às 14:28 UTC, `unity status` havia retornado `STATUS_NO_INSTANCES`, embora a instância do Editor estivesse ativa. Uma verificação posterior com `unity pipeline list` encontrou o mesmo Editor (PID 19652) e a porta 7800 acessível; comandos `unity command` com `--project-path` conseguiram recompilar e executar filtros no Test Runner existente, sem iniciar Editor em batch.

## Próxima validação necessária

No Editor já aberto, executar individualmente as suítes relevantes de economia/manpower, combustível, produção terrestre/aérea/naval, diplomacia/Governo, cidades/população, persistência e prontidão IA03; guardar o XML final de cada suite. Depois executar a regressão N2 com perda e reposição real. Conferir console e compilar antes de declarar a integração validada.

## Atualização do Runner de manpower — 2026-10-11

- A conversão para `decimal` corrigiu o arredondamento da reserva: a execução inicial falhou com 101 contra 100 esperado; depois da recompilação, o teste estratégico passou 1/1 e os assets legados de Valdoria/Karsovia passaram 2/2. XMLs: [falha antes da correção](../ValidationResults/ia03-manpower-reserve-20261011-fail.xml), [teste de reserva após correção](../ValidationResults/ia03-manpower-reserve-20261011-pass.xml), [perfis legados](../ValidationResults/ia03-manpower-profile-compatibility-20261011-pass.xml).
- O processo do Editor permaneceu o PID 19652. A API concluiu os testes; o Editor oscilou como não responsivo durante a restauração e seu log repetiu o erro de carregamento de TerrainData já registrado. Às 14:42:53Z, `editor_status` confirmou `ready`, compilando=false, domain reload=false e Play Mode parado. Não foi reiniciado.

## Reexecução de economia militar e combustível — 2026-10-11

- A fila aérea corrigida passou 1/1 em PlayMode; o XML está em [fila com aeroporto operacional](../ValidationResults/aircraft-purchase-unpowered-airport-queue-20261011-pass.xml).
- `IA03ProductionManpowerPlayModeTests` passou 12/12 em 4,41 s, e `FuelNationalInventoryPlayModeTests` passou 10/10 em 2,79 s. XMLs: [manpower/produção](../ValidationResults/ia03-production-manpower-playmode-20261011-rerun-pass.xml) e [combustível/petróleo](../ValidationResults/fuel-national-inventory-playmode-20261011-rerun-pass.xml).
- O pipeline voltou a responder a `test_status` após timeout durante a restauração de cena. As contagens terminais foram conferidas antes de copiar os XMLs. Depois da suíte de combustível, `editor_status` voltou a expirar em 10 s; não iniciei outra suíte enquanto o estado do Editor permanece incerto.
- Reposição integrada após perda N2, deduplicação, bloqueios N1/N2 após as últimas alterações, persistência, diplomacia/Governo e suítes de cidades continuam pendentes de execução individual. A primeira bateria aérea terminou historicamente em 5/6; depois, o Test3 isolado passou 1/1 e identificou que a fixture sem `IdentidadeUnidade` deixava o pré-check assumir zero militares, mas o spawn Aéreo exigia o mínimo central de 2. A fixture atual declara a identidade, mantém energia válida após o tick e usa Governo isolado com dois alistáveis. XML: [diagnóstico Test3 aprovado](../ValidationResults/aircraft-purchase-test3-diagnostic-20261011-pass.xml). A bateria completa ainda não está aprovada; veja a atualização de 14:54 abaixo. Não houve nova campanha longa nem alteração visual de cidade nesta etapa.

Às 13:38 locais, a memória livre estava em 4,07 GiB e o Editor PID 19652 aparecia como `ready`, mas `editor_status`/`test_status` continuavam expirando. O `Editor.log` registrou 236 linhas de `MDB_READERS_FULL` e `Asset database transaction committed twice`, sem leitores obsoletos removidos. Nenhum teste novo foi iniciado nesse estado.

Às 14:23 locais, o mesmo log chegou a 396 ocorrências e o Editor 19652 passou a `Responding=False`. Os workers 5124 e 18040 ainda apontam para este projeto e ficaram praticamente ociosos durante uma amostra de 30 s. O Runner não gerou XML após 13:26; execução aérea instrumentada permanece pendente.

Às 14:25 locais, o Editor continuava sem responder, a memória livre havia caído para 2,31 GiB e o log chegara a 448 erros LMDB. O Test3 agora configura energia pós-tick e tripulação conhecida no fixture e preserva diagnóstico do estado original; ainda não houve importação ou execução PlayMode.

## Atualização da compra aérea e Runner — 2026-10-11 14:56 local

- A instância antiga foi substituída; o Editor atual é PID 6168, Unity 6000.2.15f1, respondendo, `ready`, sem compilação/domain reload e com Play Mode parado. A amostra local encontrou 3,76 GiB livres e não encontrou ocorrências `MDB_READERS_FULL`/`Asset database transaction committed twice` no log atual.
- O Test3 focal continua comprovado como aprovado 1/1, saldo 5.000→3.500 e entrega 0→1; a causa era o requisito de manpower Aéreo que a fixture anterior não modelava. Nenhum código de produção foi alterado nesse diagnóstico.
- Uma tentativa subsequente de filtrar a fixture completa por nome de classe terminou com apenas Test2 no resultado terminal do Runner, não com os seis casos. A tentativa seguinte de Test1 terminou em estado `cancelled` às 14:56, após `SaveModifiedSceneTask` lançar “This cannot be used during play mode”, `An unexpected error happened while running tests` e `Too many instant steps` durante `ExitPlayModeTask`. O Editor está `ready`, compilando=false e Play Mode parado; não existe XML novo correspondente. Não contar esse cancelamento como resultado nem como aprovação/falha de Test1.
- O Runner registrou Test2 1/1 às 14:58, mas com a fixture anterior, sem energia reafirmada e sem identidade/manpower isolados; não usar como prova de falta de dinheiro. A fixture foi ajustada para manter energia válida, declarar identidade Aérea e usar Governo isolado suficiente; ainda aguarda execução. O Test1 segue cancelado sem resultado terminal.
- O próximo passo é aguardar o aviso de memória liberada e então rodar Test2 corrigido, confirmar a seleção dos seis métodos e preservar XML de cada resultado terminal. Não iniciar enquanto a memória não for liberada.
- A causa e o resultado focal estão descritos em [AuditoriaRotaProducaoMilitar](AuditoriaRotaProducaoMilitar.md). Reposição pós-perda N2, cooldown/deduplicação, gates econômicos N1/N2, persistência, Governo/diplomacia e cidades continuam pendentes de validação individual.

## Desalinhamento de filtro no Runner — 2026-10-11 15:02 local

- A chamada assíncrona pedindo Test2 exato foi aceita, mas `test_status` devolveu primeiro Test1. Depois surgiu requisição Test5 e o resultado terminal foi Test6 1/1; seu XML está preservado em [resultado Test6](../ValidationResults/aircraft-purchase-test6-20261011-pass.xml). Não atribuir qualquer um desses resultados ao Test2.
- O Editor encerrou pronto/parado, sem requisição ativa e com 4,30 GiB livres. A fixture corrigida de insuficiência financeira ainda não foi validada; o problema passa a ser a correspondência entre pedido e resultado do Runner, além do erro anterior na saída de Play Mode.

## Test2 financeiro validado — 2026-10-11 15:03 local

- Reexecutei o filtro exato de Test2 após o Runner ficar sem requisição ativa. O terminal e o XML concordaram: `AircraftPurchasePlayModeTests.Test2_LocalAirportPurchase_InsufficientFunds_DoesNotDebitOrSpawn`, 1/1 aprovado, 0 falhas; artefato em [XML Test2](../ValidationResults/aircraft-purchase-test2-insufficient-funds-20261011-pass.xml).
- A fixture agora mantém energia operacional, identidade de aeronave e manpower suficiente, deixando saldo 500 contra preço 1.500 como o único gate inválido. O PASS confirma recusa sem débito e sem spawn.
- Test1, Test4 e Test5 ainda precisam de resultados terminais com nome correspondente. Test3 permanece aprovado isoladamente; Test6 tem XML 1/1 preservado.

## Reexecução dos gates aéreos — 2026-10-11 15:08 local

- Test1 sem energia, Test2 sem saldo, Test4 recusa de spawn sobre água e Test5 menu com aeroporto sem energia passaram individualmente 1/1 com as fixtures atualizadas; cada XML foi preservado e o nome terminal coincidiu com o filtro. Links e duração estão em [AuditoriaRotaProducaoMilitar](AuditoriaRotaProducaoMilitar.md).
- As fixtures de Test1/2/4/5 agora dão identidade Aérea e manpower suficiente, e reafirmam energia após o tick conforme o gate testado. Não houve mudanças em scripts de produção.
- Test6 será reexecutado com filtro exato. Ainda faltam validações aéreas de combustível zero e manpower insuficiente/rollback; isso mantém incompleta a validação econômica aérea mais ampla.

## Atualização da matriz de economia/produção — 2026-10-11 18:29 UTC

- O problema do BrainMaster ao escolher aeroporto foi reproduzido e corrigido na menor fronteira: aeroportos sem energia agora são pulados e a busca tenta o próximo aeroporto do mesmo time. Evidência e detalhes em [AuditoriaRotaProducaoMilitar](AuditoriaRotaProducaoMilitar.md).
- Os resultados terminais mais recentes foram: filtro aéreo 7/7, manpower/produção 12/12, combustível/petróleo 10/10. O XML aéreo lista Test1–Test6 e o caso BrainMaster; a classe recebeu depois `Test7_LocalAirportPurchase_InsufficientManpower_DoesNotDebitOrSpawn` e agora tem 8 métodos, portanto a classe completa ainda precisa ser reexecutada. Os XMLs estão no diretório externo `ValidationResults`: [aéreo 7/7](../../ValidationResults/aircraft-purchase-playmode-full-20261011-7of7.xml), [manpower 12/12](../../ValidationResults/ia03-production-manpower-playmode-20261011-after-aircraft-fix-pass.xml), [combustível 10/10](../../ValidationResults/fuel-national-inventory-playmode-20261011-after-aircraft-fix-pass.xml). Recompilação: concluída sem erro.
- As 132 fichas permanecem sem `ReceitaProducao` autorizada. O contrato legado ainda aceita a produção com seu preço monetário atual; nenhum material, energia de fabricação ou duração em dias foi criado.
- O contrato de perda de efetivo está implementado em `IdentidadeUnidade.OnDestroy` (militares ativos diminuem, mortos acumulados aumentam e a população nacional é recalculada), mas não localizei teste que afirme esses três resultados; acrescentar um teste focal após a liberação de memória.
- **Ainda pendente nesta continuação:** regressão N2 sustentada com alinhamento, múltiplos tiros e destruição; EditMode de deduplicação do `IAAutoProductionRegistry`; cadeia de perda e reposição econômica após N2; validações específicas de compra aérea com combustível zero e manpower insuficiente; suites atuais de persistência, cidade/população, Governo e diplomacia.
- **Editor:** na última checagem que motivou a pausa, consultas expiravam em 10 s, havia 1,53 GiB livres e dois AssetImportWorkers ativos. O usuário está liberando memória e avisará quando o Editor voltar a responder. Nenhum teste foi iniciado depois daquele estado. Campanha A/B/C/D de 90 minutos permanece fora desta etapa.

## Auditoria estática das rotas e petróleo — 2026-10-11

- **Jogador:** `GerenteDeJogo.TentarGastarDinheiro` usa `GerenciadorRecursos`; `SistemaGovernoMundial.SincronizarJogador` espelha esse caixa no país do jogador. Terrestre valida efetivo e instalação operacional antes de debitar e insere ordens na fila; menu aéreo e naval debita por unidade e estorna quando o produtor recusa. Aeronaves podem ir ao hangar se não há vaga de pátio; o estaleiro mantém produção em fila.
- **BrainMaster:** `IA_BrainMaster.TrySpend` chama `SistemaGovernoMundial.TentarPagar`, depois sincroniza `Credits` como espelho de `pais.saldo`. `IA_BackendBridge` cobra o total antes da execução e devolve o valor se a produção falha ou é parcial. O teste PlayMode do orçamento confirma o espelho e a devolução; não é uma carteira separada.
- **Outras IAs:** NovaIA, Soberana, IA01 e IA02 usam `IA_NationalTreasury`; DEUSA publica comandos na fila/Backend do BrainMaster, então reutiliza o mesmo caminho de produção. Há um parâmetro único opcional de vantagem inicial em `DeusaConfig`; o código o credita ao saldo nacional uma vez, mas não encontrei asset serializado que habilite esse bônus. `CerebroIA`/`AnalistaExecutivo` ainda mantém caminho legado local em `SampleScene`, fora de `EditorBuildSettings` atual; permanece fora do caminho de partida validado.
- **Manpower:** a rota central computa mínimos para infantaria (1), veículo (1), naval (4) e aéreo (2), ou usa `IdentidadeUnidade.militaresConsumidos` positivo. Em `OnDestroy`, baixa militares ativos, soma a mortos e recalcula população; a regra ainda não tem teste específico de destruição/baixas.
- **Petróleo:** `PlataformaOffshore` produz para tanque interno com limite de capacidade; não credita automaticamente o tesouro/estoque nacional. `NavioPetroleiro` transfere óleo bruto ao estoque nacional no pier, e `SistemaIndustrialNacional` refina para diesel/combustíveis por domínio. A construção da plataforma pelo BrainMaster define `OwnerTeamId` e notifica o WorldState; percepção e contagem de plataforma existem. A produção/capacidade atuais da ficha serializada são dados existentes e não foram alterados nem tratados como balanceamento aprovado.
- **Reserva de combustível:** o gestor de prontidão consulta `IntegracaoMercadoIndustrial.ReservaMinimaPadrao`, atualmente 1.500 para combustíveis industriais. `IA_NationalDirectors` contém outro limite local igual a 1.500 para solicitar compras. São valores hoje iguais; consolidar a consulta no helper existente e executar a regressão de demanda/readiness quando o Editor estiver estável.
- **Produção sem spam:** `IAAutoProductionRegistry` bloqueia reserva equivalente enquanto pedido está reservado/enfileirado/em construção; a fila de comandos também rejeita duplicatas e põe `CooldownSeconds` após completar/falhar. Existem testes do registro de produção para ciclos repetidos e ciclo de vida, mas não encontrei teste direto do cooldown da fila de comandos; validar esse contrato antes de concluir a fase de deduplicação.
