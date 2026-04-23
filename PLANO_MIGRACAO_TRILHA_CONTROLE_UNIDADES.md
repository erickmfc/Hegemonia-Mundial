# Plano de Migracao da Trilha de Controle das Unidades

## Objetivo

Padronizar e substituir a trilha de controle das unidades com foco em:

- cobertura total do que o sistema atual faz;
- migracao segura e rastreavel;
- eliminacao de caminhos paralelos e legado ativo por engano;
- prevencao de regressao em unidades, prefabs, IA, HUD, producao e combate.

Regra obrigatoria desta migracao:

> Nenhum caminho antigo pode ser removido antes de o novo caminho correspondente estar cobrindo 100% das funcoes que ele substitui.

## Premissas ja definidas

- Trilha oficial naval de superficie: `ControleNavioRealista`
- Trilha oficial aerea: `ControleUnidade` como fachada + executores especializados
- `ControleSubmarino` continua como executor oficial especializado
- `ControleUnidade` passa a ser o unico ponto publico de entrada para ordens RTS

## 1. Diagnostico atual

### Nucleo de controle

- `Assets/scripts/ControleUnidade.cs`
  - Ja funciona como semi-fachada do jogo.
  - Recebe ordens de movimento.
  - Cancela patrulha/seguir.
  - Controla selecao e visual de rota.
  - Encaminha movimento para terra, mar e ar.
  - Tambem propaga modo de combate para varios subsistemas.
- Pontos concretos:
  - `MoverParaPonto`: linhas aproximadas 359-365
  - `DefinirModoCombate`: linha aproximada 561
  - `TryObterEstadoCombate`: linha aproximada 617

### Selecao e emissao de ordens

- `Assets/scripts/GerenteSelecao.cs`
  - E o principal emissor de ordens do jogador.
  - Conhece diretamente `ControleUnidade`, `ControleAviao`, `Helicoptero` e `C700TransporteAereo`.
  - Agrupa unidades por tipo e aplica comportamento por dominio.
- Ponto concreto:
  - `MoverUnidadesEmGrupo`: linha aproximada 565

### Patrulha e seguir

- `Assets/scripts/DesenharLinhasOrdem.cs`
  - Implementa patrulha e seguir via componentes adicionados/destruidos em runtime.
  - Adiciona `ComportamentoPatrulhaUniversal` e `ComportamentoSeguirUniversal`.
  - Tambem destrui scripts legados:
    - `ComportamentoPatrulha`
    - `ComportamentoPatrulhaCaminho`
    - `ComportamentoSeguir`
- Pontos concretos:
  - `AddComponent<ComportamentoPatrulhaUniversal>`: linha aproximada 222
  - `AddComponent<ComportamentoSeguirUniversal>`: linha aproximada 243
  - classes universais embutidas no mesmo arquivo: linhas aproximadas 277 e 333

### Combate e aquisicao de alvo

- `Assets/scripts/SistemaDeTiro.cs`
  - Mantem `modoPassivo`, `alvoAtual`, busca de alvo, linha de tiro e disparo.
  - O estado passivo/ativo ainda nao e uma fonte central de verdade; ele existe dentro de cada subsistema.
- Pontos concretos:
  - `modoPassivo`: linha aproximada 8
  - `alvoAtual`: linha aproximada 12
  - `OverlapSphereNonAlloc`: linha aproximada 212
  - `DefinirModoPassivo`: linha aproximada 356

### Trilha naval duplicada

- `Assets/scripts/ControleNavioRealista.cs`
  - Controlador naval maduro e completo.
  - Usa `NavMeshAgent` como apoio de navegacao.
  - Expoe `DefinirDestino`.
- `Assets/scripts/NavegacaoInteligenteNaval.cs`
  - Outra trilha naval de superficie, tambem com `NavMeshAgent`.
  - Exibe a mesma responsabilidade central: receber destino e controlar navegacao.
- Pontos concretos:
  - `ControleNavioRealista.DefinirDestino`: linha aproximada 663
  - `NavegacaoInteligenteNaval.DefinirDestino`: linha aproximada 214

### Trilha aerea especializada

- `Assets/scripts/ControleAviao.cs`
  - Possui ciclo de vida proprio de aeronave.
  - Mantem estados como `ReservaHangar`, `Taxiando`, `EmMissao`, `Pousando`, `RetornandoPraVaga`.
- `Assets/scripts/C700TransporteAereo.cs`
  - Tem logica propria de missao, taxi, retorno, carga e desembarque.
- `Assets/scripts/Helicoptero.cs`
  - Tem logica propria e ainda usa `NavMeshAgent` em partes do fluxo.
- `Assets/scripts/ControleSubmarino.cs`
  - E um executor especializado de submarino e nao deve ser achatado como navio de superficie.

### IA e producao ainda contornam a trilha principal

- `Assets/scripts/IA/IA_Comandante.cs`
  - Se existe `ControleUnidade`, usa `MoverParaPonto`.
  - Se nao existe, cai para `NavMeshAgent.SetDestination`.
- `Assets/scripts/Fabrica.cs`
  - Depois de spawnar, tenta `ControleUnidade`, senao cai para `NavMeshAgent.SetDestination`.
- `Assets/scripts/Estaleiro.cs`
  - Apos produzir navio, escolhe entre:
    - `ControleNavioRealista`
    - `NavegacaoInteligenteNaval`
    - `ControleSubmarino`
    - `IdentidadeNaval`
    - `NavMeshAgent`
- Outros bypasses encontrados:
  - `Assets/scripts/IA/BrainMaster/IA_BackendBridge.cs`
  - `Assets/scripts/IA/Modulos/IA_General.cs`
  - `Assets/scripts/IA/Modulos/IA_General_Pro.cs`
  - `Assets/scripts/IA_Dominadora.cs`
  - `Assets/scripts/IA_Suprema.cs`
  - `Assets/scripts/IdentidadeNaval.cs`
  - `Assets/scripts/NavioPetroleiro.cs`
  - `Assets/scripts/NavioTransporteTropas.cs`
  - `Assets/scripts/TransporteAnfibio.cs`
  - `Assets/scripts/TransporteTerrestre.cs`
  - `Assets/scripts/ComportamentoPatrulha.cs`
  - `Assets/scripts/ComportamentoPatrulhaCaminho.cs`
  - `Assets/scripts/ComportamentoSeguir.cs`

## 2. Problemas encontrados

- Existe dupla autoridade potencial de movimento no mesmo prefab.
- `ControleUnidade` e fachada, mas ainda divide autoridade com varias trilhas paralelas.
- Patrulha e seguir ainda sao comportamentos de componente efemero, nao estados oficiais.
- Passivo/ativo ainda depende de varredura de componentes e nao de estado oficial da unidade.
- IA, spawn e transporte conseguem reativar caminhos antigos mesmo depois de migracao parcial.
- Existem prefabs de variante, morto e teste que podem escapar se a migracao olhar apenas prefabs principais.
- A trilha naval de superficie esta duplicada entre `ControleNavioRealista` e `NavegacaoInteligenteNaval`.
- Ha chamadas diretas a `NavMeshAgent.SetDestination` fora de qualquer trilha oficial.

## 3. Trilha oficial recomendada

### Autoridade final

- Terra
  - `ControleUnidade` recebe todas as ordens publicas.
  - Um executor terrestre oficial aplica locomocao no `NavMeshAgent`.
- Naval de superficie
  - `ControleUnidade` encaminha apenas para `ControleNavioRealista`.
- Submarino
  - `ControleUnidade` encaminha apenas para `ControleSubmarino`.
- Aereo
  - `ControleUnidade` roteia.
  - `ControleAviao`, `C700TransporteAereo` e `Helicoptero` continuam como executores oficiais especializados.
- Combate
  - `ControleUnidade` passa a manter o estado oficial de combate da unidade.
  - `SistemaDeTiro`, torretas, anti-missil e lancadores deixam de ser donos do estado e passam a ser consumidores.

### O que fica mantido

- `Assets/scripts/ControleUnidade.cs`
- `Assets/scripts/ControleNavioRealista.cs`
- `Assets/scripts/ControleSubmarino.cs`
- `Assets/scripts/ControleAviao.cs`
- `Assets/scripts/C700TransporteAereo.cs`
- `Assets/scripts/Helicoptero.cs`
- `Assets/scripts/SistemaDeTiro.cs`
- scripts de torreta, lancadores e anti-missil, desde que consumam estado oficial

### O que sera substituido

- `Assets/scripts/NavegacaoInteligenteNaval.cs` como trilha ativa
- fallbacks que chamam `NavMeshAgent.SetDestination` fora da trilha oficial
- contratos publicos de patrulha/seguir baseados em `AddComponent/Destroy`
- controle difuso de passivo/ativo espalhado por varios componentes

### O que sera desativado

- qualquer executor nao-oficial presente junto do executor oficial no mesmo prefab
- qualquer bypass externo de movimento vindo de IA, producao, transporte ou utilitario

## 4. Lista completa dos afetados

### Critico

- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ControleUnidade.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\GerenteSelecao.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\DesenharLinhasOrdem.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\SistemaDeTiro.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ControleNavioRealista.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\NavegacaoInteligenteNaval.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ControleSubmarino.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ControleAviao.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ControleAviaoCaca.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\C700TransporteAereo.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\Helicoptero.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\Fabrica.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\Estaleiro.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IA\IA_Comandante.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\Menus\MenuComportamento.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\Menus\MenuComandoInteligente.cs`

### Importante

- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IA\BrainMaster\IA_BackendBridge.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IA\Modulos\IA_General.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IA\Modulos\IA_General_Pro.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IA_Dominadora.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IA_Suprema.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IdentidadeNaval.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\NavioPetroleiro.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\NavioTransporteTropas.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\TransporteAnfibio.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\TransporteTerrestre.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ComportamentoPatrulha.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ComportamentoPatrulhaCaminho.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\ComportamentoSeguir.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\IdentidadeUnidade.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\RegistroEntidadesJogo.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\Menus\MenuConstrucao.cs`
- `C:\Users\Mathe\Downloads\Hegemonia-Mundial-main\Hegemonia-Mundial-main\Assets\scripts\Heliporto.cs`

### Complementar

- Prefabs `Variant`
- Prefabs `morto`
- Prefabs em `Assets/teste`
- efeitos visuais, sons e animacoes disparados por scripts antigos
- cenas e objetos herdados que carregam componentes mistos

## 5. Plano de migracao por etapas

### Etapa 1 - Preparacao

Entra:

- inventario completo de scripts, prefabs e cenas afetados;
- matriz de autoridade por dominio e por comportamento;
- verificador automatico de combinacoes proibidas em prefab.

Sai:

- nada ainda.

Coexiste:

- todo legado permanece.

Ainda nao pode remover:

- `NavegacaoInteligenteNaval`
- fallbacks de IA e producao
- scripts legados de patrulha/seguir

Riscos:

- mapear menos do que o jogo realmente usa.

### Etapa 2 - Criacao da trilha oficial

Entra:

- API publica unica em `ControleUnidade` para:
  - mover
  - parar
  - recuar
  - patrulhar
  - seguir
  - definir modo de combate
  - cancelar ordem especial
  - consultar estado oficial
- adaptadores internos por dominio:
  - terrestre
  - naval de superficie
  - submarino
  - aviao
  - transporte aereo
  - helicoptero
- estado oficial da unidade:
  - dominio
  - executor ativo
  - ordem atual
  - modo de combate
  - bloqueios

Sai:

- nenhum comportamento antigo ainda.

Coexiste:

- o adaptador novo pode delegar para legado por baixo enquanto sobe cobertura.

Ainda nao pode remover:

- scripts antigos ainda chamados pelo adaptador oficial.

Riscos:

- o contrato novo ainda nao cobrir todos os casos especiais de missao, taxi, retorno, atracagem ou carga.

### Etapa 3 - Adaptacao das unidades

Entra:

- migracao por lotes de dominio, nao por unidade isolada;
- validacao em `Awake`/`OnEnable` para desligar executores nao-oficiais;
- diagnostico duro quando um prefab estiver com combinacao proibida.

Sai:

- permissividade de prefab hibrido sem diagnostico.

Coexiste:

- componentes legados ainda podem estar presentes, mas sem autoridade quando a unidade ja estiver migrada.

Ainda nao pode remover:

- legado de unidades que ainda nao passaram pelo lote do seu dominio.

Riscos:

- unidade parcialmente migrada perder uma capacidade rara.

### Etapa 4 - Desativacao controlada do legado

Entra:

- troca de todos os chamadores externos para API oficial:
  - IA
  - spawn
  - producao
  - pouso
  - atracagem
  - transporte
  - HUD/UI
  - selecao

Sai:

- `NavMeshAgent.SetDestination` fora dos executores oficiais;
- `NavegacaoInteligenteNaval` como trilha ativa;
- contratos publicos de patrulha/seguir baseados em componentes efemeros.

Coexiste:

- apenas bridges temporarias claramente rastreadas.

Ainda nao pode remover:

- qualquer classe ainda chamada por uma bridge de compatibilidade.

Riscos:

- IA ou spawn invisiveis continuarem disparando caminho antigo.

### Etapa 5 - Testes de cobertura

Entra:

- matriz de validacao por dominio;
- auditoria automatica de prefabs;
- auditoria automatica de chamadas proibidas.

Sai:

- fallback silencioso.

Coexiste:

- so bridges restantes com justificativa.

Ainda nao pode remover:

- bridges sem cobertura validada.

Riscos:

- regressao em unidades raras, transporte ou embarque/desembarque.

### Etapa 6 - Limpeza final

Entra:

- remocao de scripts legados sem consumidores;
- limpeza de componentes sobrando em prefab;
- documentacao da arquitetura final.

Sai:

- `NavegacaoInteligenteNaval`
- `ComportamentoPatrulha`
- `ComportamentoPatrulhaCaminho`
- `ComportamentoSeguir`
- qualquer fallback que contorne a API oficial

Coexiste:

- nada.

Riscos:

- apagar legado cedo demais.

## 6. O que ainda depende do antigo

- IA ainda chama `SetDestination` direto em varios modulos.
- `Fabrica` ainda tem fallback direto no `NavMeshAgent`.
- `Estaleiro` ainda escolhe entre varias trilhas, incluindo legado.
- `IdentidadeNaval` ainda move por conta propria.
- transporte terrestre, anfibio, navio de tropas e helicoptero ainda possuem pontos de comando proprio fora da trilha unificada.
- patrulha e seguir ainda dependem de scripts que podem ser adicionados ou destruidos em runtime.

## 7. O que ja pode migrar

- trilha terrestre padrao que ja usa `ControleUnidade`;
- UI de modo passivo/ativo que ja conversa com `ControleUnidade`;
- navios de superficie que ja estao em `ControleUnidade` + `ControleNavioRealista`;
- producao que ja encontra `ControleUnidade` no spawn.

## 8. O que precisa de ponte e fallback temporario

### Ponte de compatibilidade

- patrulha/seguir
- missao e retorno de aeronaves
- fluxo de spawn/saida de estaleiro
- sincronizacao de modo de combate entre fachada e subsistemas locais

### Fallback temporario controlado

- `ControleSubmarino`
- `C700TransporteAereo`
- `Helicoptero`
- regras especiais de embarque, desembarque, taxi, pouso e atracagem

Observacao:

- fallback temporario controlado nao significa bypass livre; significa delegacao oficial por dentro da nova API.

## 9. Checklist de cobertura total

- Toda unidade terrestre recebe ordens apenas pela API oficial.
- Todo navio de superficie usa apenas `ControleNavioRealista`.
- Todo submarino entra pela API oficial e executa via `ControleSubmarino`.
- Toda aeronave entra pela API oficial e executa via seu controlador especializado.
- Nenhuma IA chama `NavMeshAgent.SetDestination` diretamente para unidade jogavel.
- Nenhuma fabrica ou estaleiro despacha unidade fora da API oficial.
- Patrulha existe como ordem oficial, nao como contrato publico de componente efemero.
- Seguir existe como ordem oficial, nao como contrato publico de componente efemero.
- Parar e recuar estao no mesmo contrato oficial para todos os dominios.
- Passivo/ativo e uma fonte oficial de verdade por unidade.
- `SistemaDeTiro`, torretas, anti-missil e lancadores apenas consomem esse estado.
- HUD e menus leem o estado oficial, nao tentam inferir estado por conta propria.
- Sons, efeitos e animacoes continuam disparando apos a troca do controlador.
- Prefabs `Variant`, `morto` e `teste` entram na auditoria.
- Nenhum prefab mantem dois executores concorrentes ativos.

## 10. Checklist de validacao

- Teste de mover, parar, recuar, patrulhar e seguir para infantaria.
- Teste de mover, parar, recuar, patrulhar e seguir para veiculos terrestres.
- Teste de mover, parar e combate para navios de superficie.
- Teste de mover, mudar profundidade, atacar e voltar para submarinos.
- Teste de taxi, decolagem, missao, retorno e pouso para avioes.
- Teste de missao, carga, desembarque e retorno para `C700TransporteAereo`.
- Teste de helicoptero em voo, pouso, transporte e comando manual.
- Teste de passivo/ativo em todas as familias de armas.
- Teste de spawn por fabrica, estaleiro, aeroporto e heliporto.
- Teste de IA controlando deslocamento e combate sem bypass antigo.
- Teste de conflito para garantir que nenhuma unidade aceita dois controladores.
- Teste de desempenho em batalha cheia antes e depois.
- Auditoria automatica de todos os prefabs para combinações proibidas.
- Auditoria automatica de todo o codigo para chamadas proibidas de movimento.

## 11. Riscos de regressao e prevencao

- Unidade parar de obedecer
  - causa: chamador ainda aponta para sistema antigo
  - prevencao: API unica + erro explicito quando nao houver executor resolvido

- Script antigo continuar interferindo
  - causa: componente legado sobrando no prefab
  - prevencao: auditoria automatica + desligamento em `Awake`

- Duas logicas controlarem a mesma unidade
  - causa: executor antigo continuar ativo
  - prevencao: trava de autoridade por dominio

- Ataque falhar
  - causa: migrar locomocao sem preservar estado de combate
  - prevencao: centralizar estado de combate antes de remover legado

- Patrulha quebrar
  - causa: remover script runtime antes da ordem oficial equivalente
  - prevencao: bridge que traduz ordem nova para execucao antiga ate fechar cobertura

- Passivo/ativo ficar inconsistente
  - causa: multiplos componentes mantendo flags separadas
  - prevencao: `ControleUnidade` passa a ser a fonte oficial

- Prefab ficar com componente sobrando
  - causa: variantes esquecidas
  - prevencao: auditoria em todos os `*.prefab`, inclusive `morto`, `Variant` e `teste`

- Unidade migrada funcionar diferente das outras
  - causa: migracao por prefab avulso
  - prevencao: migrar por dominio completo

- Desempenho piorar
  - causa: bridges pesadas rodando em `Update`
  - prevencao: validacao apenas em `Awake`/`OnEnable`, cache de estado e medicao comparativa

## 12. Arquitetura final recomendada

### Quem controla o que

- `ControleUnidade`
  - recebe toda ordem publica;
  - guarda estado oficial da unidade;
  - publica estado para UI e IA;
  - roteia para o executor correto.

- Executor terrestre oficial
  - controla locomocao terrestre.

- `ControleNavioRealista`
  - controla locomocao naval de superficie.

- `ControleSubmarino`
  - controla locomocao e estado especializado de submarino.

- `ControleAviao`
  - controla ciclo de vida e locomocao de avioes.

- `C700TransporteAereo`
  - controla transporte aereo especializado.

- `Helicoptero`
  - controla ciclo especializado do helicoptero.

- `SistemaDeTiro`, torretas, anti-missil e lancadores
  - executam combate local, sem virar donos da autoridade global.

### Quem nao pode mais controlar nada diretamente

- IA
- HUD/UI
- producao
- spawn
- utilitarios
- identidades auxiliares
- scripts legados de patrulha/seguir
- qualquer script fora do executor oficial que tente chamar `SetDestination`

### Como evitar conflitos futuros

- toda nova unidade deve nascer com executor oficial explicito;
- auditoria de prefab deve falhar se houver combinacao proibida;
- nenhuma feature nova pode criar outro ponto publico de ordem;
- toda logica externa deve chamar apenas `ControleUnidade`.

## 13. Resultado final esperado

Ao final da migracao:

- cada unidade tera uma unica autoridade real de movimento;
- cada unidade tera um estado oficial de ordem e combate;
- IA, HUD, producao e suporte falarao com a mesma API;
- navios de superficie nao terao mais duas trilhas concorrentes;
- patrulha e seguir serao ordens oficiais, nao gambiarra de componente efemero;
- nenhuma unidade ficara parcialmente migrada sem diagnostico;
- nenhum prefab legado escapara sem entrar na auditoria.
