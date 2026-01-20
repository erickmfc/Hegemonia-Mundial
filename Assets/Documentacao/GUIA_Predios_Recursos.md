# 🏭 Guia: Conectando Prédios ao Sistema de Recursos

## 📋 Visão Geral

Este guia ensina como fazer seus prédios (usinas, refinarias, casas, etc.) modificarem automaticamente os recursos do HUD. Quando você constrói uma **Usina de Petróleo**, por exemplo, ela aumenta automaticamente o ganho de petróleo por segundo mostrado no HUD!

---

## 🎯 Como Funciona

1. **Você constrói um prédio** (ex: Usina de Petróleo)
2. O componente `PredioRecursos` **se registra automaticamente** no `GerenciadorRecursos`
3. Os **ganhos por segundo aumentam** (ex: +2 petróleo/s)
4. O **HUD atualiza automaticamente** mostrando o novo valor
5. Quando o prédio é **destruído**, os ganhos são **removidos**

---

## 🚀 Setup Básico - 3 Métodos

### **Método 1: Usar Scripts Prontos** ⭐ MAIS FÁCIL

Use os exemplos já criados:

**Prédios disponíveis:**
- `UsinaPetroleo.cs` - Gera petróleo (+2/s, +5/s ou +10/s por nível)
- `RefinariaAco.cs` - Gera aço (+3/s)
- `UsinaEnergia.cs` - Gera energia (+10/s)
- `Banco.cs` - Gera dinheiro (+20/s)
- `PocoPetroleo.cs` - Gera petróleo (+5/s)
- `CasaResidencial.cs` - Aumenta população e gera renda

**Como usar:**
1. Selecione o **prefab do seu prédio** (ex: Prefab_Usina_Petroleo)
2. Clique em **Add Component**
3. Adicione o script **`UsinaPetroleo`**
4. **Pronto!** Ao construir, produzirá automaticamente

---

### **Método 2: Componente Genérico** 🔧 FLEXÍVEL

Use o `PredioRecursos.cs` base para qualquer prédio:

**Passo a passo:**

1. Selecione o prefab do prédio na pasta `Assets/Prefabs`
2. **Add Component** → Pesquisar: `PredioRecursos`
3. Configure no Inspector:

```
💰 Produção Dinheiro: 0      (deixe 0 se não produz)
⛽ Produção Petróleo: 2       (exemplo: +2 petróleo/s)
🔩 Produção Aço: 0
⚡ Produção Energia: 0

⚙️ Ativar Ao Criar: ✅ (marcar)
Delay Inicial: 0 (segundos até começar a produzir)
```

4. **Apply** no prefab
5. **Pronto!**

---

### **Método 3: Script Customizado** 💻 AVANÇADO

Crie seu próprio script herdando de `PredioRecursos`:

```csharp
using UnityEngine;

public class MinhaUsina : PredioRecursos
{
    void Start()
    {
        // Configure a produção
        producaoPetroleo = 5f;   // +5 petróleo/s
        producaoDinheiro = -2f;  // -2 dinheiro/s (custo operacional)
        
        delayInicial = 3f; // 3s para ligar
        
        base.Start(); // IMPORTANTE: chama o Start do PredioRecursos
    }
}
```

---

## 📊 Exemplos Práticos

### **Exemplo 1: Usina de Petróleo Simples**

```
Prefab: Usina_Petroleo_Lvl1
Script: UsinaPetroleo.cs
Configuração:
  - Nível: 1
  - Produz: +2 petróleo/s
```

**Resultado:** Quando construída, o HUD mostra:
```
⛽ 500 (+2/s)  ← Era (+0/s) antes
```

---

### **Exemplo 2: Refinaria que Consome Recursos**

```
Prefab: Refinaria_Aco
Script: PredioRecursos.cs
Configuração:
  💰 Produção Dinheiro: -1     ← NEGATIVO (consome)
  ⛽ Produção Petróleo: 0
  🔩 Produção Aço: +3           ← POSITIVO (produz)
  ⚡ Produção Energia: -0.5     ← NEGATIVO (consome)
```

**Resultado no HUD:**
```
💰 5,000 (+9/s)   ← Era +10/s, agora -1 = +9/s
🔩 300 (+3/s)     ← Era +0/s, agora +3/s
⚡ 100 (-0.5/s)   ← Era +0/s, agora -0.5/s (vermelho!)
```

---

### **Exemplo 3: Sistema de Upgrades**

Usina com 3 níveis:

```csharp
// No UsinaPetroleo.cs
Nível 1 → +2 petróleo/s   (custo: $500)
Nível 2 → +5 petróleo/s   (upgrade: $1000)
Nível 3 → +10 petróleo/s  (upgrade: $1500)
```

**Como fazer upgrade:**
```csharp
// Em outro script ou botão UI
UsinaPetroleo usina = GetComponent<UsinaPetroleo>();
usina.FazerUpgrade(); // Sobe para próximo nível
```

---

## 🎮 Integração com Sistema de Construção

### **Opção 1: Instantiate Direto**

```csharp
// No seu MenuDeConstrucao.cs
public void ConstruirUsina(Vector3 posicao)
{
    GameObject prefab = Resources.Load<GameObject>("Predios/Usina_Petroleo");
    GameObject novaUsina = Instantiate(prefab, posicao, Quaternion.identity);
    
    // O script PredioRecursos já vai se auto-registrar!
    // Não precisa fazer mais nada!
}
```

---

### **Opção 2: Com Construtor Existente**

Se você já tem um sistema de construção (`Construtor.cs`):

```csharp
void FinalizarConstrucao(GameObject predio)
{
    // Seu código de construção aqui...
    
    // O PredioRecursos já se ativa sozinho no Start()
    // Mas você pode forçar se quiser:
    PredioRecursos recursos = predio.GetComponent<PredioRecursos>();
    if (recursos != null && !recursos.estaProduzindo)
    {
        recursos.AtivarProducao();
    }
}
```

---

## 🛡️ Sistema de Danos

### **Desativar Produção ao Danificar**

```csharp
// No seu script de danos
void ReceberDano(float dano)
{
    vida -= dano;
    
    if (vida <= vidaMaxima * 0.25f) // Vida < 25%
    {
        // Desativa produção quando muito danificado
        PredioRecursos recursos = GetComponent<PredioRecursos>();
        if (recursos != null && recursos.estaProduzindo)
        {
            recursos.DesativarProducao();
            Debug.Log("⚠️ Prédio danificado! Produção interrompida.");
        }
    }
}
```

### **Reativar ao Reparar**

```csharp
void Reparar()
{
    vida = vidaMaxima;
    
    PredioRecursos recursos = GetComponent<PredioRecursos>();
    if (recursos != null && !recursos.estaProduzindo)
    {
        recursos.AtivarProducao();
        Debug.Log("✅ Prédio reparado! Produção retomada.");
    }
}
```

---

## 📈 Balanceamento Sugerido

### **Economia Inicial (Nível 1)**

| Prédio | Custo | Produção | Retorno |
|--------|-------|----------|---------|
| **Usina Petróleo** | $500 | +2⛽/s | 250s |
| **Refinaria Aço** | $800 | +3🔩/s | 267s |
| **Casa** | $200 | +1💰/s, +10👥 | 200s |
| **Banco** | $2000 | +20💰/s | 100s |
| **Usina Energia** | $1000 | +10⚡/s | 100s |

### **Custos Operacionais**

Alguns prédios devem consumir recursos:
- **Refinaria**: -0.5⛽/s (precisa de petróleo)
- **Fábrica**: -2⚡/s (precisa de energia)
- **Quartel**: -5💰/s (salários)

---

## 🎨 Efeitos Visuais

Adicione efeitos aos prédios produtivos:

```csharp
// No PredioRecursos, configure:
Efeito Producao: [Arraste prefab de partículas]
```

**Sugestões:**
- **Usina de Petróleo**: Fumaça preta saindo
- **Refinaria**: Faíscas de solda
- **Banco**: Símbolo $ brilhante
- **Fazenda**: Terra sendo cultivada

O efeito ativa automaticamente quando `estaProduzindo = true`!

---

## 🔍 Debug e Testes

### **Ver Produção na Scene View**

Quando você seleciona um prédio no Editor:
- **Ícone verde** aparece acima dele se está produzindo
- **Linha conecta** o prédio ao ícone

### **Console Logs**

O sistema mostra automaticamente:
```
✅ [Usina_Petroleo] Produção ativada! 💰+0/s | ⛽+2/s | 🔩+0/s | ⚡+0/s
⏸️ [Usina_Petroleo] Produção desativada!
💥 [Usina_Petroleo] Prédio destruído. Produção removida.
```

### **Comandos de Teste**

```csharp
// No Console do Unity (ou script de debug)
var recursos = FindObjectOfType<GerenciadorRecursos>();
Debug.Log($"Ganho total: {recursos.petroleoPorSegundo}/s petróleo");
```

---

## ⚠️ Problemas Comuns

### **❌ Prédio não produz nada**

**Soluções:**
1. Verificar se `GerenciadorRecursos` existe na cena
2. Checar se `AtivarAoCriar` está marcado ✅
3. Ver se `delayInicial` não é muito alto
4. Conferir se os valores de produção não são 0

### **❌ Produção continua após destruir prédio**

**Causa:** O `OnDestroy()` não foi chamado
**Solução:** Sempre use `Destroy(gameObject)` para destruir prédios

### **❌ HUD não atualiza cores**

**Causa:** Valores negativos pequenos (-0.1/s)
**Solução:** Use valores maiores ou ajuste limites no `PainelRecursos`

---

## 📚 Referência Rápida

### **Métodos Públicos do PredioRecursos**

```csharp
// Ativar manualmente
predio.AtivarProducao();

// Desativar temporariamente
predio.DesativarProducao();

// Aumentar produção (upgrade)
predio.AumentarProducao(1.5f); // 1.5x mais produção

// Verificar status
if (predio.estaProduzindo) { }
```

---

## 🎉 Exemplo Completo: Usina de Petróleo

### **No Prefab:**
1. Modelo 3D da usina
2. Collider
3. Script `UsinaPetroleo.cs`
4. Partículas de fumaça (opcional)

### **No Script:**
```csharp
// Já está pronto em UsinaPetroleo.cs!
Nível 1 = +2⛽/s
```

### **No Jogo:**
1. Jogador constrói a usina
2. Script detecta criação (Start)
3. Após 0s, ativa produção automaticamente
4. HUD atualiza: `⛽ 500 (+2/s)`
5. A cada segundo, +2 petróleo
6. Se destruída, remove o +2/s

---

✅ **Sistema 100% automático!** Você só precisa adicionar o componente ao prefab!

**Criado por:** Sistema de Recursos para Prédios v1.0  
**Compatível com:** GerenciadorRecursos v1.1+  
**Data:** 18/01/2026
