# Sistema de Comandos Passivo/Ativo - Hegemonia Global

## ✅ O QUE FOI FEITO

### 1. **Removido Sistema de Vida do Menu**
- ❌ Removida a caixa preta com texto "VIDA: X/Y"
- ❌ Removido "Status: Operacional"
- ✅ Menu agora mostra APENAS os botões de comando

### 2. **Criados Comandos Passivo e Ativo**
Arquivos criados:
- `Assets/scripts/Menus/Comandos/ComandoPassivo.cs`
- `Assets/scripts/Menus/Comandos/ComandoAtivo.cs`

Estes comandos funcionam em **TODAS** as unidades que possuem `ControleTorreta`:
- ✈️ Helicópteros
- 🚁 Aviões
- ⚔️ Torres de defesa
- 🚢 Navios de guerra
- 🎯 Qualquer unidade com armamento automático

## 📋 COMO CONFIGURAR NO UNITY

### Passo 1: Criar os ScriptableObjects
1. No Unity, clique com botão direito na pasta `Assets/Resources/Comandos/`
2. Vá em: **Create → Hegemonia → Comandos → Passivo**
3. Nomeie como: `ComandoPassivo`
4. Configure:
   - **Titulo Botao**: `PASSIVO`
   - **Icone Botao**: (opcional, deixe vazio ou adicione sprite)

5. Repita para criar **Create → Hegemonia → Comandos → Ativo**
6. Nomeie como: `ComandoAtivo`
7. Configure:
   - **Titulo Botao**: `ATIVO`

### Passo 2: Adicionar aos Helicópteros
1. Selecione o prefab do helicóptero
2. Adicione o componente: **UnidadeComandos** (se não tiver)
3. No campo `Comandos Desta Unidade`, adicione:
   - Element 0: `ComandoPassivo`
   - Element 1: `ComandoAtivo`

### Passo 3: Adicionar ControleTorreta aos Helicópteros (se não tiver)
Se o helicóptero não tiver armas:
1. Adicione o componente **ControleTorreta**
2. Configure:
   - **Missel Prefab**: Arraste o prefab do míssil
   - **Locais Do Missel**: Crie transforms vazios nas asas
   - **Alcance**: 120 (ou o que preferir)
   - **Modo Passivo**: Deixe FALSE por padrão (ativo ao spawnar)

## 🎮 COMO FUNCIONA EM JOGO

### Modo PASSIVO (Azul)
- 🔵 A unidade **NÃO** ataca automaticamente
- Ela só atira se você mandar atacar um alvo específico
- Útil para não chamar atenção ou economizar munição

### Modo ATIVO (Vermelho)
- 🔴 A unidade **ATACA AUTOMATICAMENTE** qualquer inimigo no alcance
- Ela procura alvos sozinha e dispara sem precisar de ordem
- Modo padrão de combate

## 🎯 AVIÕES E OUTRAS UNIDADES

O sistema funciona **AUTOMATICAMENTE** em qualquer unidade que tenha:
- ✅ Componente `ControleTorreta`
- ✅ Componente `UnidadeComandos` com os ScriptableObjects configurados

Basta adicionar esses componentes nos prefabs e os botões aparecerão!

---
**Data**: 24/01/2026  
**Versão**: 1.0
