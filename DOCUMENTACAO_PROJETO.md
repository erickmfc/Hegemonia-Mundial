# 📋 HEGEMONIA GLOBAL - Documentação do Projeto

**Versão:** 1.0  
**Engine:** Unity 2022+  
**Gênero:** RTS (Real-Time Strategy) Militar  
**Data:** Janeiro 2026

---

## 📖 ÍNDICE

1. [Visão Geral](#visão-geral)
2. [Sistemas Implementados](#sistemas-implementados)
3. [Scripts Principais](#scripts-principais)
4. [Estrutura de Pastas](#estrutura-de-pastas)
5. [Mecânicas de Gameplay](#mecânicas-de-gameplay)
6. [Guia de Configuração](#guia-de-configuração)
7. [Problemas Conhecidos](#problemas-conhecidos)
8. [Próximos Passos](#próximos-passos)

---

## 🎮 VISÃO GERAL

**Hegemonia Global** é um jogo de estratégia em tempo real (RTS) focado em combate militar moderno. O jogador comanda exércitos, marinhas e forças aéreas para conquistar territórios e derrotar inimigos.

### Características Principais:
- Combate terrestre, naval e aéreo
- Sistema de recursos (Dinheiro, Petróleo, Aço, Energia)
- Construção de bases e unidades
- Sistema de identificação amigo/inimigo (IFF)
- Mísseis balísticos e táticos
- Helicópteros e heliportos

---

## ⚙️ SISTEMAS IMPLEMENTADOS

### 1. **Sistema de Seleção e Controle**
- **Script:** `GerenteSelecao.cs`
- **Funcionalidade:** Seleção de unidades com mouse (clique único ou box selection)
- **Features:**
  - Anel de seleção visual
  - Seleção múltipla com arrastar
  - Comandos de movimento com clique direito

### 2. **Sistema de Movimento**
- **Script:** `ControleUnidade.cs`
- **Funcionalidade:** Movimento de unidades usando Unity NavMesh
- **Features:**
  - Pathfinding automático
  - Detecção de terreno navegável
  - Parada em destino

### 3. **Sistema de Combate - Torretas**
- **Script:** `ControleTorreta.cs`
- **Funcionalidade:** Torretas automáticas que rastreiam e atacam inimigos
- **Features:**
  - Rotação suave em direção ao alvo
  - Disparo automático
  - Modos Passivo/Ativo
  - Verificação de IFF (não ataca aliados)

### 4. **Sistema de Combate - Lançadores de Mísseis**

#### **LancadorSimples.cs** (Versão Atual - Recomendada)
- **Funcionalidade:** Lançador automático de mísseis
- **Features:**
  - Radar de 300m
  - Disparo automático ao detectar inimigos
  - Suporta ICBM e Mísseis Táticos
  - Auto-configuração de pontos de saída
  - Sistema de recarga (8 segundos)
  - Logs de diagnóstico

#### **LancadorMultiplo.cs** (Versão Antiga - Deprecada)
- Sistema mais complexo com animações
- Mantido para referência

### 5. **Sistema de Mísseis**

#### **MisselICBM.cs**
- Míssil balístico intercontinental
- Trajetória parabólica
- Grande raio de explosão
- Usado para alvos distantes

#### **MisselTatico.cs**
- Míssil tático de curto alcance
- Trajetória mais direta
- Explosão concentrada
- Usado para combate próximo
- **Parâmetros:**
  - Velocidade: 40 m/s
  - Alcance de dano: 10m
  - Dano: 150 HP

### 6. **Sistema de Danos e Vida**
- **Script:** `SistemaDeDanos.cs`
- **Funcionalidade:** Gerencia HP de unidades e estruturas
- **Features:**
  - Vida máxima configurável
  - Efeitos visuais de dano (fumaça leve, escura, fogo)
  - Explosão ao morrer
  - Sistema de upgrades de vida

### 7. **Sistema de Identificação (IFF)**
- **Script:** `IdentidadeUnidade.cs`
- **Funcionalidade:** Identifica time de cada unidade
- **Features:**
  - `teamID` para diferenciar aliados/inimigos
  - `nomeDoPais` para identificação visual
  - Previne fogo amigo

### 8. **Sistema de Recursos**
- **Script:** `GerenciadorRecursos.cs`
- **Recursos:**
  - 💰 Dinheiro
  - ⛽ Petróleo
  - 🔩 Aço (Metal)
  - ⚡ Energia
- **Features:**
  - Produção por segundo
  - População (atual/máxima)
  - Sistema de eventos para atualização de UI

### 9. **Sistema de Armazéns**
- **Scripts:** 
  - `GerenciadorArmazens.cs`
  - `DadosArmazemRecursos.cs`
- **Funcionalidade:** Armazenamento de recursos produzidos
- **Features:**
  - Capacidade máxima por recurso
  - Transferência automática da produção
  - Avisos de armazém cheio

### 10. **Sistema de Construção**
- **Script:** `MenuConstrucao.cs`
- **Funcionalidade:** Menu de compra/construção de unidades e estruturas
- **Features:**
  - Categorias: Exército, Marinha, Aeronáutica, Infraestrutura
  - Auto-carregamento de fichas (`DadosConstrucao`)
  - Geração de ícones 3D em tempo real
  - Validação de pré-requisitos (ex: Heliporto para helicópteros)
  - Sistema de snapshot para preview de unidades

### 11. **Sistema de Comportamento**
- **Script:** `MenuComportamento.cs`
- **Funcionalidade:** Controle de modos de combate
- **Modos:**
  - **Passivo:** Unidade não ataca
  - **Ativo/Ataque:** Unidade ataca inimigos automaticamente

### 12. **Sistema de Comandos**
- **Scripts:**
  - `ComandoMenu.cs` (Base abstrata)
  - `CMD_DisparoManual.cs`
  - `MenuComandoInteligente.cs`
- **Funcionalidade:** Sistema de comandos contextuais para unidades
- **Features:**
  - Botões dinâmicos baseados na unidade selecionada
  - Comandos específicos (Patrulha, Seguir, Disparar)

### 13. **Sistema de Helicópteros**
- **Scripts:**
  - `Helicoptero.cs`
  - `Heliporto.cs`
  - `GerenciadorHelicopteros.cs`
- **Funcionalidade:** Gestão de aeronaves
- **Features:**
  - Movimento aéreo (ignora NavMesh)
  - Sistema de pouso/decolagem
  - Registro global de helicópteros
  - Menu de chamada no heliporto

### 14. **Sistema Naval**
- **Script:** `Estaleiro.cs`
- **Funcionalidade:** Construção de navios
- **Features:**
  - Spawn de unidades navais
  - Ponto de saída configurável
  - Atribuição automática de Team ID

### 15. **Sistema de Censo**
- **Script:** `CensoImperial.cs`
- **Funcionalidade:** Contagem de unidades militares
- **Features:**
  - Total de unidades
  - Breakdown por tipo (terrestre, naval, aéreo)
  - Eventos de atualização

### 16. **Sistema de HUD**
- **Scripts:**
  - `PainelRecursos.cs`
  - `CriadorHUDRecursos.cs`
- **Funcionalidade:** Interface de recursos e informações
- **Features:**
  - Display vertical compacto
  - Mostra recursos, estoque, população, exército
  - Indicadores de ganho por segundo (+X/s)
  - Cores dinâmicas (verde para ganho, vermelho para perda)

### 17. **Sistema de Barra de Vida**
- **Script:** `BarraDeVida.cs`
- **Funcionalidade:** Barra de HP flutuante sobre unidades
- **Features:**
  - Billboard (sempre olha para câmera)
  - Gradiente de cor (verde → amarelo → vermelho)
  - Esconde quando vida está cheia
  - Atualização em tempo real

---

## 📁 ESTRUTURA DE PASTAS

```
Assets/
├── Prefabs/
│   ├── Nav_Corveta/          # Navios e componentes navais
│   ├── Leopard/              # Veículos terrestres
│   ├── Helicopters/          # Aeronaves
│   └── Health Bar.prefab     # UI de vida
│
├── scripts/
│   ├── LancadorSimples.cs
│   ├── LancadorMultiplo.cs
│   ├── MisselICBM.cs
│   ├── MisselTatico.cs
│   ├── ControleTorreta.cs
│   ├── ControleUnidade.cs
│   ├── SistemaDeDanos.cs
│   ├── IdentidadeUnidade.cs
│   ├── GerenteSelecao.cs
│   ├── GerenteDeJogo.cs
│   ├── GerenciadorRecursos.cs
│   ├── GerenciadorHelicopteros.cs
│   ├── CensoImperial.cs
│   ├── Helicoptero.cs
│   ├── Heliporto.cs
│   ├── Estaleiro.cs
│   ├── Construtor.cs
│   ├── BarraDeVida.cs
│   ├── PainelRecursos.cs
│   ├── CriadorHUDRecursos.cs
│   │
│   ├── Menus/
│   │   ├── MenuConstrucao.cs
│   │   ├── MenuComportamento.cs
│   │   ├── MenuComandoInteligente.cs
│   │   ├── UnidadeComandos.cs
│   │   └── Comandos/
│   │       ├── ComandoMenu.cs
│   │       └── CMD_DisparoManual.cs
│   │
│   └── Armazens/
│       ├── GerenciadorArmazens.cs
│       └── DadosArmazemRecursos.cs
│
└── ScriptableObjects/
    └── DadosConstrucao/      # Fichas de unidades/estruturas
```

---

## 🎯 MECÂNICAS DE GAMEPLAY

### **Fluxo de Jogo Básico:**

1. **Início:**
   - Jogador começa com base inicial
   - Recursos iniciais configuráveis

2. **Economia:**
   - Construir geradores de recursos (Refinarias, Minas, Usinas)
   - Produção automática por segundo
   - Armazenamento limitado (requer armazéns)

3. **Construção:**
   - Abrir menu de construção (Tecla C)
   - Selecionar categoria (Exército/Marinha/Aeronáutica/Infraestrutura)
   - Comprar unidades/estruturas
   - Validação de pré-requisitos automática

4. **Combate:**
   - Selecionar unidades
   - Mover para posição
   - Torretas e lançadores atacam automaticamente
   - Alternar entre modo Passivo/Ativo

5. **Vitória:**
   - (A ser implementado)

---

## 🔧 GUIA DE CONFIGURAÇÃO

### **Configurar uma Nova Unidade Militar:**

1. **Criar o Prefab:**
   - Adicionar modelo 3D
   - Adicionar `NavMeshAgent` (se terrestre)
   - Adicionar `Collider`

2. **Adicionar Scripts Essenciais:**
   ```
   - ControleUnidade
   - IdentidadeUnidade (teamID = 1 para jogador)
   - SistemaDeDanos
   ```

3. **Adicionar Combate (Opcional):**
   - Para torretas: `ControleTorreta`
   - Para lançadores: `LancadorSimples`

4. **Configurar Seleção:**
   - Criar anel de seleção (círculo no chão)
   - Atribuir ao campo `anelSelecao` em `ControleUnidade`

5. **Criar Ficha de Construção:**
   - Criar ScriptableObject `DadosConstrucao`
   - Configurar nome, preço, categoria, prefab
   - Adicionar ícone (opcional)

### **Configurar Lançador de Mísseis:**

1. **No Prefab da Unidade:**
   - Adicionar componente `LancadorSimples`
   - Arrastar prefab do míssil (Comar, ICBM)
   - Criar objetos "Saida" (pontos de lançamento)
   - Rotacionar "Saida" para que Z aponte para cima/frente

2. **Parâmetros Recomendados:**
   - Alcance Radar: 300m
   - Intervalo Entre Mísseis: 0.2s
   - Tempo de Recarga: 8s
   - Tags Inimigas: "Inimigo", "Destrutivel"

### **Configurar Heliporto:**

1. **Criar Heliporto:**
   - Adicionar script `Heliporto`
   - Definir ponto de pouso

2. **Criar Helicóptero:**
   - Adicionar script `Helicoptero`
   - NÃO usar NavMeshAgent
   - Usar movimento direto (Transform)

3. **Registrar:**
   - Helicópteros se registram automaticamente no `GerenciadorHelicopteros`

---

## ⚠️ PROBLEMAS CONHECIDOS

### 1. **Emojis no HUD**
- **Problema:** Fonte LiberationSans SDF não suporta emojis (💰⛽🔩)
- **Solução Aplicada:** Substituídos por texto ASCII ($, Oil, Stl, Pwr)
- **Arquivo:** `CriadorHUDRecursos.cs` (linha 107-116)

### 2. **Health Bar - Script Missing**
- **Problema:** Prefab "Health Bar" tinha script antigo deletado
- **Solução:** Criado novo script `BarraDeVida.cs`
- **Ação Necessária:** Reatribuir script ao prefab manualmente

### 3. **NavMesh em Snapshots**
- **Problema:** Erro "Failed to create agent" ao gerar ícones 3D
- **Solução Aplicada:** Try-catch e desativação de NavMeshAgent em `MenuConstrucao.cs`
- **Arquivo:** Linha 373-395

### 4. **Mísseis Saindo de Lado**
- **Problema:** Rotação incorreta dos pontos "Saida"
- **Solução:** Ajustar rotação dos Transform "Saida" no prefab
- **Eixo Correto:** Z (azul) deve apontar para direção de voo

### 5. **Armazém Cheio**
- **Problema:** Avisos constantes de "Armazém cheio"
- **Solução:** Construir mais armazéns ou consumir recursos
- **Status:** Comportamento normal, não é bug

---

---

## ⚙️ SISTEMAS IMPLEMENTADOS (Continuação)

### 18. **Sistema de IA (BrainMaster)**
- **Arquitetura:** `IA_BrainMaster.cs` (Cérebro Central)
- **Funcionalidade:** IA Multi-agente capaz de gerenciar economia, construção e combate simultaneamente.
- **Módulos Principais:**
  - `IA_BuildDirector`: Gerencia o urbanismo e expansão da base.
  - `IA_ProductionDirector`: Controla a fila de produção de unidades em fábricas/estaleiros.
  - `IA_TacticalDirector`: Tomada de decisões de ataque e defesa.
  - `IA_SquadDirector`: Gerenciamento de grupos de unidades (Esquadrões).
  - `IA_PerformanceScheduler`: Sistema de orçamento (Budget) que garante que a IA não pese no FPS.

### 19. **Sistema de Diagnóstico de Desempenho**
- **Script:** `DiagnosticoDesempenhoJogo.cs`
- **Funcionalidade:** Monitoramento em tempo real de gargalos de hardware e software.
- **Métricas:**
  - Uso de CPU (Main e Render threads) e GPU.
  - Monitoramento de Garbage Collection (GC) e Memória RAM.
  - Identificação automática de causas de travamento.
  - Exportação automática de logs para CSV.

---

## 📁 ESTRUTURA DE PASTAS (Atualizada)

```
Assets/
├── scripts/
│   ├── IA/
│   │   └── BrainMaster/      # Nova arquitetura de inteligência artificial
│   │       ├── IA_BrainMaster.cs
│   │       ├── IA_BuildDirector.cs
│   │       ├── IA_PerformanceScheduler.cs
│   │       └── ...
│   ├── DiagnosticoDesempenhoJogo.cs
│   └── ...
```

---

## 🎯 MECÂNICAS DE GAMEPLAY (Novidades)

### **Fluxo de Otimização:**
O jogo agora conta com um sistema de **Budget de Frame**. Se o hardware do jogador estiver sobrecarregado, as IAs diminuem a frequência de decisão automaticamente para manter o jogo fluido.

---

## 🔧 GUIA DE CONFIGURAÇÃO

### **Habilitar Diagnóstico:**
1. Arraste o Prefab/Script `DiagnosticoDesempenhoJogo` para qualquer cena.
2. Pressione **F8** durante o jogo para ver o Overlay completo.
3. Pressione **F9** para ativar/desativar a gravação de logs CSV.

---

## ⚠️ PROBLEMAS CONHECIDOS & CORREÇÕES RECENTES

### 6. ✅ **Busca Naval Síncrona (Corrigido)**
- **Problema:** A busca por locais para Estaleiros causava travamentos de 2 segundos.
- **Solução:** Implementada redução de passagens radiais no `IA_BuildDirector`.

### 7. ✅ **Alocação de Strings (Corrigido)**
- **Problema:** O Diagnóstico gerava lixo na RAM ao escrever logs.
- **Solução:** Substituído por `StringBuilder` no `DiagnosticoDesempenhoJogo`.

---

## 🚀 PRÓXIMOS PASSOS (Atualizado)

### **Prioridade Máxima (Em andamento):**
1. ✅ Otimização de Performance (IA e Diagnóstico)
2. ⬜ Finalizar IA Tática para ataques coordenados
3. ⬜ Implementar Telemetria de Combate

---

## 📝 NOTAS DE DESENVOLVIMENTO

### **Convenções de Código:**
- **Idioma:** Português (variáveis, comentários)
- **Nomenclatura:** camelCase para variáveis, PascalCase para classes
- **Debug:** Usar `Debug.Log` com prefixo `[NomeScript]`

### **Tags Importantes:**
- `Inimigo` - Unidades inimigas
- `Destrutivel` - Estruturas que podem ser atacadas
- `Aliado` - Unidades do jogador (opcional)

---

## 🔗 DEPENDÊNCIAS

- **Unity Version:** 2022.3 LTS ou superior
- **Packages:**
  - TextMeshPro (UI)
  - Unity UI
  - NavMesh Components (AI Navigation)

---

## 👥 CRÉDITOS

**Desenvolvedor:** Matheus (erickmfc)  
**Assistente IA:** Antigravity (Google Deepmind)  
**Data da última revisão:** Abril de 2026

---

## 📄 CHANGELOG (Recente)

### **Março/Abril 2026**
- ✅ **Refatoração BrainMaster:** IA agora é modular e resiliente.
- ✅ **Novo Diagnóstico:** Sistema profissional de métricas FPS/MS integrado.
- ✅ **Otimização de Hardware:** Redução de 85% no custo de busca navais.
- ✅ **Estabilidade de Memória:** Correção de vazamentos de GC em logs.

---

**Última Atualização:** Abril de 2026  
**Status do Projeto:** Em Desenvolvimento Ativo
