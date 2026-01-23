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

## 🚀 PRÓXIMOS PASSOS

### **Prioridade Alta:**
1. ✅ Sistema de lançadores funcionando
2. ⬜ IA Inimiga básica
3. ⬜ Condições de vitória/derrota
4. ⬜ Minimapa

### **Prioridade Média:**
5. ⬜ Fog of War
6. ⬜ Sistema de patrulha
7. ⬜ Formações de unidades
8. ⬜ Efeitos visuais melhorados

### **Prioridade Baixa:**
9. ⬜ Tutorial
10. ⬜ Música e sons ambiente
11. ⬜ Sistema de save/load

---

## 📝 NOTAS DE DESENVOLVIMENTO

### **Convenções de Código:**
- **Idioma:** Português (variáveis, comentários)
- **Nomenclatura:** camelCase para variáveis, PascalCase para classes
- **Debug:** Usar `Debug.Log` com prefixo `[NomeScript]`

### **Tags Importantes:**
- `Inimigo` - Unidades inimigas
- `Destrutivel` - Estruturas que podem ser atacadas
- `Aliado` - Unidades do jogador (opcional, usa teamID)

### **Layers:**
- (A ser documentado conforme necessário)

### **Team IDs:**
- `1` - Jogador
- `2+` - Inimigos/Outros times

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
**Projeto:** Hegemonia Global  
**Repositório:** erickmfc/Hegemonia-Mundial

---

## 📄 CHANGELOG

### **Versão 1.0 - Janeiro 2026**
- ✅ Sistema de seleção e movimento
- ✅ Combate com torretas
- ✅ Lançadores de mísseis (ICBM e Tático)
- ✅ Sistema de recursos e armazéns
- ✅ Menu de construção com preview 3D
- ✅ Helicópteros e heliportos
- ✅ Sistema naval (Estaleiro)
- ✅ IFF (Identificação Amigo/Inimigo)
- ✅ HUD de recursos
- ✅ Barras de vida
- ✅ Sistema de comandos contextuais

---

**Última Atualização:** 20/01/2026  
**Status do Projeto:** Em Desenvolvimento Ativo
