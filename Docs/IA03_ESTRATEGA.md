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

O projeto publica evento de destruição de unidade, mas ainda não fornece um produtor comum de resultado de batalha, dano econômico ou captura/perda de objetivo. Esses valores começam em zero até que o sistema responsável chame `RegistrarResultadoCombate`, `RegistrarPrejuizoEconomico` ou `RegistrarObjetivoCapturado`. IA03 não infere vitórias, danos ou territórios sem evidência.

## Limites atuais de integração

O acordo territorial transfere permanentemente somente regiões terrestres marcadas como concedíveis pelo mapa político; o backend ainda não implementa concessão temporária nem desmilitarização. A exigência de 100% requer pontuação alta, quantidade mínima de batalhas e domínio registrado de pelo menos 90%. As missões com porta-aviões, transporte anfíbio ou presidente selecionam e enviam grupos de unidades reais pelo comando existente; embarque/desembarque de passageiros e uma visita presidencial completa dependem dos fluxos específicos desses sistemas e não são acionados por este módulo.

Os campos `CondicaoDeSucesso` e `CondicaoDeFracasso` documentam a missão; no runtime atual, chegada ao ponto, prazo e sobrevivência do grupo controlam o encerramento. Missões que exigem confirmação de destruição ou ocupação precisam de eventos desses produtores para serem concluídas com precisão.
