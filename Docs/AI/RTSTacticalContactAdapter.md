# Adaptador de contatos táticos

## Escopo desta etapa

O `RTSTacticalContactAdapter` é uma camada de leitura entre as fontes atuais
de detecção e a futura interface Tactical Display. Ele recebe atualizações de
`RTSVisibilityService` e `BoeingE3Reconhecimento` e expõe snapshots seguros
com estado temporal, origem, confiança, posição observada/estimada e validade.

Esta etapa não altera cenas, prefabs, saves, interface, IA03, movimentação,
ordens, regras de combate ou autorização diplomática. O método
`ReportAnonymousContact` existe para sensores futuros registrarem contatos sem
identidade; ele não possui qualquer caminho para disparo ou engajamento.

## Contrato de privacidade

- `TargetInstanceId`, `TargetTeamId` e `IdentificationLabel` só aparecem em
  uma observação atual que permite revelar identidade.
- Contatos antigos mantêm apenas um `ContactId` opaco, categoria, última
  posição, estimativa de movimento e validade. Relação, certeza de identificação
  e ameaça derivada deixam de ser publicadas quando o track não está atual.
- Uma equipe-alvo armazenada internamente não basta para publicar `Friendly`,
  `Hostile`, `Neutral` ou `Civilian`: sem identidade/IFF autorizado, a relação
  do snapshot permanece `Unknown`. Lançamento de míssil pode continuar sendo
  classificado como ameaça confirmada pela evidência do sensor, sem revelar a
  equipe de origem.
- `PositionIsEstimated` diferencia uma posição produzida por sensor ou
  extrapolação de uma posição atual observada diretamente.
- `ReportAnonymousContact` não publica `relation` só porque o chamador o
  forneceu. A relação fica `Unknown` por padrão e só é exposta quando
  `affiliationConfirmedByIndependentEvidence` for verdadeiro, reservado a
  evidência atual independente como IFF validado. O identificador individual
  continua oculto mesmo quando essa evidência confirma apenas a afiliação.
- Sem essa confirmação, valores `Friendly`, `Hostile`, `Neutral` ou `Civilian`
  enviados pelo chamador são descartados na publicação do snapshot. A string
  `origin` identifica a fonte para diagnóstico, mas não concede autoridade de
  identificação nem substitui evidência IFF.
- A flag é uma declaração do produtor confiável, não uma validação do sensor
  feita pelo adapter. Só deve ser ativada após verificar evidência independente;
  atualmente, as chamadas que exercitam essa opção estão nas fixtures de teste.
  Uma nova observação sem essa evidência limpa a confirmação anterior, e a
  relação só vale enquanto o contato estiver atual. O relatório legado do E-3
  continua sem confirmar afiliação porque não possui um campo IFF próprio.
- Classificação de tipo (`Identification`) e avaliação de ameaça são campos
  distintos da afiliação: só devem refletir evidência do sensor declarada pelo
  produtor, nunca informação derivada da equipe real oculta. `Identification`
  representa certeza da classificação do contato; não equivale a revelar a
  identidade individual, que é controlada separadamente por `IdentityRevealed`.
- `ThreatState` permanece separado de relação diplomática e identificação;
  um track de radar sem identidade não recebe ameaça derivada da equipe oculta,
  enquanto um lançamento de míssil reportado pelo E-3 pode ser `Confirmed` pela
  evidência do sensor.
- `VerticalKind` só indica altitude ou profundidade quando uma fonte fornecer
  essa semântica explicitamente. A coordenada `y` bruta de uma posição Unity
  não é tratada automaticamente como altitude ou profundidade.
- Um contato expirado deixa de ser retornado por `CopiarContatos` e pode emitir
  uma última atualização com estado `Expired`.
- O snapshot não expõe um `Transform`, uma referência de unidade ou uma API de
  seleção de alvo para armamento.

## Dependência atual de `TeamsAtWar`

Hoje o próprio `RTSVisibilityService` restringe os caminhos de visão direta e
radar a pares para os quais `TeamsAtWar` retorna verdadeiro. O adaptador não
deve reutilizar o `targetTeamId` interno para revelar essa mesma relação em um
retorno de radar sem identidade autorizada. A relação só é publicada quando a
fonte fornece evidência atual autorizada de identidade ou afiliação (como IFF
independente); caso contrário permanece desconhecida:

- uma relação em guerra pode ser apresentada como `Hostile` após identidade
  autorizada;
- um retorno de radar sem identificação ou IFF permanece `Unknown`, mesmo que
  o estado diplomático verdadeiro exista internamente;
- classificação de ameaça baseada em evidência independente (por exemplo,
  lançamento de míssil detectado) não deve expor a equipe oculta;
- o adapter não transforma `Unknown` em alvo válido;
- nenhuma ordem, ROE ou rotina de lançamento consulta este adapter.

O E-3 também não é tratado como autorização de ataque. O campo legado
`inimigo` do relatório do E-3 não é suficiente para classificar uma equipe
como hostil, pois atualmente significa equipe diferente da aeronave. A relação
do snapshot só é resolvida pelo estado diplomático existente quando a fonte
autoriza revelar a identidade; relatórios de sensor sem essa evidência ficam
`Unknown`.

`ContatoReconhecimento` não possui um indicador explícito de IFF/afiliação
confirmada. Na varredura do E-3, `UnidadeInimiga`/`UnidadeAliada` é escolhido
por comparação de `teamID`, e `nomeAlvo` recebe `IdentidadeUnidade.name`; isso
não comprova que o sensor forneceu identificação ou IFF. Por isso, o adapter
não usa nome, tipo de equipe ou `teamID` do relatório E-3 como prova de
identidade/afiliação: esses tracks ficam sem `Relation`, IDs reais e rótulo
identificador. A categoria continua `Unknown` para `UnidadeInimiga` e
`UnidadeAliada`; um lançamento de míssil observado ainda pode ser ameaça
`Confirmed` pela evidência do evento, sem revelar a equipe de origem. Uma
identidade previamente autorizada por outra fonte só é mantida até a validade
original dessa evidência, não estendida pelo relatório E-3.

`AirRecon` também não é por si só uma fonte de identidade: no
`RTSVisibilityService`, pode representar uma detecção de radar retransmitida.
Sem outro sinal autorizado, sua identificação fica `Probable` e a relação
`Unknown`.

## Desacoplamento futuro, fora desta etapa

O desacoplamento correto deve ocorrer em uma etapa própria e testada:

1. `RTSVisibilityService` deve publicar evidência de sensor mesmo quando o
   alvo não estiver em guerra, sem chamar rotinas de contra-ataque.
2. Cada evidência deve informar sensor, observador, posição/erro, validade e
   qualidade, sem conceder identidade além do que o sensor realmente conhece.
3. `TeamsAtWar` deve permanecer somente como uma entrada da política de
   relação/ROE e de autorização de engajamento, não como filtro da coleta de
   evidência.
4. Um serviço separado de autorização deve negar por padrão disparos contra
   civis, neutros, desconhecidos ou contatos sem identificação suficiente.
5. A IA03 e os diretores devem receber apenas contatos autorizados pelo
   contrato de percepção, nunca posições reais obtidas por acesso global à
   cena.
6. Testes de regressão devem verificar que detectar, classificar e autorizar
   são operações distintas e que uma detecção nunca dispara uma ordem por si.

Até essa migração, não se deve remover os filtros existentes do
`RTSVisibilityService` nem reutilizar este adapter como fonte de autorização.

## Limites atuais das fontes

O `RTSVisibilityService` fornece posição e fonte do contato, mas não fornece
uma medição semântica de altitude/profundidade nem uma classificação de tipo
para todo alvo de radar. No caminho atual, `RTSVisibilityService.ReportContact`
e a varredura do E-3 registram a posição exata do `Transform` como última
posição do contato; o adapter marca essas fontes como estimadas, mas não aplica
erro espacial à coordenada. Portanto, a incerteza reportada é metadado e não
uma medição radar espacialmente independente. Não adicionar jitter aleatório
por atualização: isso faria o track saltar e não modelaria uma observação
coerente.

Uma evolução futura pode manter uma posição observada separada da posição real:
cada varredura gera uma amostra de erro espacial limitada e determinística por
observador/sensor/track, a correção do track é suavizada, a extrapolação usa
velocidade estimada, e o raio de incerteza cresce com o tempo desde a última
varredura. Ao perder contato, só a estimativa propagada e sua incerteza ficam
disponíveis; nunca se volta a consultar o `Transform` do alvo. Altitude e
profundidade continuam desconhecidas até uma fonte real fornecê-las. O E-3
fornece velocidade/direção estimadas e classifica submarino à superfície e
lançamento de míssil, mas informação de sensor compartilhada não é promovida a
uma observação atual mais forte.

## Desempenho e ciclo de vida

- O adapter é criado no núcleo persistente via `RuntimeInitializeOnLoadMethod`,
  sem alteração de cenas ou prefabs.
- Eventos são assinados em `OnEnable` e removidos em `OnDisable`/`OnDestroy`.
- A resolução do `RTSVisibilityService` é tentada em intervalo de 0,5 s; a
  manutenção de validade ocorre em tick de 0,25 s.
- A coleção possui limite de 2048 contatos e não executa busca global de cena
  por frame.
- A futura interface deve consumir `CopiarContatos` em seu próprio tick e
  usar pooling de símbolos, sem criar objetos para cada atualização.

