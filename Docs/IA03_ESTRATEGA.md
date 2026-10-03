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

- Decisões usam `IIAUpdateModule` e o scheduler de BrainMaster; não existe `Update` próprio nem busca global de objetos.
- As listas de forças vêm do `IA_WorldState` e do registro central já usado por BrainMaster. Ordens passam pela `IA_CommandQueue` existente e são ignoradas em `ShadowReadOnly`.
- Pesos nacionais e metas de força alimentam os diretores já existentes de economia, mercado, diplomacia, produção, marinha e aviação. IA03 não altera saldos nem cria recursos.
- `GestorEconomiaIA03` lê saldo, comida, petróleo, energia e déficits do governo e ajusta pesos dos diretores de BrainMaster para proteger reservas e estoques essenciais.
- Relações, notícias e propostas são lidas ou registradas em `SistemaGovernoMundial`. O adaptador IA03 envia propostas de cessar-fogo, que o serviço existente pode aceitar ou recusar.
- Se o relatório apontar vantagem militar, IA03 pode propor uma indenização e pedir regiões terrestres configuradas como concedíveis. O serviço central só transfere saldo ou posse depois da aceitação.
- Perdas confirmadas usam `CartaCombateRegistro.EventoRegistrado`; uma unidade presidencial marcada por `IA03MarcaPresidencial` emite um evento a partir da notificação de morte do sistema de dano, sem consulta periódica.
- Dano a uma estrutura nacional é recebido pelo evento de dano existente. Se houver missão `DefesaDeObjetivo` compatível configurada, ela é colocada à frente da fila sem procurar unidades ou prédios na cena.
- Creatys registram-se no ciclo de vida do componente. O registro bloqueia IDs duplicados até que reste apenas uma instância.
- Missões pré-configuradas entram numa fila por prioridade. Sistemas de incidentes podem chamar `SolicitarMissaoEstrategica` para colocar uma missão urgente à frente da fila.

## Relatórios e escalada

Os relatórios são emitidos durante uma crise no intervalo configurável (5 minutos por padrão). O relatório combina o total de forças do cache, inimigos conhecidos, estoques nacionais e eventos acumulados de perdas/destruições. Os limiares de vitórias, dano econômico, baixas e pressão sobre reservas podem iniciar uma proposta de cessar-fogo após 25 minutos em conflito limitado; uma crise que continue pode escalar conforme agressividade. A avaliação de uma hora usa a pontuação acumulada para justificar uma proposta de cessar-fogo ou continuar a guerra.

Destruições confirmadas continuam vindo de `CartaCombateRegistro`. Capturas e perdas de regiões entre os dois países são atualizadas por `GerenteDeTerritorio.OnTerritoryOwnerChanged`. Dano a estruturas marcadas é medido a partir de `SistemaDeDanos.OnDanoGlobal` e fica separado em pontos de vida estrutural, sem ser convertido artificialmente em dinheiro. Resultado de batalha e prejuízo financeiro continuam exigindo um produtor autoritativo que chame `RegistrarResultadoCombate` e `RegistrarPrejuizoEconomico`; o jogo ainda não publica um evento geral de fim de batalha nem um valor monetário causado por dano militar.

## Condições de missão

O ciclo da missão avalia as condições do `MissaoEstrategicaSO`: chegada ao Creaty; permanência contínua pelo `TempoMinimoDePermanenciaSegundos`; destruição confirmada do alvo observado; sobrevivência de todo o grupo original até o prazo; confirmação externa; e captura da região política na posição do Creaty. A condição de fracasso usa a contraparte configurada (perda de unidade, saída da zona, território perdido ou confirmação externa), e qualquer prazo máximo encerra a missão que não atingiu sucesso.

Sistemas externos podem concluir missões configuradas para confirmação externa chamando `RegistrarResultadoMissaoExterno(idMissao, sucesso, motivo)` na IA03. IDs de missão devem ser únicos por perfil.

## Termos territoriais

As propostas diplomáticas aceitas podem ceder regiões terrestres permanentemente ou por um número de dias de jogo, e criar uma zona desmilitarizada temporária. A cessão temporária devolve cada região ao país original no vencimento apenas se ainda estiver sob o controle do beneficiário. Os termos e datas finais usam o estado de propostas já gravado pelo sistema de save, preservando saves antigos.

Ordens de movimento, patrulha e seguimento de infantaria e veículos das partes são recusadas quando seu destino entra em uma zona desmilitarizada. Essa aplicação não evacua automaticamente forças que já estavam lá quando o tratado foi aceito. A IA03 oferece cessão temporária de 30 dias no conflito limitado com pontuação de guerra a partir de 80, zona desmilitarizada de 45 dias na faixa 100–249 e mantém cessão permanente para pontuações maiores.

## Limites atuais de integração

As cessões permanentes e temporárias continuam limitadas a regiões terrestres configuradas como capturáveis. Uma desmilitarização se aplica às regiões terrestres incluídas na proposta. A exigência de 100% requer pontuação alta, quantidade mínima de batalhas e domínio registrado de pelo menos 90%. As missões com porta-aviões, transporte anfíbio ou presidente selecionam e enviam grupos de unidades reais pelo comando existente; embarque/desembarque de passageiros e uma visita presidencial completa dependem dos fluxos específicos desses sistemas e não são acionados por este módulo.

Vitórias por batalha, valor econômico destruído em dinheiro e retirada automática de unidades que já ocupam uma zona desmilitarizada ainda dependem de integrações autoritativas específicas. Os indicadores não presumem esses resultados.
