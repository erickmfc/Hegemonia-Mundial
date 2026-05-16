# 📋 HEGEMONIA GLOBAL - Documentação do Projeto

**Versão:** 1.6  
**Engine:** Unity 2022.3 LTS  
**Gênero:** RTS (Real-Time Strategy) Geopolítico e Militar  
**Data da Revisão:** Maio 2026

---

## 📖 ÍNDICE

1. [Visão Geral](#visão-geral)
2. [Modos de Jogo](#modos- de-jogo)
3. [Catálogo de Menus e Interface](#catálogo-de-menus-e-interface)
4. [Catálogo Detalhado de Unidades (Exército, Marinha, Aeronáutica)](#catálogo-detalhado-de-unidades)
5. [Catálogo de Estruturas e Infraestrutura](#catálogo-de-estruturas-e-infraestrutura)
6. [Sistemas Core Implementados](#sistemas-core-implementados)
7. [Mecânicas de Gameplay Avançadas](#mecânicas-de-gameplay-avançadas)
8. [Arquitetura de Inteligência Artificial (BrainMaster)](#arquitetura-de-inteligência-artificial-brainmaster)
9. [Sistemas de Logística e Economia](#sistemas-de-logística-e-economia)
10. [Sistemas de Backend e Persistência](#sistemas-de-backend-e-persistência)
11. [Estrutura de Pastas](#estrutura-de-pastas)
12. [Guia de Configuração Técnica](#guia-de-configuração-técnica)
13. [Auditoria de Conteúdo e Integridade](#auditoria-de-conteúdo-e-integridade)
14. [Problemas Conhecidos e Correções](#problemas-conhecidos-e-correções)
15. [Changelog e Próximos Passos](#changelog-e-próximos-passos)

---

## 🎮 VISÃO GERAL

**Hegemonia Global** é um simulador de estratégia em tempo real que combina táticas militares de larga escala com gestão geopolítica e econômica. O jogador deve gerenciar recursos, manter a estabilidade nacional e comandar forças terrestres, navais e aéreas em um cenário de conflito global.

---

## 🕹️ MODOS DE JOGO

1. **Modo RTS Clássico (Conquista):**
   - O jogador começa com um Quartel General (Prefeitura) e deve expandir sua base.
   - Objetivo: Eliminar a base inimiga ou atingir dominância geopolítica.
   - IA BrainMaster ativa em modo full, simulando um país rival.

2. **Modo Sandbox (Desenvolvimento):**
   - Recursos ilimitados ou acelerados para testes de unidades e infraestrutura.
   - Ideal para experimentar táticas navais e coordenação de mísseis.

3. **Modo Diplomático (Tensões):**
   - Foco no Menu de Governo e Mercado Global.
   - O conflito armado é uma consequência de falhas nas sanções e acordos comerciais.

---

## 🖥️ CATÁLOGO DE MENUS E INTERFACE

### 1. **Menu de Construção (Tecla `C`)**
O coração da produção industrial e militar. Este menu permite a expansão da base e o recrutamento de forças.
- **Categorias Disponíveis:**
  - **Exército:** Unidades de infantaria, blindados leves e pesados, e artilharia móvel.
  - **Marinha:** Embarcações de patrulha, destroyers, cruzadores, porta-aviões e submarinos.
  - **Aeronáutica:** Helicópteros de ataque/transporte, caças de superioridade aérea e bombardeiros.
  - **Infraestrutura:** Edifícios de geração de recursos (Fazendas, Refinarias) e suporte (Heliportos, Aeroportos).
  - **Defesa:** Fortificações, torres de radar, CIWS e sistemas anti-mísseis.
  - **Urbana:** Estruturas de habitação e imobiliário para crescimento populacional.
- **Funcionalidades:**
  - **Preview 3D Holográfico:** Visualização em tempo real da unidade antes da construção.
  - **Filtro de Busca Inteligente:** Permite encontrar unidades rapidamente pelo nome.
  - **Check de Requisitos:** Exibe custos em $ (Dinheiro), Oil (Petróleo) e Stl (Aço) e Energy (Energia).

### 2. **Menu de Governo e Geopolítica (Tecla `X`)**
Interface macro para gestão da nação e relações internacionais.
- **Abas Principais:**
  - **Relações Exteriores:** Lista de todas as nações, seus status diplomáticos (Paz, Guerra, Tensão) e opções para propor tratados ou declarar hostilidades.
  - **Mercado Global:** Plataforma de trading onde o jogador compra e vende Petróleo e Aço. Os preços flutuam com base na oferta e demanda global.
  - **Economia & Tesouro:** Gestão de impostos, alocação de orçamento ministerial e monitoramento da inflação/moeda nacional.
  - **Alianças e Blocos:** Gestão de participação em blocos como "Ordem Atlas" ou "Pacto Solaris".
  - **Defesa Nacional:** Visão estratégica do estado de prontidão das tropas e alertas de ameaças ICBM.
  - **Ciência e Tecnologia:** Árvore de pesquisa para desbloquear novas unidades, melhorar blindagens e eficiência de combustível.
  - **Interior e População:** Monitoramento do bem-estar social, infraestrutura urbana e crescimento demográfico.

### 3. **Menu do Pier Naval (Tecla `V`)**
Menu contextual para gestão de frotas atracadas.
- **Ações de Docagem:**
  - **Reparo Estrutural:** Recupera a integridade do casco usando recursos de Aço.
  - **Reabastecimento Logístico:** Enche os tanques de combustível e estoques de munição/torpedos.
  - **Gestão de Vagas:** Visualização de quantos navios podem ser suportados simultaneamente no pier.
- **Automação:** Opção para definir atracagem automática de navios com baixo nível de vida ou combustível.

### 4. **Menu de Mísseis e Silos (Contextual)**
Interface de comando para armamento de destruição em massa e mísseis táticos.
- **Seleção de Ogiva:** Escolha entre mísseis convencionais, ogivas termobáricas ou mísseis nucleares (ICBM).
- **Interface de Alvo:** Seleção de coordenadas diretamente no mini-mapa ou visão tática.
- **Contramedidas:** Monitoramento da capacidade de interceptação inimiga antes do lançamento.

### 5. **Menu de Comportamento (HUD Lateral)**
Permite definir a postura operacional das unidades selecionadas.
- **Modo Passivo:** Unidades não atacam a menos que recebam uma ordem direta (ideal para furtividade).
- **Modo Ativo (Fogo Livre):** Unidades engajam automaticamente qualquer inimigo detectado dentro do raio de alcance.
- **Modo Defensivo:** Unidades protegem uma área ou outra unidade específica, priorizando a escolta.

### 6. **Menu de Comando Inteligente (Mouse Direito / Contextual)**
Sistema de ordens rápidas para micro-gerenciamento em combate.
- **Patrulha:** Define uma rota circular ou linear para vigilância.
- **Escolta:** Vincula uma unidade (como um Destroyer) à proteção de outra (como um Porta-Aviões).
- **Captura:** Comando específico para infantaria ocupar prédios e pontos de interesse.

### 7. **HUD de Recursos e Estado Maior**
Exibição constante de métricas vitais no topo da tela.
- **Recursos:** Dinheiro ($), Petróleo (Oil), Aço (Stl), Energia (Pwr) e População (Pop).
- **Censo Militar:** Ícones rápidos que mostram o total de forças terrestres, navais e aéreas ativas no momento.

### 8. **Menus de Sistema (Esc / Inicial)**
- **Menu Inicial:** Acesso ao carregamento de campanhas, configurações de gráficos/áudio e créditos do projeto.
- **Menu de Pausa:** Permite salvar o progresso atual, ajustar a dificuldade dinamicamente ou retornar ao quartel general (menu principal).

---

## ⚔️ CATÁLOGO DETALHADO DE UNIDADES

### 🚜 Forças Terrestres (Exército)
- **Infantaria:**
  - **Soldado Rifle (Standard):** Unidade básica para ocupação e defesa de baixo custo.
- **Blindados Leves e Reconhecimento:**
  - **Humvee (Hamer):** Versátil, rápido e capaz de transportar pequenos esquadrões.
  - **Tanque Ubu:** Blindado leve focado em velocidade e flanqueamento.
  - **Hovercraft:** Transporte anfíbio para tropas e veículos leves.
- **Blindados Pesados (MBT):**
  - **Tanque Arthur:** O tanque de batalha principal padrão, equilíbrio entre poder e defesa.
  - **Tanque King:** Blindagem ultra-pesada para romper linhas defensivas.
  - **Tanque South:** Variante tática com sistemas de mira avançados.
  - **Tanque Leopard (Leon_c1):** Alta precisão e cadência de tiro.
  - **Tank_Antigravity:** Unidade experimental de alta tecnologia (Protótipo).
- **Artilharia e Suporte:**
  - **Artilharia Ares (MI):** Unidade de longo alcance com projéteis de alto impacto.
  - **Artilharia Caoc1:** Suporte móvel para fogo de supressão.
  - **Caminhão de Combustível (Track):** Vital para manter o avanço das frotas blindadas.
  - **Veículo Hack:** Unidade de guerra eletrônica (EW) para interferência.

### ⚓ Forças Navais (Marinha)
- **Navios de Patrulha e Escolta:**
  - **USS Arrowhead:** Corveta rápida para interceptação e patrulha costeira.
  - **Fragata F200:** Especializada em defesa de médio alcance.
  - **Navio Vigilante:** Focado em detecção e vigilância de perímetros navais.
- **Navios de Linha de Frente:**
  - **USS Dominion (Cruzador):** Poder de fogo massivo contra outros navios.
  - **USS Vindicator (Destroyer):** Defesa antiaérea e caçador de submarinos.
  - **USS Ironclad (Battleship):** O auge da artilharia naval tradicional.
  - **Navio Liberty Prime:** Unidade de assalto naval pesado.
- **Submarinos e Unidades Stealth:**
  - **USS Mako:** Caçador silencioso de navios capitais.
  - **USS Wraith:** Submarino invisível ao radar para ataques cirúrgicos.
  - **USS Leviathan (SSBN):** Plataforma submarina de lançamento nuclear.
- **Logística e Projeção:**
  - **USS Sovereign (Porta-Aviões):** Projeção de poder aéreo em escala global.
  - **Navio de Transporte de Tropas:** Movimentação massiva de exércitos terrestres.
  - **Navio Petroleiro:** Suporte de combustível para frotas de longo alcance.

### ✈️ Forças Aéreas (Aeronáutica)
- **Superioridade Aérea e Interceptação:**
  - **F-22 Raptor:** O rei dos céus, invisível e letal.
  - **F-C19 (Falcon):** Versátil para combate ar-ar e ar-terra.
  - **Série Jet (01 a 06):** Diversas variantes de jatos de combate para diferentes teatros.
- **Ataque e Bombardeio:**
  - **Bombardeiro B-2:** Furtividade total para ataques estratégicos profundos.
  - **Série Bomber (01, 02):** Bombardeiros táticos para suporte aproximado.
  - **Helicóptero Apache:** Terror dos blindados em campo aberto.
- **Logística e Suporte Aéreo:**
  - **C-700 Transporte:** O gigante dos ares para logística pesada.
  - **Drone Kamikaze (UAV-01):** Precisão descartável para alvos de alta prioridade.

---

## 🏗️ CATÁLOGO DE ESTRUTURAS E INFRAESTRUTURA

### 🏛️ Comando e Produção
- **Quartel General (HQ):** O centro nervoso da nação. Desbloqueia novas tecnologias e construções.
- **Fábrica de Veículos:** Cadeia de montagem para todos os blindados terrestres.
- **Estaleiro Marinho:** Estaleiro industrial para construção de navios e submarinos.
- **Aeroporto & Hangar:** Pistas de decolagem e manutenção para aeronaves de asa fixa.
- **Heliporto:** Plataforma de pouso e manutenção para unidades VTOL.

### ⚡ Energia e Recursos
- **Fazenda:** Geração passiva de recursos financeiros e mantimentos.
- **Refinaria:** Processamento de petróleo bruto para uso militar e venda.
- **Plataforma Offshore:** Extração de óleo em águas profundas.
- **Geradores de Energia:** Mantêm toda a grade elétrica da base ativa.

### 🛡️ Defesa e Logística Base
- **Torre de Radar:** Detecção precoce de ameaças e remoção da névoa de guerra.
- **Pier de Marinha:** Logística de atracagem, reparo e reabastecimento.
- **CIWS Phalanx:** Defesa antiaérea terminal e contra mísseis.
- **Sistema Anti-Míssil (THAAD):** Interceptação de longo alcance.
- **Silo Nuclear:** Dissuasão estratégica e lançamento de ICBMs.
- **Torre de Sentinela / Vigilante:** Defesa automática de perímetro.
- **Centro de Suporte Aéreo:** Coordenação tática para missões aéreas.
- **Muro de Concreto / Cercas:** Delimitação e proteção física da base.
- **Postes de Iluminação:** Visibilidade noturna em áreas operacionais.

---

## ⚙️ SISTEMAS CORE IMPLEMENTADOS

### 1. **Sistema de Combate e Torretas**
- Rastreamento automático de alvos com IFF (Amigo/Inimigo).
- Clamping de rotação para evitar que navios atirem em si mesmos.
- **Modos de Combate:** Alternância entre Passivo (não ataca) e Ativo (fogo livre).

### 2. **Sistema de Mísseis (ICBM e Táticos)**
- Trajetórias parabólicas realistas.
- Danos em área (AoE) diferenciados por ogiva.
- Sistema de interceptação por CIWS.

### 3. **Sistema de Combustível e Logística**
- Unidades consomem combustível ao se moverem.
- Necessidade de retorno para bases ou piers para reabastecimento.
- Caminhões de combustível (Track Combustível) para suporte em campo.

### 4. **Diagnóstico de Performance (F8/F9)**
- Overlay profissional de métricas (FPS, MS, RAM, Draw Calls).
- Sistema de "Frame Budget" para a IA.

---

## 🧠 ARQUITETURA DE INTELIGÊNCIA ARTIFICIAL (BrainMaster)

A IA opera em um sistema modular altamente otimizado através de **Diretores Especializados**:

### 1. **Diretores de Comando (Nível Macro)**
- **IA_BrainMaster:** O coordenador central que dita a estratégia global (Ofensiva, Defensiva ou Expansão).
- **IA_NationalDirectors:** Gerencia a política e o orçamento nacional, decidindo onde investir recursos.
- **IA_WorldState:** Monitora constantemente a posição e força de todas as unidades no mapa.

### 2. **Diretores Operacionais (Nível Tático)**
- **IA_BuildDirector:** Responsável pelo urbanismo da base, posicionamento de prédios e logística de energia.
- **IA_ProductionDirector:** Decide quais unidades construir com base nas ameaças detectadas (Counter-Play).
- **IA_TacticalDirector:** Comanda movimentações de tropas, define alvos de ataque e rotas de flanco.
- **IA_SquadDirector:** Agrupa unidades em esquadrões para ataques coordenados e suporte mútuo.

### 3. **Diretores de Domínio (Nível de Unidade)**
- **IA_AirDirector:** Gere decolagens, patrulhas aéreas e missões de bombardeio.
- **IA_NavalDirector:** Coordena frotas, posicionamento de submarinos e ataques costeiros.
- **IA_DefenseDirector:** Prioriza a construção de defesas contra mísseis e fortificações em pontos críticos.

### 4. **Otimização e Performance**
- **Performance Scheduler:** As decisões da IA são distribuídas em múltiplos frames (Time-Slicing).
- **Threat Analyzer:** Avalia o perigo iminente para priorizar interceptações de mísseis.
- **Naval Placement Fast Resolver:** Algoritmo otimizado para encontrar locais válidos para estruturas navais.

---

## 📦 SISTEMAS DE LOGÍSTICA E ECONOMIA

### 1. **Gestão de Energia (Power Grid)**
- Estruturas requerem energia para funcionar (Radares, Fábricas, Defesas).
- Se a demanda exceder a produção, as defesas param de funcionar e a produção é paralisada.

### 2. **Cadeia de Combustível (Fuel Chain)**
- **Refinarias:** Transformam petróleo bruto em combustível utilizável.
- **Logística de Abastecimento:** Unidades sem combustível ficam imóveis e vulneráveis.
- **Postos Avançados:** Caminhões de combustível e navios petroleiros estendem o alcance operacional das forças invasoras.

### 3. **Transporte e Mobilidade**
- **Ponto de Saída (Rally Points):** Configuração de destinos automáticos para unidades recém-produzidas.
- **Transporte Aéreo/Marítimo:** Mecânica de embarque/desembarque para mover exércitos através de obstáculos naturais.

## 📁 ESTRUTURA DE PASTAS

```
Assets/
├── scripts/
│   ├── IA/BrainMaster/      # Cérebro e diretores da IA
│   ├── Menus/               # UIs de Construção, Governo e Pier
│   ├── Marinha/             # Lógica naval e submersão
│   ├── Combate/             # Torretas, Mísseis e Danos
│   └── Gerenciadores/       # Recursos, Censo e Logística
├── Prefabs/                 # Unidades, Prédios e Efeitos
└── ScriptableObjects/       # Dados de construção e Perfis de Dificuldade
```

---

## 💾 SISTEMAS DE BACKEND E PERSISTÊNCIA

### 1. **Sistema de Save/Load (JSON/Binary)**
- Persistência total do estado do mundo, incluindo posição de unidades, recursos e progresso da IA.
- Utiliza `SaveableEntity.cs` para marcar objetos que devem ser serializados.

### 2. **Sistema de Localização**
- Suporte a múltiplos idiomas via `LocalizationManager.cs`.
- Mapeamento dinâmico de strings para menus e descrições de unidades.

### 3. **Auditoria de Conteúdo**
- Sistema automático (`AuditoriaConteudoJogo.cs`) que verifica a integridade dos prefabs e assets antes do build, evitando erros de referência nula.

---

## 🔧 GUIA DE CONFIGURAÇÃO TÉCNICA

### Como adicionar uma nova Unidade:
1.  **Prefab:** Crie um modelo 3D e adicione os componentes `ControleUnidade.cs` e `IdentidadeUnidade.cs`.
2.  **Dados:** Crie um ScriptableObject do tipo `DadosConstrucao` para definir custo, tempo de produção e ícone.
3.  **IA:** Registre a unidade no `IA_ProductionDirector` para que a IA saiba quando construí-la.
4.  **Combate:** Adicione `ControleTorreta.cs` se a unidade possuir armas.

### Como configurar o Terreno para a IA:
- A IA utiliza o `IA_MapAnalyzer` para escanear o NavMesh e identificar zonas de expansão.
- Use `IA_ManualPlacementTag` em áreas específicas onde você deseja que a IA priorize construções.

### Sistema de Som:
- Utilize `SomUnidade.cs` para gerenciar áudios espaciais 3D (motores, tiros, explosões).
- O sistema de som é otimizado para evitar saturação de canais em batalhas de larga escala.

---

## 📊 AUDITORIA DE CONTEÚDO E INTEGRIDADE

O sistema automático de auditoria (`AuditoriaConteudoJogo.cs`) realizou uma varredura completa no catálogo de unidades. Foram identificadas as seguintes inconsistências que requerem atenção imediata no Unity Editor:

### ❌ Prefabs Ausentes (Dados de Construção)
As seguintes "Fichas de Unidade" (ScriptableObjects) estão com a referência de Prefab nula, o que impede sua construção no jogo:
- **USS Dominion:** Prefab ausente em `Nav_USS_Dominion/USS_Dominion.asset`.
- **USS Wraith:** Prefab ausente em `Nav_USS_Wraith/USS_Wraith.asset`.
- **USS Mako:** Prefab ausente em `Nav_USS_Mako/USS_Mako.asset`.
- **USS Arrowhead:** Prefab ausente em `Nav_USS_Arrowhead/USS_Arrowhead.asset`.
- **Artilharia:** Prefab ausente em `Artilharia/Artilharia_ar.asset`.
- **Centro de Construção:** Prefab ausente em `Demolicao/Centro de construcao.asset`.

### ⚠️ Avisos de Configuração
- **IdentidadeUnidade:** Algumas unidades militares básicas foram detectadas sem o componente de identificação, o que pode quebrar a lógica da IA BrainMaster.
- **SaveableEntity:** Estruturas de recursos sem este componente não terão seu estado persistido corretamente em saves manuais.

---

## ⚠️ PROBLEMAS CONHECIDOS E CORREÇÕES

1. ✅ **Submersão de Submarinos:** Corrigido problema de sincronia entre NavMesh e altura visual usando `Mathf.SmoothStep`.
2. ✅ **Performance de Busca:** Otimizada a busca por pontos de construção naval (redução de 90% no custo de processamento).
3. ✅ **Parentesco de Física:** Corrigido jitter em helicópteros pousados em navios em movimento.

---

## 🚀 CHANGELOG E PRÓXIMOS PASSOS

**Últimas Atualizações (Maio 2026):**
- **Sistema de Energia:** Implementação completa da gestão de energia (Power Grid) e dependência de infraestrutura.
- **Logística Naval:** Refinamento da atracagem no Pier e sistema de reabastecimento via Navio Petroleiro.
- **Mecânica de Submarinos:** Correção da sincronia visual de submersão e física de rastro de água.
- **Otimização de Build:** Resolução de erros de shader URP e validação de assets automática.
- **IA Tática:** Implementação do `IA_PerformanceGovernor` para garantir 60 FPS mesmo com múltiplas IAs ativas.
- **Auditoria de Integridade:** Ativação do sistema de varredura que detectou 7 unidades com prefabs desconectados.

**Próximos Passos:**
1. ⬜ **Invasão Anfíbia:** Refinar a coordenação entre Hovercrafts e frotas de suporte para desembarques automáticos da IA.
2. ⬜ **Clima Dinâmico:** Adicionar tempestades que afetam a precisão de mísseis e o alcance do radar.
3. ⬜ **Diplomacia Avançada:** Implementar sistema de "Casus Belli" e sanções econômicas que afetam o preço no Mercado Global.
4. ⬜ **Telemetria:** Finalizar o sistema de estatísticas de combate (K/D ratio e eficiência de recursos).

---
**Status do Projeto:** Desenvolvimento Ativo (Versão Alpha 1.6)  
**Assinado:** Comandante Antigravity & Equipe de Desenvolvimento.
