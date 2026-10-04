# IA03 — Auditoria auxiliar do núcleo

**Data:** 04/10/2026  
**Escopo:** leitura estática do projeto atual, com foco em resultado de combate, inicialização, candidatos a travamento, configuração manual de Creatys, custo da IA03 e cessão territorial permanente.

## Limites desta auditoria

Esta auditoria foi somente leitura. Não abri outra instância da Unity, não executei Play Mode, testes, build, comandos de pacote ou profiling. Portanto, os achados abaixo distinguem fatos no código/configuração de hipóteses ainda não reproduzidas em runtime. Não há comparação objetiva de FPS, CPU, frame time ou GC Alloc.

O projeto já estava com alterações locais antes desta auditoria, inclusive nas cenas `GlobalMapRTS.unity` e `IA03_WarValidation.unity`, em configurações de pacotes e em `Docs/IA03_ESTRATEGA.md`, além de arquivos recuperados. Durante a auditoria surgiram outras alterações no worktree, incluindo `AISovereignBootstrapper.cs`, `AISovereignTerritoryTests.cs` e um prefab naval. Não alterei esses arquivos; a única alteração feita por mim foi este novo relatório.

## Resumo executivo

| Prioridade | Achado | Confiança | Consequência |
| --- | --- | --- | --- |
| Alta | Não há produtor ligado a `RegistrarResultadoCombate` da IA03. | Alta | O total de batalhas fica em zero; a regra configurada de mínimo de batalhas nunca é alcançada no fluxo atual. |
| Alta | O mapa político padrão não tem terra capturável pertencente aos países. | Alta | A cessão territorial permanente da IA03 não consegue selecionar regiões nesse mapa. |
| Alta para o teste de missões | Os 26 `SLOT_MANUAL_*` da cena são objetos vazios, sem componente `CreatyEstrategico`. | Alta | A cena não consegue iniciar uma missão que dependa de um ponto Creaty registrado. |
| Alto risco de desempenho fora da cena de validação | O bootstrap soberano cria controladores em cenas que não são menu; o adapter pode buscar todos os `MonoBehaviour` em todo frame. | Alta para a condição no código; impacto não medido | Em países sem `CerebroIA` encontrado, a lista permanece vazia e a busca global pode se repetir por frame. Uma alteração que apareceu no worktree agora pula esse bootstrap na cena IA03 de validação no Editor. |
| Médio | O resumo de runtime da IA soberana é reconstruído como string a cada frame. | Alta | Alocação recorrente de strings; custo/GC não medido. |
| Médio | Um ponto seguinte de rota não revalida todos os filtros do Creaty contra a missão. | Alta no código; depende do uso de rotas | Uma rota manual incompatível pode aceitar o grupo reservado. |
| Médio | Desativar o campo `ativo` da IA03 não equivale a desativar o componente. | Alta no código | `Tick` retorna antes de finalizar a missão; reservas e ordens podem continuar presas. |

**Conclusão sobre o travamento:** foi encontrado um candidato concreto e forte fora do caminho principal da IA03: o bootstrap da IA soberana e o adapter legado. O arquivo atual do bootstrap contém uma mitigação estreita para `IA03_WarValidation` no Editor; ela não cobre `GlobalMapRTS`, outras cenas de gameplay, nem controladores soberanos persistentes que já existissem antes da troca de cena. Não foi possível afirmar que esse risco causou o travamento observado, porque a Unity não foi executada nem houve captura do Profiler nesta etapa. A telemetria disponível no `Editor.log` também é histórica, não uma reprodução da sessão atual.

## 1. Resultado de combate e eventos consumidos pela IA03

### Eventos existentes reaproveitados

| Informação | Produtor existente | Consumo da IA03 | Limite observado |
| --- | --- | --- | --- |
| Dano | `SistemaDeDanos.OnDanoGlobal` (`Assets/scripts/SistemaDeDanos.cs:129`) | `AoReceberDanoGlobal` (`IA03EstrategaNacional.cs:198-200, 1791-1842`) | Registra dano estrutural e efeitos estratégicos; não é resultado de batalha. |
| Morte/destruição | `SistemaDeDanos.OnMorteGlobal` (`SistemaDeDanos.cs:477`) | `AoMorrerUnidade` (`IA03EstrategaNacional.cs:200-201, 1610-1642`) | Serve para baixa, perda e confirmação de alvo destruído, conforme identidade e missão. |
| Registro de combate | `CartaCombateRegistro.EventoRegistrado` (`UI/CartaCombateRegistro.cs:39, 223-232`) | `AoRegistrarCombate` (`IA03EstrategaNacional.cs:192-195, 1743-1770`) | Registra lançamento, encerramento de míssil e unidade destruída. Encerramento de míssil declara que o resultado final não foi informado. |
| Mudança de proprietário territorial | `GerenteDeTerritorio.OnTerritoryOwnerChanged` (`GerenteDeTerritorio.cs:444, 460`) | `AoMudarDonoTerritorial` (`IA03EstrategaNacional.cs:1889-1925`) | O evento não informa a causa. Uma cessão diplomática também pode entrar como objetivo capturado. |
| Ordem concluída | `OrquestradorGlobalOrdens.OrdemConcluida` | Assinado pela IA03 durante sua habilitação | Permite reagir a conclusão de ordem; não substitui um resultado de combate. |

### Lacuna confirmada: vitórias/derrotas

`IA03RelatorioConflito.RegistrarResultadoCombate(bool)` é o único método que incrementa `BatalhasVencidas` ou `BatalhasPerdidas` (`IA03RelatorioConflito.cs:133-149`). A busca de referências no código e testes encontrou apenas o wrapper `IA03EstrategaNacional.RegistrarResultadoCombate` (`IA03EstrategaNacional.cs:362-365`), sem produtor que o chame. Assim, `TotalDeBatalhas` permanece zero no fluxo real auditado e `AtingiuDominioMinimo` nunca consegue satisfazer o mínimo (`IA03RelatorioConflito.cs:43-53, 145-149`).

O projeto possui resultado de **partida RTS** (`Victory`, `Defeat`, `Draw`) em `RTSGameSession`, mas isso não representa vitória/derrota de cada batalha. O chamador localizado é `SistemaFimDeJogo`, que avalia objetivos/capitais e reporta vitória ou derrota; não foi encontrado produtor de empate nem assinante de `OnMatchFinished`. Não encontrei evento de escaramuça com início, fim, vencedor, perdedor e empate que possa ser ligado diretamente sem definir primeiro o significado de “batalha”.

**Efeito prático:** baixas, dano e objetivos alimentam parte da pontuação e do relatório, mas a regra percentual de domínio por batalhas e o mínimo configurado não estão fechados.

## 2. Inicialização e candidatos a travamento

### Busca global potencialmente executada todo frame

1. `IA_ModeSwitch` assume `BrainMaster` como modo padrão e habilita as pilhas BrainMaster/DEUSA/Sovereign em cenas fora da exceção da campanha (`Assets/scripts/IA/NovaIA/IA_ModeSwitch.cs:13-30, 48-57, 159-181`). Ao aplicar o modo, percorre um array de todos os `MonoBehaviour`; isso é uma varredura de inicialização/transição, não um loop por frame.
2. `RTSRuntimeBootstrap` chama `SistemaGovernoMundial.GarantirInstancia()` antes da cena e estabelece os serviços centrais (`Assets/scripts/RTS/RTSRuntimeBootstrap.cs:8-29`). Se o governo inicializa sem dados carregados, ele cria cinco países padrão, times 1–5 (`SistemaGovernoMundial.cs:63-64, 114-121`).
3. `AISovereignBootstrapper` sincroniza a cada cinco segundos e cria um `AISovereignController` para cada país acima do time do jogador ainda sem controlador (`Assets/scripts/IA/Sovereign/AISovereignBootstrapper.cs:26-63`). Na versão atual do worktree, `ShouldSkipBootstrapForScene` interrompe essa criação quando o Editor está rodando `Assets/Tests/PlayMode/IA03_WarValidation.unity` (`AISovereignBootstrapper.cs:10-18, 49-56`). A condição só vale para essa cena no Editor; em outras cenas e em player build ela não pula a criação.
4. `AISovereignController.Update` chama `_legacyAdapter.Apply(_authorityActive)` a cada frame (`AISovereignController.cs:103-121`). O adapter chama `IA_UnitySearch.FindAll<MonoBehaviour>()` quando a autoridade está ativa e `_disabled.Count == 0` (`AILegacyObserverAdapter.cs:20-34`). `IA_UnitySearch.FindAll` usa `Object.FindObjectsByType<T>(FindObjectsSortMode.None)` na versão atual (`IA_SharedRuntimeSupport.cs:440-445`).
5. O adapter só deixa de repetir a busca depois de desativar pelo menos um tipo legado correspondente; o tipo legado reconhecido é `CerebroIA` (`IA_SharedRuntimeSupport.cs:74-82`). Se não houver `CerebroIA` da equipe, a lista continua vazia e a busca do array global pode ocorrer no próximo frame.

Na cena de validação, a YAML contém dois pares BrainMaster/IA03, dos times 1 e 3, e nenhuma ocorrência de `CerebroIA` (`IA03_WarValidation.unity:1010, 1074, 1748, 1812`). Com o gate recém-presente no worktree, o bootstrap não deve criar ali novos controladores soberanos ao iniciar Play diretamente nessa cena no Editor. Em `GlobalMapRTS` ou outras cenas de gameplay, com governo padrão e time do jogador 1, ele ainda pode criar controladores para 2–5. O controlador do time 3 tem prioridade 220, abaixo da prioridade 300 do BrainMaster, então não há evidência de que tome a autoridade desse time; os demais podem ficar ativos. **A repetição do scan depende da ausência de `CerebroIA` correspondente em runtime**, então o custo exato precisa ser confirmado no Profiler. A nova alteração tem casos de teste adicionados em `AISovereignTerritoryTests.cs`, mas eles não foram executados.

Também há uma alocação de string em `runtimeSummary = BuildSummary()` dentro do `Update`, sem intervalo (`AISovereignController.cs:171, 990-1010`). Se houver vários controladores, essa formatação se repete por frame. É mais um candidato a GC recorrente, ainda não quantificado.

### Scheduler da IA03

- IA03 não possui `Update` próprio; é registrada como módulo do BrainMaster quando ativa e com perfil configurado (`IA_BrainMaster.cs:1033-1058`).
- Nos perfis da cena de validação, o intervalo de análise é 45 s; com missão ativa, IA03 reduz o intervalo do módulo para 5 s (`IA03EstrategaNacional.cs:103-105`). O atraso inicial usa IDs de equipe para distribuir até 15 fases de 0 a 7 s, em passos de 0,5 s (`IA03EstrategaNacional.cs:118-121`).
- O scheduler é uma instância por BrainMaster; o coordenador global compartilha o token pesado/orçamento entre cérebros, mas IA03 não é classificada como módulo pesado. Portanto, o escalonamento inicial ajuda a evitar alinhamento dos 15 cérebros, mas não prova isoladamente ausência de picos.
- IA03 implementa `IIAUpdateModule`, não `IIAIncrementalUpdateModule`. O `GlobalFrameBudgetMs` do scheduler é aplicado a módulos incrementais; um `Tick` de IA03 é síncrono e não pode ser interrompido no meio (`IA_PerformanceScheduler.cs:124-145`). O budget declarado serve para registrar excesso depois da chamada, não para limitar sua duração.
- No caminho IA03 pesquisado não há busca global nem LINQ frequente. Há listas reutilizadas; a cópia `new List<GameObject>(unidades)` acontece quando uma ordem é emitida. O fallback de `IA_WorldState.FindObjectsByType<IdentidadeUnidade>` só entra quando o registro está vazio e tem intervalo de 20 s (`IA_WorldState.cs:890-908`).

## 3. Cena e configuração manual de Creatys

`IA03_WarValidation.unity` tem 26 GameObjects nomeados `SLOT_MANUAL_*`, organizados sob `Creatys_Manuais`, mas são placeholders sem componente `CreatyEstrategico`. O GUID do componente não aparece na cena. A configuração atual inclui dois BrainMasters/IA03, equipes 1 e 3, perfis e seis assets de missão por país; o bootstrap diplomático começa em paz. Nenhum Creaty é criado automaticamente, conforme a configuração manual pretendida.

**Resultado:** a cena permite verificar carregamento do cérebro e controles do Inspector, mas não validar despacho de ordens, reserva de ponto, chegada ou término de missão até Creatys reais serem colocados manualmente.

Ao configurar esses pontos, conferir `id` único, owner, alvo, tipo, nível, domínio, prioridade, limite de unidades, cooldown, filtros de países e rota. O registro valida critérios na seleção inicial (`RegistroCreatysEstrategicos.cs:162`; `IA03EstrategaNacional.cs:987`). Porém, ao avançar para `ProximoPonto`, o fluxo reserva e envia a ordem sem repetir todos os filtros de tipo/domínio/nível (`IA03EstrategaNacional.cs:1466`). O validador de Editor confere IDs/owner e alguns dados de alvo/superfície, mas não todas as combinações missão-rota-capacidade (`Assets/Editor/IA03/ValidarCreatysEstrategicos.cs:74`).

## 4. Missões, reservas e escalada

- O processador de missão roda pelo intervalo do BrainMaster, não por `Update` da IA03. Sucesso, fracasso, expiração e cancelamento passam por encerramento que cancela ordens pertencentes à IA03 e libera a reserva do Creaty.
- Há uma exceção: editar o booleano serializado `ativo` para `false` não chama `OnDisable`. Como `Tick` retorna imediatamente quando `!ativo` (`IA03EstrategaNacional.cs:227-231`), o processador pode deixar de chegar à finalização normal. Desativar o componente aciona `OnDisable` e `EncerrarMissao`, mas isso é outro controle. O risco é relevante para o painel de debug/Inspector.
- Os assets de missão bloqueiam invasão em N4 e ainda bloqueiam operações futuras como porta-aviões, invasão anfíbia e visita presidencial. Essa restrição corresponde ao escopo de não expandir módulos agora.
- A cena não fornece evidência runtime de transição N4→N3→N2→N1. O Editor oferece botões de debug, mas a presença de botões e assets não comprova execução, envio, conclusão ou liberação em jogo.

## 5. Cessão territorial permanente

### Geração da proposta

`GestorDiplomaciaIA03.TentarProporCessaoTerritorial` seleciona apenas regiões terrestres capturáveis cujo proprietário é o país pagador (`IA03/GestorDiplomaciaIA03.cs:150-183`). O recurso padrão `Assets/Resources/MapaTerritorialInicial.asset` marca as terras dos países como não capturáveis; a única região `capturable: 1` é `ilha-central`, neutra e com owner 0 (`MapaTerritorialInicial.asset:17-214, 238-243`). `GerenteDeTerritorio` carrega esse recurso na ausência de override (`GerenteDeTerritorio.cs:201-210`). Portanto, no mapa padrão, a seleção retorna zero candidatos e a proposta permanente não é gerada.

O teste de cessão usa região sintética capturável com proprietário definido (`Assets/Tests/EditMode/IA03AcordosDiplomaticosEditModeTests.cs:119+`); ele não valida o recurso político padrão.

### Aceite, UI, estado territorial e economia

Se uma proposta válida existir, o caminho de execução está ligado: `SistemaGovernoMundial.ResolverProposta` executa a transferência e marca a proposta executada somente se o executor retornar sucesso (`SistemaGovernoMundial.cs:1396-1447, 2093-2134`). A transferência chama `GerenteDeTerritorio.TentarCapturarTerritorio`, que atualiza o estado efetivo e publica `OnTerritoryOwnerChanged` (`GerenteDeTerritorio.cs:448-469`). A UI de governo exibe a proposta e oferece aceite (`Menus/MenuGoverno.cs:2232-2289`). Save/load persiste propostas, notícias e proprietários territoriais (`SistemaSaveGame.cs:1234-1258, 1439-1479`).

Pontos ainda sem comprovação ou inconsistentes:

- Economia imobiliária agrega ativos por `teamId` de identidades/estruturas, não pelo dono da região. Não foi encontrado recálculo/transferência de estruturas, emprego ou produção associado à cessão (`SistemaEconomiaImoveis.cs:84-127, 667-679`).
- Construção manual consulta o novo proprietário territorial, mas `ConstruirEstruturaIA` instancia diretamente; não foi confirmada validação equivalente em todos os caminhos de construção da IA (`Construtor.cs:410-436, 1476-1515`).
- Ao carregar o proprietário salvo, a restauração não emite `OnTerritoryOwnerChanged` (`GerenteDeTerritorio.cs:472-529`). Consultas de UI leem o estado restaurado, mas consumidores que dependam do evento não recebem replay.
- A IA03 incrementa `ObjetivosCapturados` ao observar owner mudar do alvo para o próprio time, mesmo se a causa tiver sido cessão diplomática (`IA03EstrategaNacional.cs:1889-1907`). O evento atual não carrega motivo/causa.

## 6. Evidência do Editor e testes

- Não foram executados testes ou Play Mode nesta auditoria. Há testes EditMode existentes para avaliação, reserva/liberação, escalada e cessão sintética, mas eles não substituem a validação dos produtores de runtime nem do mapa padrão.
- `ProjectSettings/EditorBuildSettings.asset` contém `Assets/Scenes/GlobalMapRTS.unity`; a cena `IA03_WarValidation` não está na lista de cenas de build. Ela precisa ser aberta/testada pelo Editor ou incluída por uma mudança explícita posterior.
- O `Editor.log` localizado (`C:\Users\Mathe\AppData\Local\Unity\Editor\Editor.log`) tinha `LastWriteTime` 04/10/2026 12:04:26 e cerca de 1,9 MB quando lido. O histórico registra `package_add` com timeout de 60 s e operações de Tundra/reload de assemblies; há build de Tundra concluído depois do timeout. Como o arquivo não é da sessão recém-reproduzida, isso não prova a causa do travamento atual.
- Há scripts de inicialização automática do Editor que merecem verificação separada antes de abrir/rodar testes: `RunAircraftTestsInEditor`, `TutorialCoastSceneBuilder` e `CodexPlaceCityInCena19`. Os últimos podem iniciar testes, alterar cenas ou entrar em Play dependendo dos flags `EditorPrefs`. Esses valores não foram lidos e nenhum desses scripts foi executado nesta auditoria.

## Próximos passos recomendados

1. Em uma sessão controlada, medir no Profiler a contagem e o custo de `FindObjectsByType<MonoBehaviour>`, GC Alloc e `AISovereignController.Update` na cena de validação; comparar com o bootstrap soberano desativado apenas para teste controlado, sem alterar o mapa principal.
2. Inspecionar os flags `EditorPrefs` dos inicializadores automáticos antes de abrir um fluxo de teste que possa editar/salvar cenas.
3. Adicionar manualmente Creatys verdadeiros à cena de validação e percorrer Paz, N4, N3, N2 e N1 com poucas unidades; registrar logs e resultado de cada missão.
4. Definir qual sistema existente representa uma “batalha” (escaramuça, missão ou partida) e conectar um produtor autoritativo a `RegistrarResultadoCombate`; não inferir vitória só por uma unidade destruída.
5. Configurar ao menos uma região terrestre capturável de propriedade de país em um cenário controlado, então verificar aceite, UI, mapa, construção, economia e save/load da cessão.
6. Só após os passos acima, executar o cenário de 15 cérebros e registrar métricas objetivas de CPU, frame time, GC e frequência de picos.

**Recomendação de escopo:** fechar primeiro os achados de resultado de batalha, buscas soberanas por frame, Creatys da cena e propriedade capturável no cenário de teste. Não iniciar porta-aviões, invasão anfíbia ou visitas presidenciais antes dessa validação.

## === MENSAGEM PARA COLAR NO CHAT PRINCIPAL ===

Auditoria auxiliar estática concluída e salva em `Docs/IA03_AUDITORIA_AUXILIAR.md`. Não alterei código nem executei Unity/testes. Confirmei três lacunas funcionais: não há produtor de resultado de batalha para IA03; o mapa padrão não oferece região terrestre capturável de país para cessão permanente; e os 26 slots da cena IA03 são placeholders sem `CreatyEstrategico`. O risco de travamento mais forte está no adapter da IA soberana, que pode chamar `FindObjectsByType<MonoBehaviour>` e gerar strings de resumo todo frame; a alteração atualmente presente no worktree pula a criação de novos controladores apenas na cena `IA03_WarValidation` dentro do Editor, mas isso não cobre outras cenas nem mede custo. A cessão aceita, quando configurada, atualiza o dono territorial, porém economia e replay do evento em load não foram demonstrados. A próxima etapa é validar com Profiler e Creatys reais num cenário controlado, sem ampliar escopo.
