# Governo > Ciencia: Layout de Cards do Menu

Este documento define o layout funcional dos cards da area de Ciencia do jogo, usando os recursos industriais ja mapeados no projeto, os conteudos militares existentes e os conteudos futuros previstos para municoes e armamentos.

Objetivo:
- padronizar o menu de `Pesquisa`, `Tecnologias`, `Projetos` e `Laboratorios`
- alinhar os cards com os minerais, materiais refinados, misseis, bombas e municoes do projeto
- facilitar implementacao de UI, balanceamento e integracao futura com industria e fabricacao militar

Localizacao no jogo:
- menu principal de gestao: `Governo`
- secao lateral: `Ciencia`
- abas internas da Ciencia:
  - `Pesquisa`
  - `Tecnologias`
  - `Projetos`
  - `Laboratorios`

Regra de navegacao:
- o jogador entra por `Governo`
- dentro de `Governo`, acessa `Ciencia`
- todo o sistema de industria cientifica, materiais, municoes e programa nuclear fica agrupado dentro dessa area
- nao criar um menu separado de industria fora de `Governo > Ciencia` para esta fase visual

## Direcao Visual

Paleta sugerida:
- `Ciano`: pesquisa ativa / industrial padrao
- `Verde`: tecnologia passiva / bonus aplicado
- `Laranja`: projeto de fabricacao / refino
- `Cinza militar`: municoes e armamentos convencionais
- `Vermelho escuro`: nuclear, bloqueios e risco estrategico

Estrutura fixa do card:
1. `Indice`
2. `Icone ou imagem`
3. `Nome do card`
4. `Descricao curta`
5. `Categoria`
6. `Requisitos`
7. `Desbloqueia`
8. `Custos`
9. `Tempo`
10. `Estado`
11. `Acoes`

Campos por aba:
- `Pesquisa`: progresso, requisitos, desbloqueia, tempo restante
- `Tecnologias`: efeito, proximo beneficio, status, botao aplicar
- `Projetos`: materiais de entrada, saida, linha industrial, energia, duracao
- `Laboratorios`: especializacao, eficiencia, energia, pesquisa vinculada

## Recursos Confirmados do Projeto

### Materias-primas
- `minerio_ferro`
- `minerio_cobre`
- `bauxita`
- `minerio_titanio`
- `uranio_bruto`

### Materiais refinados
- `aco_estrutural`
- `cobre_eletrolitico`
- `duraluminio`
- `liga_titanio`
- `componentes_eletronicos`
- `uranio_enriquecido`

### Munições e armamentos previstos no design
- `Bala`
- `Bala_30`
- `Bala_Nav`
- `Tank_Bala`
- `Bomb_01_Prefeb`
- `Bomb_02_Prefeb`
- `Bomb_03_Prefeb`
- `missel_sub`
- `Missel_navTomy`
- `homing_missile`
- `Missil_05`
- `Intercept_Missile`
- `SS_Missile`
- `ICNU`

### Conteudos encontrados no projeto e nos assets
- `homing_missile`
- `Missil_05`
- `Bomb_01`
- `Bomb_02`
- `Bomb_03`
- `BallisticMissile_01`
- `BallisticMissile_02`
- `CommonMissile_01`
- `CommonMissile_02`
- `CommonMissile_03`
- `CommonMissile_04`
- `ICBM_Prefeb`
- `Cruise_Prefeb`

---

## Aba: Pesquisa

Esta aba exibe pesquisas desbloqueaveis. Cada pesquisa abre recursos, receitas, laboratorios ou familias de armamento.

### Bloco 1: Extracao e Base Industrial

#### Card 1
- `Nome`: Extracao de Minerio de Ferro
- `Cor`: Ciano
- `Categoria`: Extracao
- `Descricao`: Estudo para iniciar ordens nacionais de extracao virtual de ferro.
- `Requisitos`: Aco 150, Energia 60
- `Desbloqueia`: `minerio_ferro`, ordem de extracao de ferro
- `Tempo base`: 04:30:00
- `Icone sugerido`: escavadeira, perfuratriz ou minerio escuro

#### Card 2
- `Nome`: Extracao de Minerio de Cobre
- `Cor`: Ciano
- `Categoria`: Extracao
- `Descricao`: Permite explorar virtualmente as reservas de cobre do pais.
- `Requisitos`: Aco 120, Energia 50
- `Desbloqueia`: `minerio_cobre`, ordem de extracao de cobre
- `Tempo base`: 03:15:00
- `Icone sugerido`: bobina de cobre ou rocha com veios metalicos

#### Card 3
- `Nome`: Extracao de Bauxita
- `Cor`: Ciano
- `Categoria`: Extracao
- `Descricao`: Inicia ciclos industriais de extracao nacional de bauxita.
- `Requisitos`: Aco 120, Energia 60
- `Desbloqueia`: `bauxita`
- `Tempo base`: 05:00:00
- `Icone sugerido`: rocha marrom-avermelhada

#### Card 4
- `Nome`: Extracao de Titanio
- `Cor`: Ciano
- `Categoria`: Extracao Estrategica
- `Descricao`: Permite explorar depositos escassos de titanio.
- `Requisitos`: Aco 180, Energia 80
- `Desbloqueia`: `minerio_titanio`
- `Tempo base`: 06:30:00
- `Icone sugerido`: lingotes cinza escuro ou bloco metalico premium

#### Card 5
- `Nome`: Extracao de Uranio Bruto
- `Cor`: Vermelho escuro
- `Categoria`: Extracao Nuclear
- `Descricao`: Permite explorar depositos estrategicos de uranio bruto.
- `Requisitos`: Autorizacao nacional, Boa estabilidade, Alta energia
- `Desbloqueia`: `uranio_bruto`
- `Tempo base`: 10:00:00
- `Estado inicial`: Bloqueada
- `Icone sugerido`: atomo, barril nuclear, simbolo radiologico

### Bloco 2: Refino e Materiais

#### Card 6
- `Nome`: Metalurgia do Aco
- `Cor`: Ciano
- `Categoria`: Refino
- `Descricao`: Transforma minerio de ferro em aco estrutural.
- `Requisitos`: Extracao de Minerio de Ferro
- `Desbloqueia`: receita `aco_estrutural`
- `Tempo base`: 03:40:00
- `Icone sugerido`: viga I ou lingote de aco

#### Card 7
- `Nome`: Refino de Cobre Industrial
- `Cor`: Ciano
- `Categoria`: Refino
- `Descricao`: Refino de cobre para cabos, municoes e eletricos.
- `Requisitos`: Extracao de Minerio de Cobre
- `Desbloqueia`: receita `cobre_eletrolitico`
- `Tempo base`: 03:50:00
- `Icone sugerido`: bobina de cobre

#### Card 8
- `Nome`: Materiais Leves Aeronauticos
- `Cor`: Ciano
- `Categoria`: Materiais
- `Descricao`: Pesquisa de ligas leves para aeronaves, drones e bombas.
- `Requisitos`: Extracao de Bauxita
- `Desbloqueia`: receita `duraluminio`
- `Tempo base`: 05:30:00
- `Icone sugerido`: asa de aviao ou fuselagem

#### Card 9
- `Nome`: Ligas Estrategicas
- `Cor`: Ciano
- `Categoria`: Materiais Estrategicos
- `Descricao`: Desenvolvimento de ligas resistentes para blindagem e misseis pesados.
- `Requisitos`: Extracao de Titanio, Metalurgia do Aco
- `Desbloqueia`: receita `liga_titanio`
- `Tempo base`: 06:00:00
- `Icone sugerido`: tubos metalicos ou placas blindadas

#### Card 10
- `Nome`: Eletronica Industrial
- `Cor`: Ciano
- `Categoria`: Eletronica
- `Descricao`: Produz componentes industriais para radares, guiagem e sistemas de controle.
- `Requisitos`: Refino de Cobre Industrial, Materiais Leves Aeronauticos
- `Desbloqueia`: receita `componentes_eletronicos`
- `Tempo base`: 07:00:00
- `Icone sugerido`: placa de circuito, chip ou modulo

#### Card 11
- `Nome`: Pesquisa Nuclear
- `Cor`: Vermelho escuro
- `Categoria`: Nuclear
- `Descricao`: Etapa cientifica para iniciar o programa nuclear estrategico.
- `Requisitos`: Extracao de Uranio Bruto, Boa estabilidade, Grande energia, Alto investimento
- `Desbloqueia`: laboratorio nuclear, projeto de `uranio_enriquecido`
- `Tempo base`: 12:00:00
- `Estado inicial`: Bloqueada
- `Icone sugerido`: simbolo atomico e reator

### Bloco 3: Municoes

#### Card 12
- `Nome`: Municao Leve
- `Cor`: Cinza militar
- `Categoria`: Balistica
- `Descricao`: Padrao inicial para armamento de infantaria.
- `Requisitos`: Metalurgia do Aco, Refino de Cobre Industrial
- `Desbloqueia`: `Bala`
- `Tempo base`: 02:40:00
- `Icone sugerido`: cartucho de rifle

#### Card 13
- `Nome`: Municao Automatica 30 mm
- `Cor`: Cinza militar
- `Categoria`: Balistica Pesada
- `Descricao`: Municao para sistemas automaticos e plataformas leves.
- `Requisitos`: Municao Leve, Metalurgia do Aco
- `Desbloqueia`: `Bala_30`
- `Tempo base`: 03:20:00
- `Icone sugerido`: cartucho grande 30 mm

#### Card 14
- `Nome`: Municao Naval
- `Cor`: Cinza militar
- `Categoria`: Artilharia Naval
- `Descricao`: Projeteis reforcados para canhoes navais.
- `Requisitos`: Ligas Estrategicas
- `Desbloqueia`: `Bala_Nav`
- `Tempo base`: 04:15:00
- `Icone sugerido`: projetil naval

#### Card 15
- `Nome`: Projetil de Tanque
- `Cor`: Cinza militar
- `Categoria`: Artilharia Blindada
- `Descricao`: Municao perfurante para blindados e canhoes terrestres.
- `Requisitos`: Metalurgia do Aco, Ligas Estrategicas
- `Desbloqueia`: `Tank_Bala`
- `Tempo base`: 04:30:00
- `Icone sugerido`: shell de tanque

### Bloco 4: Bombas e Misseis

#### Card 16
- `Nome`: Bombas Aereas Leves
- `Cor`: Laranja
- `Categoria`: Armamento Aereo
- `Descricao`: Primeira familia de bombas operacionais para aeronaves.
- `Requisitos`: Materiais Leves Aeronauticos
- `Desbloqueia`: `Bomb_01_Prefeb`
- `Tempo base`: 03:00:00

#### Card 17
- `Nome`: Bombas Aereas Medias
- `Cor`: Laranja
- `Categoria`: Armamento Aereo
- `Descricao`: Bombas de medio impacto para missoes taticas.
- `Requisitos`: Bombas Aereas Leves
- `Desbloqueia`: `Bomb_02_Prefeb`
- `Tempo base`: 04:00:00

#### Card 18
- `Nome`: Bombas Aereas Pesadas
- `Cor`: Laranja
- `Categoria`: Armamento Aereo
- `Descricao`: Bombas de alto impacto para infraestrutura e blindagem.
- `Requisitos`: Bombas Aereas Medias, Ligas Estrategicas
- `Desbloqueia`: `Bomb_03_Prefeb`
- `Tempo base`: 05:10:00

#### Card 19
- `Nome`: Missil Guiado
- `Cor`: Laranja
- `Categoria`: Misseis Taticos
- `Descricao`: Base de guiagem e perseguicao de alvo.
- `Requisitos`: Eletronica Industrial
- `Desbloqueia`: `homing_missile`
- `Tempo base`: 06:00:00

#### Card 20
- `Nome`: Missil Antiaereo
- `Cor`: Laranja
- `Categoria`: Defesa Aerea
- `Descricao`: Plataforma dedicada a interceptacao aerea.
- `Requisitos`: Missil Guiado
- `Desbloqueia`: `Missil_05`
- `Tempo base`: 06:30:00

#### Card 21
- `Nome`: Missil Naval
- `Cor`: Laranja
- `Categoria`: Guerra Naval
- `Descricao`: Missil para combate maritimo e plataformas costeiras.
- `Requisitos`: Ligas Estrategicas, Eletronica Industrial
- `Desbloqueia`: `missel_sub`
- `Tempo base`: 07:00:00

#### Card 22
- `Nome`: Missil Naval de Longo Alcance
- `Cor`: Laranja
- `Categoria`: Guerra Naval
- `Descricao`: Extensao do alcance e do pacote de guiagem naval.
- `Requisitos`: Missil Naval
- `Desbloqueia`: `Missel_navTomy`
- `Tempo base`: 08:00:00

#### Card 23
- `Nome`: Missil Superficie-Superficie
- `Cor`: Laranja
- `Categoria`: Ataque Estrategico
- `Descricao`: Ataque de media e longa distancia contra alvos em terra.
- `Requisitos`: Missil Guiado, Ligas Estrategicas
- `Desbloqueia`: `SS_Missile`
- `Tempo base`: 08:30:00

#### Card 24
- `Nome`: Missil Interceptador
- `Cor`: Laranja
- `Categoria`: Interceptacao
- `Descricao`: Sistema especializado em neutralizar ameacas em voo.
- `Requisitos`: Missil Antiaereo, Eletronica Industrial
- `Desbloqueia`: `Intercept_Missile`
- `Tempo base`: 07:20:00

#### Card 25
- `Nome`: Programa ICBM
- `Cor`: Vermelho escuro
- `Categoria`: Dissuasao Estrategica
- `Descricao`: Etapa final de pesquisa para armamento balistico de alcance maximo.
- `Requisitos`: Pesquisa Nuclear, Laboratorio Nuclear, Uranio Enriquecido
- `Desbloqueia`: `ICNU`
- `Tempo base`: 16:00:00
- `Estado inicial`: Bloqueada

---

## Aba: Tecnologias

Esta aba mostra bonus permanentes e protocolos industriais / cientificos.

### Industrial Base

#### Card 1
- `Nome`: Extracao Continua
- `Cor`: Verde
- `Efeito`: reinicia automaticamente o proximo ciclo de extracao
- `Status inicial`: ativa

#### Card 2
- `Nome`: Extracao por Quantidade
- `Cor`: Verde
- `Efeito`: permite definir quantidade-alvo por recurso

#### Card 3
- `Nome`: Extracao por Numero de Dias
- `Cor`: Verde
- `Efeito`: permite ciclos temporarios programados

#### Card 4
- `Nome`: Controle por Estoque-Alvo
- `Cor`: Verde
- `Efeito`: pausa a extracao ao atingir estoque predefinido

#### Card 5
- `Nome`: Reserva Imediata de Materiais
- `Cor`: Verde
- `Efeito`: protege materiais ja alocados em projetos

#### Card 6
- `Nome`: Linhas Industriais
- `Cor`: Verde
- `Efeito`: amplia numero maximo de linhas
- `Niveis`: 2, 3, 5, 8 linhas

#### Card 7
- `Nome`: Integracao com Armazem Nacional
- `Cor`: Verde
- `Efeito`: sincronizacao automatica de estoque e reserva

#### Card 8
- `Nome`: Mercado Industrial
- `Cor`: Verde
- `Efeito`: libera compras e vendas industriais com feedback publico

#### Card 9
- `Nome`: Processamento por Eventos
- `Cor`: Verde
- `Efeito`: sistema diario centralizado via data do jogo

### Eficiência e Refino

#### Card 10
- `Nome`: Eficiência Metalurgica
- `Cor`: Verde
- `Efeito`: bonus de rendimento em aco e cobre refinado

#### Card 11
- `Nome`: Refino de Alto Rendimento
- `Cor`: Verde
- `Efeito`: reduz perdas industriais em projetos

#### Card 12
- `Nome`: Automacao de Linhas
- `Cor`: Verde
- `Efeito`: reduz tempo de espera entre ordens

### Eletronica e Guiagem

#### Card 13
- `Nome`: Miniaturizacao Industrial
- `Cor`: Verde
- `Efeito`: melhora componentes eletronicos

#### Card 14
- `Nome`: Cablagem Militar
- `Cor`: Verde
- `Efeito`: habilita projetos mais robustos de municao e guiagem

#### Card 15
- `Nome`: Guiagem de Precisao
- `Cor`: Verde
- `Efeito`: habilita sistemas guiados avancados

#### Card 16
- `Nome`: Sensores de Alvo
- `Cor`: Verde
- `Efeito`: fortalece armamentos inteligentes

#### Card 17
- `Nome`: Controle de Fogo
- `Cor`: Verde
- `Efeito`: sinergia com misseis interceptadores e anti-aereos

### Munições e Guerra

#### Card 18
- `Nome`: Producao em Massa de Municao Leve
- `Cor`: Verde
- `Efeito`: acelera lotes de `Bala`

#### Card 19
- `Nome`: Casco Reforcado 30 mm
- `Cor`: Verde
- `Efeito`: melhora lotes de `Bala_30`

#### Card 20
- `Nome`: Explosivos de Alta Densidade
- `Cor`: Verde
- `Efeito`: fortalece bombas e misseis

#### Card 21
- `Nome`: Espoleta Programavel
- `Cor`: Verde
- `Efeito`: habilita municao especializada

#### Card 22
- `Nome`: Blindagem de Projetil
- `Cor`: Verde
- `Efeito`: sinergia com `Tank_Bala` e `Bala_Nav`

#### Card 23
- `Nome`: Padronizacao Logistica de Municao
- `Cor`: Verde
- `Efeito`: reduz custo logistico de reabastecimento futuro

### Misseis e Nuclear

#### Card 24
- `Nome`: Propulsao de Combustivel Solido
- `Cor`: Verde
- `Efeito`: bonus de acesso a misseis avancados

#### Card 25
- `Nome`: Aletas de Estabilizacao
- `Cor`: Verde
- `Efeito`: melhora familia de misseis taticos

#### Card 26
- `Nome`: Navegacao Inercial
- `Cor`: Verde
- `Efeito`: exigencia para ataque estrategico

#### Card 27
- `Nome`: Radar de Interceptacao
- `Cor`: Verde
- `Efeito`: sinergia com `Intercept_Missile`

#### Card 28
- `Nome`: Autorizacao Nuclear
- `Cor`: Vermelho escuro
- `Efeito`: libera etapa institucional do programa nuclear

#### Card 29
- `Nome`: Enriquecimento Controlado
- `Cor`: Vermelho escuro
- `Efeito`: habilita o ciclo de `uranio_enriquecido`

#### Card 30
- `Nome`: Blindagem Radiologica
- `Cor`: Vermelho escuro
- `Efeito`: melhora seguranca de projetos nucleares

---

## Aba: Projetos

Esta aba mostra o que pode ser produzido nas linhas industriais e, depois, nas linhas militares.

### Projetos Industriais Atuais

#### Card 1
- `Nome`: Aco Estrutural
- `Cor`: Laranja
- `Entradas`: 1.000 t `minerio_ferro`
- `Saida`: 750 t `aco_estrutural`
- `Dinheiro`: 500
- `Energia`: 120
- `Duracao`: 2 dias
- `Linha`: obrigatoria

#### Card 2
- `Nome`: Cobre Eletrolitico
- `Cor`: Laranja
- `Entradas`: 1.000 t `minerio_cobre`
- `Saida`: 700 t `cobre_eletrolitico`
- `Dinheiro`: 650
- `Energia`: 140
- `Duracao`: 3 dias

#### Card 3
- `Nome`: Duraluminio
- `Cor`: Laranja
- `Entradas`: 1.000 t `bauxita`
- `Saida`: 550 t `duraluminio`
- `Dinheiro`: 1.200
- `Energia`: 240
- `Duracao`: 4 dias

#### Card 4
- `Nome`: Liga de Titanio
- `Cor`: Laranja
- `Entradas`: 1.000 t `minerio_titanio` + 300 t `aco_estrutural`
- `Saida`: 450 t `liga_titanio`
- `Dinheiro`: 3.500
- `Energia`: 500
- `Duracao`: 6 dias

#### Card 5
- `Nome`: Componentes Eletronicos
- `Cor`: Laranja
- `Entradas`: 300 t `cobre_eletrolitico` + 200 t `duraluminio`
- `Saida`: 100 un. `componentes_eletronicos`
- `Dinheiro`: 2.500
- `Energia`: 350
- `Duracao`: 5 dias

#### Card 6
- `Nome`: Uranio Enriquecido
- `Cor`: Vermelho escuro
- `Entradas`: 1 lote `uranio_bruto`
- `Saida`: 1 carga `uranio_enriquecido`
- `Dinheiro`: 25.000
- `Energia`: 2.500
- `Duracao`: 30 dias
- `Requisitos`: Pesquisa Nuclear, Laboratorio Nuclear, boa estabilidade

### Projetos Futuros de Municao

#### Card 7
- `Nome`: Lote de Bala
- `Cor`: Cinza militar
- `Entradas`: aco + cobre
- `Saida`: `Bala`

#### Card 8
- `Nome`: Lote de Bala 30 mm
- `Cor`: Cinza militar
- `Entradas`: aco estrutural + explosivos
- `Saida`: `Bala_30`

#### Card 9
- `Nome`: Lote de Bala Naval
- `Cor`: Cinza militar
- `Entradas`: aco estrutural + liga_titanio
- `Saida`: `Bala_Nav`

#### Card 10
- `Nome`: Projetil de Tanque
- `Cor`: Cinza militar
- `Entradas`: aco estrutural + cobre eletrolitico
- `Saida`: `Tank_Bala`

### Projetos Futuros de Bombas

#### Card 11
- `Nome`: Bomba Leve
- `Cor`: Laranja
- `Saida`: `Bomb_01_Prefeb`

#### Card 12
- `Nome`: Bomba Media
- `Cor`: Laranja
- `Saida`: `Bomb_02_Prefeb`

#### Card 13
- `Nome`: Bomba Pesada
- `Cor`: Laranja
- `Saida`: `Bomb_03_Prefeb`

### Projetos Futuros de Misseis

#### Card 14
- `Nome`: Missil Guiado
- `Cor`: Laranja
- `Saida`: `homing_missile`

#### Card 15
- `Nome`: Missil Antiaereo
- `Cor`: Laranja
- `Saida`: `Missil_05`

#### Card 16
- `Nome`: Missil Naval
- `Cor`: Laranja
- `Saida`: `missel_sub`

#### Card 17
- `Nome`: Missil Naval de Longo Alcance
- `Cor`: Laranja
- `Saida`: `Missel_navTomy`

#### Card 18
- `Nome`: Missil Superficie-Superficie
- `Cor`: Laranja
- `Saida`: `SS_Missile`

#### Card 19
- `Nome`: Missil Interceptador
- `Cor`: Laranja
- `Saida`: `Intercept_Missile`

#### Card 20
- `Nome`: Projeto ICNU
- `Cor`: Vermelho escuro
- `Entradas`: `uranio_enriquecido` + `liga_titanio` + `componentes_eletronicos`
- `Saida`: `ICNU`

---

## Aba: Laboratorios

Esta aba representa os polos cientificos e de especializacao tecnica.

#### Card 1
- `Nome`: Laboratorio de Materiais Ferrosos
- `Cor`: Ciano
- `Especializacao`: ferro, aco estrutural, projeteis

#### Card 2
- `Nome`: Laboratorio de Cobre Industrial
- `Cor`: Ciano
- `Especializacao`: cobre, condutores, municao leve

#### Card 3
- `Nome`: Laboratorio de Materiais Leves
- `Cor`: Ciano
- `Especializacao`: bauxita, duraluminio, estruturas aeronauticas

#### Card 4
- `Nome`: Laboratorio de Ligas Estrategicas
- `Cor`: Ciano
- `Especializacao`: titanio, blindagem, misseis pesados

#### Card 5
- `Nome`: Laboratorio de Eletronica Industrial
- `Cor`: Ciano
- `Especializacao`: chips, guiagem, radares, controle

#### Card 6
- `Nome`: Laboratorio Nuclear
- `Cor`: Vermelho escuro
- `Especializacao`: uranio bruto, enriquecimento, carga estrategica
- `Estado inicial`: bloqueado

#### Card 7
- `Nome`: Laboratorio Balistico
- `Cor`: Cinza militar
- `Especializacao`: `Bala`, `Bala_30`, `Tank_Bala`

#### Card 8
- `Nome`: Laboratorio de Armas Navais
- `Cor`: Cinza militar
- `Especializacao`: `Bala_Nav`, `missel_sub`, `Missel_navTomy`

#### Card 9
- `Nome`: Laboratorio de Misseis Guiados
- `Cor`: Cinza militar
- `Especializacao`: `homing_missile`, `Missil_05`, `Intercept_Missile`

#### Card 10
- `Nome`: Centro Estrategico de Dissuasao
- `Cor`: Vermelho escuro
- `Especializacao`: `SS_Missile`, `ICNU`

---

## Ordem Recomendada no Menu

Fluxo ideal de exibicao:
1. Ferro
2. Cobre
3. Bauxita
4. Titanio
5. Uranio bruto
6. Aco estrutural
7. Cobre eletrolitico
8. Duraluminio
9. Liga de titanio
10. Componentes eletronicos
11. Uranio enriquecido
12. Municao leve
13. Municao automatica
14. Municao naval
15. Projetil de tanque
16. Bombas
17. Misseis guiados
18. Misseis navais
19. Misseis estrategicos
20. Programa nuclear final

---

## Layout das Colunas por Aba

### Pesquisa
- esquerda: lista de cards de pesquisa
- centro-direita: fila de pesquisa
- direita: situacao cientifica industrial
- rodape direito: bonus total de ciencia e acoes

### Tecnologias
- esquerda: cards de tecnologias permanentes
- centro-direita: tecnologias ativas
- direita: nivel industrial, bonus e acoes

### Projetos
- esquerda: projetos disponiveis
- centro-direita: fila de projetos + linhas industriais
- direita: materiais reservados, capacidade de producao e acoes

### Laboratorios
- esquerda: laboratorios disponiveis
- centro-direita: ocupacao, capacidade e consumo energetico
- direita: situacao cientifica industrial, eficiencia total e acoes

---

## Resumo Funcional

- `Pesquisa` desbloqueia conhecimento
- `Tecnologias` aplica bonus permanentes
- `Projetos` transforma materiais em bens industriais e militares
- `Laboratorios` define especializacao, velocidade e teto cientifico

Este documento deve servir como base de implementacao visual e sistemica do menu de Ciencia.
