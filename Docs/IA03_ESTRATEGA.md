# IA03 — Estratega Nacional

IA03 é uma camada estratégica opcional sobre `IA_BrainMaster`. Ela não substitui IA01 nem cria controladores de movimento, mercado ou diplomacia paralelos. Sem `IA03EstrategaNacional` anexado e configurado, BrainMaster conserva o fluxo atual.

## Configuração na Unity

1. Crie um perfil em **Assets > Create > Hegemonia > IA03 > Perfil de País** e configure país, presidente, preferências, reserva defensiva e limiares de negociação.
2. Crie missões em **Assets > Create > Hegemonia > IA03 > Missão Estratégica**. As missões definem faixa de crise, domínio, efetivo, Creatys aceitos, objetivo, prazo e composição naval/presidencial necessária.
3. Anexe `IA03EstrategaNacional` ao mesmo GameObject que contém `IA_BrainMaster`; atribua o perfil e a lista de missões. O módulo entra no `IA_PerformanceScheduler` somente quando existe e está habilitado com perfil atribuído.
4. Posicione os componentes `CreatyEstrategico` manualmente nas cenas ou prefabs. Defina ID único, país proprietário, nível, domínio, tipo, peso e rota opcional em `ProximoPonto`.
5. Execute **Hegemonia > IA03 > Validar Creatys Estratégicos** para receber avisos de IDs, configuração e superfície. A ferramenta não muda posições nem salva assets.

O alvo pode ser atribuído diretamente no perfil da IA. Se ficar em zero, IA03 usa `rivalTeamId` do governo quando disponível. Missões `Atacar` priorizam observações inimigas já fornecidas pelo `IA_WorldState`; quando não houver alvo observado compatível, usam a posição do Creaty.

## Integrações

- Decisões usam `IIAUpdateModule` e o scheduler de BrainMaster; não existe `Update` próprio nem busca global feita pela IA03. Os atrasos iniciais variam 0,5 s por teamId (até 7 s para 15 países). O `IA_WorldState` reutiliza seu registro central e só usa busca global como fallback quando o registro está vazio, limitada a uma tentativa por 20 s.
- As listas de forças vêm do `IA_WorldState` e do registro central já usado por BrainMaster. Ordens passam pela `IA_CommandQueue` existente e são ignoradas em `ShadowReadOnly`.
- Pesos nacionais e metas de força alimentam os diretores já existentes de economia, mercado, diplomacia, produção, marinha e aviação. O BrainMaster recalcula seu plano a cada 4 s; depois desse cálculo, reaplica as metas militares do perfil IA03. IA03 não altera saldos nem cria recursos.
- `GestorEconomiaIA03` lê saldo, comida, petróleo, energia e déficits do governo e ajusta pesos dos diretores de BrainMaster para proteger reservas e estoques essenciais.
- Relações, notícias e propostas são lidas ou registradas em `SistemaGovernoMundial`. O adaptador IA03 envia propostas de cessar-fogo, que o serviço existente pode aceitar ou recusar.
- Se o relatório apontar vantagem militar, IA03 pode propor uma indenização e pedir regiões terrestres configuradas como concedíveis. O serviço central só transfere saldo ou posse depois da aceitação.
- Perdas confirmadas usam `CartaCombateRegistro.EventoRegistrado`; uma unidade presidencial marcada por `IA03MarcaPresidencial` emite um evento a partir da notificação de morte do sistema de dano, sem consulta periódica.
- Dano a uma estrutura nacional é recebido pelo evento de dano existente. Se houver missão `DefesaDeObjetivo` compatível configurada, ela é colocada à frente da fila sem procurar unidades ou prédios na cena.
- Creatys registram-se no ciclo de vida do componente. O registro bloqueia IDs duplicados até que reste apenas uma instância.
- Missões pré-configuradas entram numa fila por prioridade. Sistemas de incidentes podem chamar `SolicitarMissaoEstrategica` para colocar uma missão urgente à frente da fila.
- Na guerra total, o efetivo máximo de uma missão é configurável em `PerfilPaisSO.ContingenteMaximoPorMissaoN1` (0,5 por padrão), além do teto geral de mobilização (0,9 por padrão) e da reserva defensiva. O restante não é anexado a esse grupo de ataque.

## Relatórios e escalada

Os relatórios são emitidos durante uma crise no intervalo configurável (5 minutos por padrão). O relatório combina o total de forças do cache, inimigos conhecidos, estoques nacionais e eventos acumulados de perdas/destruições. Limiares de baixas, custo de reposição conhecido e pressão sobre reservas podem iniciar uma proposta de cessar-fogo após 25 minutos em conflito limitado; uma crise que continue pode escalar conforme agressividade. A condição baseada em vitórias e domínio depende de um resultado autoritativo de batalha, que o jogo ainda não publica. A avaliação de uma hora usa a pontuação acumulada para justificar uma proposta de cessar-fogo ou continuar a guerra.

Destruições confirmadas vêm de `CartaCombateRegistro`, alimentado pela notificação de morte de `SistemaDeDanos`. Para unidades e estruturas com preço de reposição conhecido por `IA_ConstructionMetadata` ou por correspondência única no catálogo, o relatório acumula esse preço como estimativa de prejuízo; valores ambíguos ou ausentes ficam desconhecidos. Essa estimativa não é o custo econômico total do conflito: interrupção de produção, recursos e territórios ainda não têm valoração monetária conectada. Capturas e perdas de regiões entre os dois países são atualizadas por `GerenteDeTerritorio.OnTerritoryOwnerChanged`. Dano estrutural recebido por `SistemaDeDanos.OnDanoGlobal` permanece em pontos de vida e não é convertido artificialmente em dinheiro. O jogo ainda não publica um resultado autoritativo de fim de batalha para alimentar `RegistrarResultadoCombate`.

## Condições de missão

O ciclo da missão avalia as condições do `MissaoEstrategicaSO`: chegada ao Creaty; permanência contínua pelo `TempoMinimoDePermanenciaSegundos`; destruição confirmada do alvo observado; sobrevivência de todo o grupo original até o prazo; confirmação externa; e captura da região política na posição do Creaty. A condição de fracasso usa a contraparte configurada (perda de unidade, saída da zona, território perdido ou confirmação externa), e qualquer prazo máximo encerra a missão que não atingiu sucesso.

Sistemas externos podem concluir missões configuradas para confirmação externa chamando `RegistrarResultadoMissaoExterno(idMissao, sucesso, motivo)` na IA03. IDs de missão devem ser únicos por perfil.

## Termos territoriais

As propostas diplomáticas aceitas podem ceder regiões terrestres permanentemente ou por um número de dias de jogo, e criar uma zona desmilitarizada temporária. A cessão temporária devolve cada região ao país original no vencimento apenas se ainda estiver sob o controle do beneficiário. Os termos e datas finais usam o estado de propostas já gravado pelo sistema de save, preservando saves antigos.

Ordens de movimento, patrulha e seguimento de infantaria e veículos das partes são recusadas quando seu destino entra em uma zona desmilitarizada. Essa aplicação não evacua automaticamente forças que já estavam lá quando o tratado foi aceito. A IA03 oferece cessão temporária de 30 dias no conflito limitado com pontuação de guerra a partir de 80, zona desmilitarizada de 45 dias na faixa 100–249 e mantém cessão permanente para pontuações maiores.

## Limites atuais de integração

As cessões permanentes e temporárias continuam limitadas a regiões terrestres configuradas como capturáveis. Uma desmilitarização se aplica às regiões terrestres incluídas na proposta. A exigência de 100% requer pontuação alta, quantidade mínima de batalhas e domínio registrado de pelo menos 90%. As missões com porta-aviões, transporte anfíbio ou presidente selecionam e enviam grupos de unidades reais pelo comando existente; embarque/desembarque de passageiros e uma visita presidencial completa dependem dos fluxos específicos desses sistemas e não são acionados por este módulo.

Vitórias por batalha e valoração monetária completa do prejuízo (além dos custos de reposição conhecidos de unidades e estruturas destruídas) ainda dependem de integrações autoritativas específicas. A retirada automática de unidades que já ocupam uma zona desmilitarizada também não está conectada. Os indicadores não presumem esses resultados.

O BrainMaster mantém apenas uma missão IA03 ativa por estrategista; o teto N1 impede concentrar 90% do exército numa única missão, mas ainda não distribui grupos simultâneos de ataque/defesa por vários Creatys. A defesa restante e a reposição continuam sob os diretores existentes do BrainMaster. O jogo também não publica início/fim de batalha com vencedor; não se inferem vitórias a partir de baixas.

## Validação técnica da etapa 2

Na validação de 2026-10-03, Unity 6000.2.15f1 compilou sem erros e passaram 30 testes direcionados: 23 de missão IA03, 1 PlayMode de morte/eventos, 2 de performance e 4 de acordos diplomáticos/territoriais. `MissaoUrgenteFicaNoTopoEPreservaMissaoInterrompidaSemDuplicar` verifica prioridade urgente, retenção da missão interrompida e deduplicação. Os quatro testes de acordos cobrem cessão permanente/temporária, expiração da concessão e vigência/expiração da desmilitarização. O Test Runner também foi observado demorando e o Pipeline registrou timeouts de operação da thread principal após 60 s; esses registros não foram falhas de teste.

O microbenchmark sintético reproduz o `PhaseOffsetSeconds` do BrainMaster e o atraso de registro da IA03 (`0.187 s + DelayInicialEscalonado`). Na execução atual, 1.800 quadros simulados com 15 schedulers vazios mediram média 0,000270 ms/quadro, p95 0,000300 ms e pico 0,047100 ms. Com 15 IA03 em N1, sem unidades ou campanha, mediram média 0,001149 ms/quadro, p95 0,002400 ms, pico 0,064400 ms, pico de módulo 0,035200 ms e 84 execuções; nenhum módulo excedeu o budget. Isso mede apenas os schedulers e estrategistas IA03 num harness isolado: não inclui os demais módulos do BrainMaster, FPS/CPU de uma guerra real ou guerra longa.

`IA_BrainMaster.Update` agora reutiliza, por cérebro, os objetos de snapshot do governor, decisão de batalha e orçamento de engajamento através de overloads que atualizam a instância recebida. O teste de identidade confirma que as referências são mantidas e os valores são atualizados. Isso remove essas três criações por frame do caminho do BrainMaster; não prova zero alocação em todo o BrainMaster. O contador `GC.GetAllocatedBytesForCurrentThread` do harness continua inválido (reportou zero mesmo em um probe com `new` explícito), então GC Alloc/frame precisa ser medido no Profiler do Unity.

### Investigação do travamento no Unity

O `No cameras rendering` não se reproduziu na cena principal: durante Play Mode, `/GlobalMap_World/Main Camera` estava ativa e a Game View renderizou o mapa e a interface. A mensagem apareceu na cena temporária `InitTestScene` do Test Runner, que não tem câmera. O Console ficou com zero erros ativos de compilação/runtime após a execução; os três erros históricos eram `Failed to handle /api/exec request: Main thread operation timed out after 60000ms`, emitidos pelo Pipeline durante o Test Runner, não exceções da partida.

Fiz A/B temporário, em Play Mode, desligando um sistema por vez e restaurando ambos antes de sair: a mediana amostrada do Main Thread ficou em 5,23 ms com tudo ligado, 5,17 ms sem `GlobalTerrainStreamer` e 5,46 ms sem o `IA01Manager` ocioso (oito amostras por condição). Não houve redução consistente. A cena tinha zero controllers IA01, zero `IA_BrainMaster` e zero IA03. Dez chamadas isoladas do mesmo `FindObjectsByType<IA01Controller>` usado por `BindSceneControllers` levaram 1,346 ms no total (0,1346 ms/call), resultado baixo demais para explicar sozinho o travamento observado. A captura não substitui uma partida longa ou o Profiler por frame.

O streamer tinha cinco tiles locais ativos/desejados e reportou 1,471 s de trabalho para a última geração de tile. As medições A/B ocorreram depois do aquecimento; um pico de geração durante o carregamento inicial continua sem medição isolada. Também apareceram avisos separados de `NavMeshAgent` sem NavMesh válido em `F200` e de vizinhos de Terrain com resoluções de heightmap diferentes; são riscos de navegação/continuidade visual, mas não produziram o `No cameras rendering` nem um erro de compilação neste teste.

O arquivo removido `GlobalMap_CoarseTerrain.asset` não é referenciado pela cena carregada: o Terrain principal usa `GlobalMap_CoarseTerrain_VisualGate.asset`, e o builder também aponta para essa variante. O mapa renderizou sem restaurar o arquivo removido. A cena principal ainda precisa de A/B com dois países e, depois, 15 cérebros IA03 para fechar o perfil real de campanha.

### Confirmação adicional do travamento (03/10/2026)

Na última inspeção via Unity MCP, o Editor estava `ready`, parado fora de Play Mode, sem compilação em andamento; `GlobalMapRTS` era a única cena aberta e estava `isDirty=false`. O estado real do Console era `compilationFailed=false` e zero erros ativos. O buffer ainda retinha sete erros históricos: timeouts do Pipeline após 60 s, falhas de operações MCP tentadas durante Play Mode e um aviso antigo de limpeza de `GameDifficultyManager`; eles não são erros ativos da partida. Havia quatro avisos ativos, incluindo a auditoria de conteúdo, IA01 sem controllers no cenário de teste e vizinhos de Terrain com resolução diferente.

O log do Editor confirma uma recarga excepcionalmente longa: `Loaded All Assemblies` levou 78,966 s e `Domain Reload Profiling` marcou 91,244 s. A maior parte foi `BeginReloadAssembly` (27,119 s), `LoadAllAssembliesAndSetupDomain` (51,225 s, com `LoadAssemblies` em 74,011 s) e finalização (12,268 s). A recarga foi registrada depois de uma recompilação síncrona forçada. O projeto tem 745 arquivos C# sob `Assets` e 99 assemblies compilados. O Windows estava usando 85,6% da memória física na amostragem; isso pode agravar o tempo, mas não prova causalidade. O log também repete um aviso de referência HDRP ausente no sample do Cinemachine; como a compilação atual está saudável, não há evidência de que esse aviso seja a causa do bloqueio.

O `EditorSettings.asset` mantém a recarga normal de domínio ao entrar em Play Mode. Os testes e as capturas identificaram que `No cameras rendering` vinha da cena temporária sem câmera do Test Runner; `GlobalMapRTS` contém a `Main Camera` e duas câmeras de interface/FLIR, e em execução anterior a Game View renderizou o mapa. Desligar temporariamente `GlobalWorldDiagnostics` não removeu a indisponibilidade temporária do Pipeline. As tentativas anteriores de desligar `GlobalTerrainStreamer` e o `IA01Manager` ocioso também não reduziram a mediana de Main Thread. Como o bloqueio medido ocorre durante a recarga do domínio, não desativei outros sistemas da cena: isso não isolaria esse gargalo e arriscaria alterar o estado de teste.

O Inspector de debug já existe em `Assets/Editor/IA03/IA03EstrategaNacionalEditor.cs`, com leitura do estado e botões de Paz/N4/N3/N2/N1/retomada diplomática; os comandos ficam disponíveis apenas em Play Mode. Nenhum Inspector duplicado foi criado. O teste de cessão permanente verifica dono runtime, captura serializada, evento territorial, notícia e status da proposta. `MapaGeralController` e `Construtor` consultam o `GerenteDeTerritorio` central para exibir/validar posse, e a diplomacia e captura também usam essa fonte; não apareceu uma lista econômica separada de regiões que precisasse ser transferida. Ainda falta validar UI, economia e captura juntas num cenário executado de ponta a ponta. A cena principal atualmente não tem componentes `IA_BrainMaster`, `IA03EstrategaNacional` ou `CreatyEstrategico`; a busca por GUIDs de serialização em `.unity`, `.prefab` e `.asset` também não encontrou instâncias desses componentes, perfis ou missões IA03 em outros assets do projeto. A cena separada de validação agora contém dois BrainMasters com IA03 (teams 1 e 3), quatro tanques reais de teste e 22 slots vazios para Creatys; eles permanecem sem `CreatyEstrategico` para respeitar o posicionamento manual. O teste de 15 estrategistas continua pendente. A causa confirmada da recarga anterior é o custo de recarga de assemblies/domínio; ainda não foi isolada uma assembly individual como culpada, e a pressão de memória permanece um agravante possível. A próxima medição útil é capturar um novo Domain Reload Profiler com a mesma máquina e com a memória do Windows folgada, comparando o tempo de `LoadAssemblies` antes de qualquer alteração estrutural.

### Matriz de fechamento da etapa

| Requisito | Evidência atual | Estado |
| --- | --- | --- |
| BrainMaster e scheduler existentes | IA03 usa `IIAUpdateModule`, intervalo configurável e atraso inicial por `teamId`; o teste de defasagem cobre 15 países. | Verificado em código e teste |
| Registro de Creatys sem varredura da IA03 | `RegistroCreatysEstrategicos` reutiliza cadastro de ciclo de vida; IA03 consulta o registro. Não há Creatys na cena carregada para validar operação real. | Código verificado; cenário pendente |
| Unidades próprias e inimigos conhecidos | Snapshot/cache do BrainMaster para forças próprias e visibilidade do `IA_WorldState` para inimigos conhecidos; mortes reais passam pelo registro de combate. | Parcial; sem teste de exército numa partida real |
| Hooks de morte, dano, estruturas e território | IA03 assina `CartaCombateRegistro.EventoRegistrado`, `SistemaDeDanos.OnDanoGlobal`/`OnMorteGlobal`, `OrquestradorGlobalOrdens.OrdemConcluida`, `GerenteDeTerritorio.OnTerritoryOwnerChanged`, governo e evento presidencial; o PlayMode valida morte real e remoção de assinatura. | Verificado para os caminhos cobertos; sem campanha completa |
| Resultado de batalha vencida/perdida | `RegistrarResultadoCombate` não tem produtor externo. Destruição de unidades não prova resultado de batalha; vitória de prefeitura é fim de partida. | Pendente; não inferido |
| Condições de missão, timeout e liberação | Testes EditMode cobrem sucesso, fracasso, cancelamento/expiração, rota, permanência e liberação de grupo/Creaty/fila. | Verificado em testes direcionados |
| Fila urgente e deduplicação | Teste cobre prioridade de defesa, preservação da missão interrompida e ausência de duplicata. | Verificado em teste direcionado |
| Paz/N4/N3/N2/N1 e mobilização | Testes cobrem mudança forçada, filtro N4 e limite/reserva N1; Inspector só permite forçar no Play Mode. | Verificado por testes de lógica; ciclo real pendente |
| Relatórios a cada cinco minutos e avaliação aos 25 minutos | Intervalos e contadores estão no código; não foi executada uma guerra real com esse tempo simulado. Vitórias seguem dependentes do produtor ausente. | Parcial |
| Perdas financeiras | Preço de reposição conhecido de unidade/estrutura destruída é estimado. Interrupção, recursos e território não têm valoração integrada. | Parcial |
| Reposição de unidades | IA03 altera metas/pesos dos diretores existentes; não cria nem compra unidades diretamente. Não há confirmação de reposição em campanha real. | Integração por código; comportamento real pendente |
| Cessão permanente | Teste valida troca de dono runtime, evento, estado serializado, notícia e proposta executada; UI, economia e captura conjunta não foram exercitadas em cena. | Caminho central verificado; ponta a ponta pendente |
| Performance de 15 IA03 | Harness isolado passou sem exceder budget; custo médio 0,001149 ms/quadro. Não inclui campanha, unidades, outros módulos ou GC real. | Microbenchmark sintético aprovado; perfil real pendente |
| Erro do Unity | Domain reload anterior medido em 91,244 s; em execução posterior da cena IA03, o Editor ficou sem resposta e o Pipeline expirou operações em 60 s, sem exceção de jogo identificada. `No cameras rendering` é da cena temporária de testes. | Dois bloqueios observados em fases diferentes; causa de assembly/método individual não isolada |

### Pendências para declarar a etapa concluída

1. Reabrir Unity depois que o Editor voltar a responder. Na cena salva `Assets/Tests/PlayMode/IA03_WarValidation.unity`, posicionar/configurar manualmente os Creatys (os 22 slots estão vazios) e só então executar o ciclo de teste; manter `GlobalMapRTS` intacta.
2. Executar nessa cena o ciclo Paz → N4 → N3 → N2 → N1 com eventos reais de ordem, perda, batalha e território, incluindo relatórios e reposição.
3. Definir/conectar um produtor autoritativo de fim de batalha antes de validar vitórias e a regra dos 2/3.
4. Medir prejuízo econômico acumulado para além dos custos de reposição já conhecidos.
5. Medir 15 BrainMasters ativos no Profiler em campanha representativa, comparando baseline, Main Thread, GC Alloc, picos e frequência.
6. Repetir o perfil de domain reload com memória do sistema folgada e conferir se o aviso de limpeza de `GameDifficultyManager` volta a ocorrer.

### Execução controlada da cena IA03 (03/10/2026)

A cena salva em `Assets/Tests/PlayMode/IA03_WarValidation.unity` contém dois GameObjects de país com `IA_BrainMaster` + `IA03EstrategaNacional` (teams 1 e 3), perfis e seis missões compartilhadas, quatro instâncias de tanque para teste e 22 slots vazios para Creatys. Os slots continuam sem `CreatyEstrategico` porque o posicionamento/configuração dos Creatys é manual. A cena foi movida com `AssetDatabase.MoveAsset` para o caminho de Play Mode que o projeto já reconhece; o GUID foi preservado.

A primeira tentativa de Play não testou IA03: `Assets/Scripts/Editor/EntrarNoMenuAoDarPlay.cs` direciona cenas fora das exceções configuradas para `Cena menu P`. A medição dessa execução pertence ao mapa/menu e não deve ser usada como baseline da IA03. Depois de mover a cena para `Assets/Tests/PlayMode/`, confirmei `playModeStartScene = null` e a cena correta como ativa.

Na segunda tentativa, `Editor.log` registrou a vinculação do BrainMaster para os teams 1 e 3 e a integração da cena temporária de backup levou 6,631 s. Em seguida, o Editor deixou de atender o Pipeline: as operações retornaram `Main thread operation timed out after 60000ms`, e a tentativa normal de sair do Play Mode também expirou. O processo Unity cujo título indicava `IA03_WarValidation` foi observado como `Responding=false`; numa amostra de 3 s, consumiu 3,11 s de CPU e manteve cerca de 2,8 GB de working set. Uma leitura por thread identificou a thread da janela (TID 5328) consumindo 3,98 s de CPU em 4 s; cada uma das outras threads amostradas consumiu no máximo 0,05 s. Isso confirma trabalho ativo na thread principal durante o travamento, mas não identifica o método executado. A cena e seus assets tinham sido salvos antes de entrar em Play Mode; `GlobalMapRTS`, o prefab `Barco cartel` e o asset `GlobalMap_CoarseTerrain_VisualGate` mantiveram os hashes anteriores, e `GlobalMap_CoarseTerrain.asset` continuou ausente.

Esse travamento impede afirmar que a guerra real, as ordens, os relatórios ou a mobilização foram validados. O log disponível não identifica uma exceção de jogo nem prova que IA03 causou o bloqueio. Um ponto do BrainMaster que merece medição isolada quando o Editor puder ser perfilado é `IA_BrainMaster.Update`: ele chama `SyncNationStateWithGovernment()` a cada frame para cada cérebro; esse método chama `SistemaGovernoMundial.GarantirPaisIA` e reaplica os pesos nacionais. Com os dois cérebros da cena, são duas sincronizações por frame. A frequência é visível no código, mas o custo desse caminho não foi medido e não deve ser tratado como causa confirmada.

Resultado desta tentativa: cena e configuração salvas; testes automatizados direcionados anteriores continuam aprovados (30/30); execução controlada bloqueada pelo Editor sem resposta. Não foram coletados FPS, Main Thread, GC Alloc ou picos válidos para IA03 nessa sessão, e nenhum método de produção foi alterado por causa desse travamento.
