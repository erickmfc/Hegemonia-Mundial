# Plano de implementação — ponte IA01/IA02 para IA03

## Objetivo
Reaproveitar o padrão de locais das IA01 e IA02 para alimentar o registro estratégico da IA03, sem copiar sistemas inteiros nem criar dois cadastros concorrentes.

## Fonte existente
- IA01BuildSlot / IA01CityLayout: posições, grupos, domínio, equipe, estado e save do slot.
- IA02BuildSlot / IA02CityLayout: mesma função para a segunda equipe.
- CreatyEstrategico / RegistroCreatysEstrategicos: registro único usado pela IA03 para seleção, reservas, rotas e cooldowns.

## Implementação mínima
1. Criar uma ponte de leitura que enumere slots IA01 e IA02 ativos.
2. Gerar IDs estáveis derivados de equipe, grupo e slot; nunca usar posição como identidade.
3. Converter domínio e papel do slot para IA03TipoCreaty/IA03DominioEstrategico.
4. Reconciliar a ponte após carregamento de cena/save, removendo referências de slots inexistentes.
5. Serializar somente IDs e estado da ponte; não duplicar reservas ou inventário.
6. Conectar o evento oficial de batalha ao resultado da missão IA03.
7. Validar com os 26 slots e uma campanha salva/carregada.

## Proteções
- Não editar slots existentes durante o Play Mode.
- Não alterar regras de território, economia ou construção.
- Não criar um segundo registro de Creatys.
- Preservar alterações já presentes no workspace.

## Ordem de execução
Ponte e IDs -> save/load -> resultado oficial de batalha -> testes de integração -> correção das missões especiais.

## Convenção de catalogação em português
Todos os Creatys novos e adaptados devem usar nomes e descrições em português, sem alterar o ID técnico estável. O catálogo deve exibir:
- Nível 1 — Terrestre
- Nível 1 — Naval
- Nível 1 — Aéreo
- Nível 1 — Logística
- Nível 1 — Diplomático, quando aplicável

O ID técnico permanece estável para save/load; o nome apresentado no mapa, Inspector e relatórios será em português. Domínio, nível, equipe, região e função devem aparecer separados para facilitar a alocação manual.

## Raio configurável do Creaty
Cada ponto poderá ter um raio em metros, exibido como uma esfera no mapa e no Inspector. O valor define a área de influência do ponto: por exemplo, 200 m cobre uma área menor e 500 m cobre uma área maior.

O raio só comandará patrulha ou detecção quando houver um sistema existente compatível para conectá-lo. Caso contrário, nesta primeira integração ele será usado para visualização, seleção de candidatos e validação de presença, sem criar uma simulação paralela.

## Comportamento de patrulha e perseguição segura
Quando a unidade estiver em paz, mantém a rota normal de patrulha. Em guerra ou sob ordem de ataque, uma detecção dentro do raio cria ou atualiza um contato com posição e tempo da última confirmação.

Estados previstos:
1. Patrulha: segue a rota configurada.
2. Contato detectado: interrompe a troca de waypoint e calcula uma aproximação segura.
3. Interceptação: aproxima-se do alvo sem colar nele, respeitando uma distância de segurança e o alcance efetivo das armas.
4. Rastreamento: mantém o último vetor conhecido por um período curto mesmo se o alvo sair do raio.
5. Reavaliação: se o contato for perdido, o alvo ficar fora das regras de engajamento ou a ameaça desaparecer, retorna gradualmente à patrulha.

O comportamento deve respeitar equipe, regras de guerra, alcance, combustível, danos e ordens manuais. O raio não criará uma perseguição infinita nem uma nova simulação de combate.

## Primeiro bloco implementado
Foi criado `Assets/scripts/IA03/PatrulhaCombateSegura.cs` como componente opcional e isolado. Ele:
- usa `ControleUnidade` como único executor de movimento;
- desenha e usa o raio configurável de detecção;
- memoriza a última posição do contato;
- intercepta mantendo distância segura e antecipando movimento;
- evita perseguição quando o combate não está autorizado;
- retoma a rota de patrulha após perder o contato;
- aceita contatos vindos de um radar existente por `ReceberContatoRadar`.

O componente ainda não foi anexado a prefabs nem chamado pelos controladores atuais, justamente para não interferir nos testes em andamento. A integração será feita depois de escolher os prefabs e a autoridade de guerra/radar de cada domínio.
