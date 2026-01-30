# 🚁 GUIA RÁPIDO - Adicionar Botões PASSIVO/ATIVO no Helicóptero

## ⚠️ IMPORTANTE: Por que a barra preta aparece vazia?

Você está vendo uma **barra preta sem botões** porque os comandos ainda não foram criados no Unity.
Agora ela está configurada para **ficar invisível** até você criar os botões.

---

## 📋 PASSO A PASSO (faça exatamente nesta ordem):

### 1️⃣ Criar a pasta de Comandos (se não existir)
1. No Unity, vá em `Assets/Resources/`
2. Se não existir pasta `Comandos`, crie: Right-click → Create → Folder → Nome: `Comandos`

### 2️⃣ Criar o Comando PASSIVO
1. Dentro de `Assets/Resources/Comandos/`
2. Right-click → **Create → Hegemonia → Comandos → Passivo**
3. Renomeie para: `ComandoPassivo`
4. Clique nele no Inspector e configure:
   ```
   Titulo Botao: PASSIVO
   ```
5. Salve (Ctrl+S)

### 3️⃣ Criar o Comando ATIVO
1. Dentro de `Assets/Resources/Comandos/`
2. Right-click → **Create → Hegemonia → Comandos → Ativo**
3. Renomeie para: `ComandoAtivo`
4. Clique nele no Inspector e configure:
   ```
   Titulo Botao: ATIVO
   ```
5. Salve (Ctrl+S)

### 4️⃣ Adicionar os comandos ao Helicóptero
1. Abra o **prefab do helicóptero** (na pasta Prefabs)
2. Procure o componente **UnidadeComandos**
   - Se NÃO tiver, adicione: Add Component → UnidadeComandos
3. No campo `Comandos Desta Unidade`:
   - Aumente o tamanho para `2`
   - **Element 0**: Arraste `ComandoPassivo`
   - **Element 1**: Arraste `ComandoAtivo`
4. Salve o prefab (Ctrl+S)

### 5️⃣ Testar
1. Entre no Play Mode
2. Clique no helicóptero
3. Você deve ver:
   - 🟢 Anel de seleção **levemente verde** (quase invisível)
   - 🔵 Botão **PASSIVO** (azul)
   - 🔴 Botão **ATIVO** (vermelho)

---

## ✅ Resultado Final:
- ✅ Anel de seleção transparente (0.15 alpha)
- ✅ Menu só aparece se houver comandos
- ✅ Sem barra preta vazia
- ✅ Sem texto de "VIDA"
- ✅ Apenas botões PASSIVO e ATIVO

---

## 🐛 Problemas Comuns:

**Menu não aparece?**
- Verifique se o helicóptero tem `UnidadeComandos`
- Verifique se os comandos estão na pasta `Assets/Resources/Comandos/`
- Verifique se você arrastou os comandos para o prefab

**Botões não funcionam?**
- Verifique se o helicóptero tem `ControleTorreta`
- Se não tiver, adicione o componente

**Ainda aparece barra preta?**
- Reinicie o Unity (às vezes o código demora a atualizar)
- Verifique se salvou todas as alterações

---
Data: 24/01/2026

---

## 🚢 SISTEMA DE NAVEGAÇÃO NAVAL INTELIGENTE

### O que é?
Sistema similar ao **Navio de Vigilância do Liberty** onde navios vão **DE RÉ** automaticamente quando você clica em locais próximos atrás deles!

### Como funciona?
- ✅ Clique **ATRÁS do navio** (na popa) + **Perto** = Vai de RÉ 🔴
- ✅ Clique **À FRENTE** ou **Longe** = Vai normalmente 🟢

### Setup Rápido:
1. Adicione o componente `NavegacaoInteligenteNaval` ao navio
2. Configure:
   - `Angulo Para Marcha Re`: 135°
   - `Distancia Maxima Re`: 20m
   - `Velocidade Re`: 0.6
3. Arraste o `Rastro_Agua` e `Modelo3D`
4. Teste: Clique atrás do navio!

📖 **Guias Completos:**
- `SETUP_NAVEGACAO_NAVAL.md` - Configuração passo a passo
- `GUIA_NAVEGACAO_NAVAL.md` - Documentação técnica completa

