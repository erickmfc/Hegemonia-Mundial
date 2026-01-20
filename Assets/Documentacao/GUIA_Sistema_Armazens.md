# 📦 Guia Completo: Sistema de Armazéns

## 🎯 Visão Geral

Sistema de armazenamento de recursos com dois tipos de galpões:
- **🏭 Armazém de Recursos** - Alimentos, água, petróleo, minerais, metal, energia
- **🎖️ Armazém Militar** - Munição, mísseis, explosivos, equipamento, blindagem

### ✨ Características:
- ✅ **ScriptableObjects** para armazenar dados persistentes
- ✅ **Conexão automática** com HUD de recursos
- ✅ **Transferência automática** de produção para armazéns
- ✅ **Preparado para mercado internacional**
- ✅ **Preparado para menu de recursos**
- ✅ **Galpões físicos** com visuais no jogo

---

## 🚀 Setup Inicial (5 Passos)

### **Passo 1: Criar os ScriptableObjects (Dados)**

#### A) Armazém de Recursos:
1. No Unity, clique com botão direito em `Assets/Armazens/`
2. **Create** → **Hegemonia** → **Armazéns** → **Armazém de Recursos**
3. Renomeie para: `Dados_Armazem_Recursos_Principal`
4. Configure no Inspector:
   ```
   Capacidade Máxima: 10000
   Alimentos Máximo: 5000
   Água Máximo: 5000
   Petróleo Máximo: 3000
   Minerais Máximo: 2000
   Metal Máximo: 2000
   Energia Máximo: 1000
   ```

#### B) Armazém Militar:
1. No mesmo local: **Create** → **Hegemonia** → **Armazéns** → **Armazém Militar**
2. Renomeie para: `Dados_Armazem_Militar_Principal`
3. Configure:
   ```
   Capacidade Máxima: 5000
   Munição Leve Máximo: 10000
   Munição Pesada Máximo: 1000
   Mísseis Máximo: 100
   Explosivos Máximo: 500
   Equipamento Máximo: 1000
   Blindagem Máximo: 200
   Nível Segurança: 5
   ```

---

### **Passo 2: Criar o Gerenciador**

1. Hierarchy → **Create Empty**
2. Renomear para: `GerenciadorArmazens`
3. **Add Component** → `GerenciadorArmazens`
4. No Inspector, arraste os ScriptableObjects:
   - **Armazem Recursos**: `Dados_Armazem_Recursos_Principal`
   - **Armazem Militar**: `Dados_Armazem_Militar_Principal`
5. Configure:
   ```
   Intervalo Transferência: 5 (segundos)
   ```

---

### **Passo 3: Adicionar Scripts aos Galpões Físicos**

#### A) Galpão de Recursos:
1. Selecione o **prefab do armazém de recursos**
2. **Add Component** → `GalpaoRecursos`
3. Configure:
   - **Dados Armazem**: `Dados_Armazem_Recursos_Principal`
   - **Nome Galpao**: "Armazém Central"

#### B) Galpão Militar:
1. Selecione o **prefab do armazém militar**
2. **Add Component** → `GalpaoMilitar`
3. Configure:
   - **Dados Armazem Militar**: `Dados_Armazem_Militar_Principal`
   - **Nome Galpao**: "Arsenal Militar"
   - **Nivel Seguranca**: 5

---

### **Passo 4: Testar**

1. **Pressione Play**
2. Abra a **Console**
3. Você deve ver:
   ```
   ✅ [GalpaoRecursos] Galpão de Recursos ativado
   ✅ [GalpaoMilitar] Galpão Militar ativado
   ```
4. A cada 5 segundos, a produção de petróleo/aço/energia será transferida para os armazéns!

---

### **Passo 5: Verificar Dados**

1. No **Project**, clique no ScriptableObject `Dados_Armazem_Recursos_Principal`
2. Com o jogo **rodando**, veja os valores mudando em tempo real!
3. **Petróleo**, **Metal**, **Energia** devem aumentar automaticamente

---

## 💻 Como Usar no Código

### **Consultar Recursos Armazenados**

```csharp
// Pegar quantidade de petróleo armazenado
int petroleo = GerenciadorArmazens.Instancia.ConsultarRecursoCivil(TipoRecurso.Petroleo);
Debug.Log($"Petróleo em estoque: {petroleo}");

// Pegar quantidade de mísseis
int misseis = GerenciadorArmazens.Instancia.ConsultarRecursoMilitar(TipoRecursoMilitar.Misseis);
Debug.Log($"Mísseis disponíveis: {misseis}");
```

---

### **Adicionar Recursos (Compra/Produção)**

```csharp
// Adicionar 100 unidades de alimentos
bool sucesso = GerenciadorArmazens.Instancia.AdicionarRecursoCivil(TipoRecurso.Alimentos, 100);

if (sucesso)
{
    Debug.Log("Alimentos adicionados!");
}
else
{
    Debug.Log("Armazém cheio!");
}

// Adicionar 50 mísseis
GerenciadorArmazens.Instancia.AdicionarRecursoMilitar(TipoRecursoMilitar.Misseis, 50);
```

---

### **Remover Recursos (Venda/Consumo)**

```csharp
// Retirar 50 unidades de água
bool disponivel = GerenciadorArmazens.Instancia.RemoverRecursoCivil(TipoRecurso.Agua, 50);

if (disponivel)
{
    Debug.Log("Água removida!");
}
else
{
    Debug.Log("Água insuficiente!");
}

// Retirar munição para equipar tropas
GerenciadorArmazens.Instancia.RemoverRecursoMilitar(TipoRecursoMilitar.MunicaoLeve, 300);
```

---

### **Transação Internacional (Compra/Venda)**

```csharp
// Comprar 500 de petróleo por $1000
bool comprou = GerenciadorArmazens.Instancia.ExecutarTransacaoInternacional(
    TipoRecurso.Petroleo,  // Recurso
    500,                    // Quantidade
    1000,                   // Preço
    true                    // true = compra, false = venda
);

// Vender 200 de metal por $800
bool vendeu = GerenciadorArmazens.Instancia.ExecutarTransacaoInternacional(
    TipoRecurso.Metal,
    200,
    800,
    false  // Venda
);
```

---

###**Obter Relatório Completo**

```csharp
// Mostra todos os recursos armazenados
string relatorio = GerenciadorArmazens.Instancia.ObterRelatorioCompleto();
Debug.Log(relatorio);
```

**Saída:**
```
=== RELATÓRIO DE ARMAZÉNS ===

📦 ARMAZÉM DE RECURSOS:
Ocupação: 45.2%
🌾 Alimentos: 1000/5000
💧 Água: 800/5000
⛽ Petróleo: 1200/3000
💎 Minerais: 500/2000
🔩 Metal: 600/2000
⚡ Energia: 300/1000

🎖️ ARMAZÉM MILITAR:
Ocupação: 20.5%
🔫 Munição Leve: 5000/10000
...
```

---

## 🔗 Conexão com HUD

### **Transferência Automática de Produção**

O `GerenciadorArmazens` automaticamente transfere a produção do `GerenciadorRecursos` para os armazéns:

```
A cada 5 segundos:
  ├─ Petróleo produzido → Armazém de Recursos
  ├─ Metal produzido → Armazém de Recursos
  └─ Energia produzida → Armazém de Recursos (baterias)
```

**Configurável em:**
```csharp
GerenciadorArmazens.intervaloTransferencia = 10f; // Mudar para 10s
```

---

### **Eventos**

```csharp
// Inscrever em eventos
GerenciadorArmazens.Instancia.OnArmazensAtualizados += QuandoAtualizar;
GerenciadorArmazens.Instancia.OnArmazemCheio += QuandoCheio;

void QuandoAtualizar()
{
    Debug.Log("Armazéns atualizados!");
    // Atualizar UI aqui
}

void QuandoCheio(string recurso)
{
    Debug.LogWarning($"Armazém de {recurso} está cheio!");
    // Mostrar notificação para jogador
}
```

---

## 🌍 Preparação para Mercado Internacional

O sistema já está preparado para o futuro mercado:

### **Estrutura de Venda:**
```csharp
public struct RecursoParaVenda
{
    public TipoRecurso tipo;
    public int quantidadeDisponivel;
    public int precoUnitario;
}
```

### **Quando criar o Mercado:**

1. Implementar UI de mercado
2. Chamar `ExecutarTransacaoInternacional()` ao clicar em comprar/vender
3. O sistema JÁ debita/credita dinheiro e adiciona/remove recursos!

---

## 📊 Preparação para Menu de Recursos

### **Dados Persistentes:**

Como são **ScriptableObjects**, os dados salvam entre sessões de Edit Mode!

```csharp
// NO FUTURO MENU DE RECURSOS:
public void AtualizarMenuRecursos()
{
    var dados = GerenciadorArmazens.Instancia.armazemRecursos;
    
    textoAlimentos.text = $"{dados.alimentos}/{dados.alimentosMaximo}";
    textoAgua.text = $"{dados.agua}/{dados.aguaMaximo}";
    textoPetroleo.text = $"{dados.petroleo}/{dados.petroleoMaximo}";
    
    // Barra de progresso
    barraAlimentos.fillAmount = (float)dados.alimentos / dados.alimentosMaximo;
}
```

---

## 🎨 Visual nos Galpões

### **Texto 3D de Capacidade:**

```csharp
// Já implementado em GalpaoRecursos/GalpaoMilitar
// Mostra "45%" acima do galpão
```

### **Luzes de Segurança (Militar):**

```csharp
// Verde = Vazio
// Amarelo = 50%
// Vermelho = Cheio
```

### **Efeito Visual ao Receber Recursos:**

1. Adicione partículas ao galpão (opcional)
2. Arraste para `Efeito Armazenamento` no Inspector
3. Ativa automaticamente quando recebe recursos!

---

## 🛠️ Arquivos Criados

```
Assets/scripts/Armazens/
├── DadosArmazemRecursos.cs      ← ScriptableObject (recursos civis)
├── DadosArmazemMilitar.cs       ← ScriptableObject (recursos militares)
├── GerenciadorArmazens.cs       ← Gerenciador central
├── GalpaoRecursos.cs            ← Script para galpão físico
└── GalpaoMilitar.cs             ← Script para galpão militar

Assets/Armazens/ (criar esta pasta)
├── Dados_Armazem_Recursos_Principal.asset
└── Dados_Armazem_Militar_Principal.asset
```

---

## ⚠️ Dicas Importantes

### **1. Criar a Pasta Armazens:**
```
Assets → Create → Folder → "Armazens"
```

### **2. Múltiplos Armazéns:**
Você pode criar vários ScriptableObjects:
```
Dados_Armazem_Norte.asset
Dados_Armazem_Sul.asset
Dados_Armazem_Leste.asset
```

Cada galpão físico pode apontar para um ScriptableObject diferente!

### **3. Salvar/Persistent:**
ScriptableObjects salvam durante Edit Mode, mas NÃO entre Play sessions.
Para salvar no jogo, você precisará:
- Sistema de Save/Load (implementar depois)
- PlayerPrefs (temporário)
- JSON (recomendado)

---

## ✅ Checklist de Implementação

```
[ ] Criar pasta "Armazens" em Assets
[ ] Criar ScriptableObject Dados_Armazem_Recursos_Principal
[ ] Criar ScriptableObject Dados_Armazem_Militar_Principal
[ ] Criar GameObject "GerenciadorArmazens" na cena
[ ] Adicionar script GerenciadorArmazens
[ ] Arrastar ScriptableObjects para o Gerenciador
[ ] Adicionar GalpaoRecursos ao prefab do armazém civil
[ ] Adicionar GalpaoMilitar ao prefab do armazém militar
[ ] Arrastar ScriptableObjects para os galpões
[ ] Pressionar Play e testar
[ ] Verificar Console para confirmação
[ ] Abrir ScriptableObjects e ver valores mudando
```

---

**Criado por:** Sistema de Armazéns v1.0  
**Compatível com:** GerenciadorRecursos v1.1+  
**Data:** 18/01/2026
