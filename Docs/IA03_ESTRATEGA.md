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

Em execuções anteriores, 26 testes EditMode IA03 e o teste PlayMode de morte/eventos passaram. `MissaoUrgenteFicaNoTopoEPreservaMissaoInterrompidaSemDuplicar` verifica prioridade urgente, retenção da missão interrompida e deduplicação. Na validação de 2026-10-03, Unity 6000.2.15f1 compilou sem erros e os testes `GovernorAtualizaInstanciasReutilizaveisSemTrocarReferencia` e `MicrobenchmarkIsoladoComparaQuinzeAgendadoresComESemIA03` passaram (2/2, EditMode). A primeira chamada ao Test Runner demorou e o Pipeline registrou três timeouts de operação da thread principal após 60 s; o teste terminou depois, sem falha.

O microbenchmark sintético reproduz o `PhaseOffsetSeconds` do BrainMaster e o atraso de registro da IA03 (`0.187 s + DelayInicialEscalonado`). Na execução atual, 1.800 quadros simulados com 15 schedulers vazios mediram média 0,000245 ms/quadro, p95 0,000200 ms e pico 0,034100 ms. Com 15 IA03 em N1, sem unidades ou campanha, mediram média 0,000959 ms/quadro, p95 0,001100 ms, pico 0,023400 ms, pico de módulo 0,022300 ms e 85 execuções; nenhum módulo excedeu o budget. Isso mede apenas os schedulers e estrategistas IA03 num harness isolado: não inclui os demais módulos do BrainMaster, FPS/CPU de uma guerra real ou guerra longa.

`IA_BrainMaster.Update` agora reutiliza, por cérebro, os objetos de snapshot do governor, decisão de batalha e orçamento de engajamento através de overloads que atualizam a instância recebida. O teste de identidade confirma que as referências são mantidas e os valores são atualizados. Isso remove essas três criações por frame do caminho do BrainMaster; não prova zero alocação em todo o BrainMaster. O contador `GC.GetAllocatedBytesForCurrentThread` do harness continua inválido (reportou zero mesmo em um probe com `new` explícito), então GC Alloc/frame precisa ser medido no Profiler do Unity.

### Investigação do travamento no Unity

O `No cameras rendering` não se reproduziu na cena principal: durante Play Mode, `/GlobalMap_World/Main Camera` estava ativa e a Game View renderizou o mapa e a interface. A mensagem apareceu na cena temporária `InitTestScene` do Test Runner, que não tem câmera. O Console ficou com zero erros ativos de compilação/runtime após a execução; os três erros históricos eram `Failed to handle /api/exec request: Main thread operation timed out after 60000ms`, emitidos pelo Pipeline durante o Test Runner, não exceções da partida.

Fiz A/B temporário, em Play Mode, desligando um sistema por vez e restaurando ambos antes de sair: a mediana amostrada do Main Thread ficou em 5,23 ms com tudo ligado, 5,17 ms sem `GlobalTerrainStreamer` e 5,46 ms sem o `IA01Manager` ocioso (oito amostras por condição). Não houve redução consistente. A cena tinha zero controllers IA01, zero `IA_BrainMaster` e zero IA03. Dez chamadas isoladas do mesmo `FindObjectsByType<IA01Controller>` usado por `BindSceneControllers` levaram 1,346 ms no total (0,1346 ms/call), resultado baixo demais para explicar sozinho o travamento observado. A captura não substitui uma partida longa ou o Profiler por frame.

O streamer tinha cinco tiles locais ativos/desejados e reportou 1,471 s de trabalho para a última geração de tile. As medições A/B ocorreram depois do aquecimento; um pico de geração durante o carregamento inicial continua sem medição isolada. Também apareceram avisos separados de `NavMeshAgent` sem NavMesh válido em `F200` e de vizinhos de Terrain com resoluções de heightmap diferentes; são riscos de navegação/continuidade visual, mas não produziram o `No cameras rendering` nem um erro de compilação neste teste.

O arquivo removido `GlobalMap_CoarseTerrain.asset` não é referenciado pela cena carregada: o Terrain principal usa `GlobalMap_CoarseTerrain_VisualGate.asset`, e o builder também aponta para essa variante. O mapa renderizou sem restaurar o arquivo removido. A cena principal ainda precisa de A/B com dois países e, depois, 15 cérebros IA03 para fechar o perfil real de campanha.
