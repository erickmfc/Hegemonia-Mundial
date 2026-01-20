# 🏗️ Guia: Adicionar Armazéns ao Menu de Construção

## 📋 Objetivo

Fazer com que os **Galpão de Recursos** e **Galpão Militar** apareçam no menu de construção (tecla "C") para poderem ser construídos no jogo.

---

## 🚀 Passo a Passo

### **Etapa 1: Criar as Fichas de Construção (ScriptableObjects)**

#### A) **Ficha do Armazém de Recursos** 📦

1. No Unity, vá para a pasta onde ficam suas construções:
   ```
   Assets/Construcoes/ (ou onde você salva as fichas)
   ```

2. **Click direito** → **Create** → **Hegemonia** → **Ficha de Construcao**

3. Renomeie para:
   ```
   Ficha_Armazem_Recursos
   ```

4. **Selecione** a ficha e configure no **Inspector**:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📝 INFORMAÇÕES BÁSICAS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Nome Item: "Armazém de Recursos"

Descrição: 
"Armazém para estocar recursos civis.
Capacidade: 10,000 unidades.
Armazena: Alimentos, Água, Petróleo,
Minerais, Metal e Energia."

Ícone: [Arraste uma imagem de armazém]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔧 TÉCNICO
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Prefab Da Unidade: 
[Arraste o PREFAB do armazém de recursos]
(O modelo 3D azul da pasta Prefabs)

Preço: 1000

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📂 CLASSIFICAÇÃO
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Categoria: Infraestrutura

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🎮 COMPORTAMENTOS E MENU
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Scripts De Comando: 
(Deixar vazio - armazéns não têm comandos)
Size: 0
```

---

#### B) **Ficha do Armazém Militar** 🎖️

1. **Click direito** → **Create** → **Hegemonia** → **Ficha de Construcao**

2. Renomeie para:
   ```
   Ficha_Armazem_Militar
   ```

3. Configure no **Inspector**:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📝 INFORMAÇÕES BÁSICAS
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Nome Item: "Arsenal Militar"

Descrição:
"Armazém militar seguro.
Capacidade: 5,000 unidades.
Armazena: Munição, Mísseis, Explosivos,
Equipamento e Blindagem."

Ícone: [Arraste uma imagem de arsenal]

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔧 TÉCNICO
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Prefab Da Unidade:
[Arraste o PREFAB do armazém militar]
(O modelo 3D azul da pasta Prefabs)

Preço: 1500

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📂 CLASSIFICAÇÃO
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Categoria: Tecnologia

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🎮 COMPORTAMENTOS E MENU
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Scripts De Comando:
(Deixar vazio)
Size: 0
```

---

### **Etapa 2: Preparar os Prefabs**

#### **IMPORTANTE:** Os prefabs precisam ter os scripts corretos!

#### A) **Prefab do Armazém de Recursos:**

1. Abra o **prefab** do armazém de recursos
2. Certifique-se que tem:
   - ✅ `GalpaoRecursos.cs` (já adicionado)
   - ✅ Collider (BoxCollider, MeshCollider, etc)
   - ✅ Modelo 3D visível

3. Configure o componente `GalpaoRecursos`:
   ```
   Dados Armazem: [Arraste Dados_Armazem_Recursos_Principal]
   Nome Galpao: "Armazém Central"
   Ativo: ✅
   ```

4. **Apply** no prefab

---

#### B) **Prefab do Armazém Militar:**

1. Abra o **prefab** do armazém militar
2. Certifique-se que tem:
   - ✅ `GalpaoMilitar.cs` (já adicionado)
   - ✅ Collider
   - ✅ Modelo 3D visível

3. Configure o componente `GalpaoMilitar`:
   ```
   Dados Armazem Militar: [Arraste Dados_Armazem_Militar_Principal]
   Nome Galpao: "Arsenal Militar"
   Ativo: ✅
   Nivel Seguranca: 5
   ```

4. **Apply** no prefab

---

### **Etapa 3: Adicionar ao Menu de Construção**

Agora você precisa adicionar essas fichas ao seu **MenuConstrucao** (ou sistema de menu que você usa).

#### **Se você tem um script de menu:**

```csharp
// No seu MenuConstrucao.cs ou equivalente
public List<DadosConstrucao> construcoesDisponiveis = new List<DadosConstrucao>();
```

1. Selecione o GameObject do menu na cena
2. No Inspector, procure a lista `Construcoes Disponiveis`
3. Aumente o **Size** em +2
4. Arraste as fichas:
   - `Ficha_Armazem_Recursos`
   - `Ficha_Armazem_Militar`

---

### **Etapa 4: Integração com o Sistema de Armazéns**

Para que os armazéns funcionem corretamente quando construídos:

#### **Opção A: Automática (Recomendada)**

Se você já tem o `GerenciadorArmazens` na cena, ele vai detectar os ScriptableObjects automaticamente!

#### **Opção B: Manual**

Adicione este código ao seu script de construção:

```csharp
void FinalizarConstrucao(GameObject predio)
{
    // Seu código existente...
    
    // Verifica se é um galpão
    GalpaoRecursos galpaoRecursos = predio.GetComponent<GalpaoRecursos>();
    if (galpaoRecursos != null)
    {
        Debug.Log("✅ Armazém de Recursos construído!");
        // Já se conecta automaticamente ao GerenciadorArmazens
    }
    
    GalpaoMilitar galpaoMilitar = predio.GetComponent<GalpaoMilitar>();
    if (galpaoMilitar != null)
    {
        Debug.Log("✅ Armazém Militar construído!");
        // Já se conecta automaticamente ao GerenciadorArmazens
    }
}
```

---

## ✅ Checklist de Verificação

```
FICHAS DE CONSTRUÇÃO:
[ ] Ficha_Armazem_Recursos criada
[ ] Ficha_Armazem_Militar criada
[ ] Ambas com nome, descrição e ícone
[ ] Ambas com preço definido
[ ] Ambas com categoria selecionada

PREFABS:
[ ] Prefab armazém recursos tem GalpaoRecursos.cs
[ ] Prefab armazém militar tem GalpaoMilitar.cs
[ ] Ambos apontam para os ScriptableObjects corretos
[ ] Ambos têm Collider
[ ] Ambos têm modelo 3D visível

MENU:
[ ] Fichas adicionadas à lista do menu
[ ] Menu aparece com tecla "C"
[ ] Armazéns aparecem na categoria correta

TESTE:
[ ] Pressionar C no jogo
[ ] Ver armazéns no menu
[ ] Construir armazém de recursos
[ ] Construir armazém militar
[ ] Verificar Console para confirmação
```

---

## 🎮 Como Testar

1. **Pressione Play** ▶️
2. **Pressione C** para abrir menu de construção
3. Procure a aba **"Infraestrutura"** → Verá o Armazém de Recursos
4. Procure a aba **"Tecnologia"** → Verá o Arsenal Militar
5. **Clique** em um deles
6. **Clique** no mapa para construir
7. Observe o **Console**:
   ```
   ✅ [Galpão_Recursos] Galpão de Recursos ativado: Armazém Central
   ```

---

## 📊 Estrutura Final

```
Assets/
├── Construcoes/ (ou sua pasta de fichas)
│   ├── Ficha_Armazem_Recursos.asset      ✅ NOVO
│   └── Ficha_Armazem_Militar.asset       ✅ NOVO
│
├── Prefabs/
│   ├── Armazem_Recursos.prefab           
│   │   └── Componente: GalpaoRecursos    ✅
│   └── Armazem_Militar.prefab
│       └── Componente: GalpaoMilitar     ✅
│
└── Armazens/ (ScriptableObjects de dados)
    ├── Dados_Armazem_Recursos_Principal.asset
    └── Dados_Armazem_Militar_Principal.asset
```

---

## 💡 Dicas Importantes

### **1. Ícones Personalizados:**

Crie ou use imagens para os ícones:
- **Armazém de Recursos**: Imagem de um galpão civil
- **Arsenal Militar**: Imagem de um bunker/arsenal

Tamanho recomendado: **128x128** ou **256x256**

---

### **2. Preços Sugeridos:**

```
Armazém de Recursos: $1,000 - $2,000
Arsenal Militar: $1,500 - $3,000
```

O militar deve ser mais caro por ser estratégico!

---

### **3. Categorias:**

- **Armazém de Recursos** → `Infraestrutura`
- **Arsenal Militar** → `Tecnologia` ou `Infraestrutura`

---

### **4. Múltiplos Armazéns:**

Você pode construir vários! Cada um vai:
- Usar o **mesmo** ScriptableObject de dados
- Contribuir para a capacidade total
- Atualizar o HUD automaticamente

---

## ⚠️ Problemas Comuns

### ❌ "Armazém não aparece no menu"
**Solução:** Verifique se a ficha foi adicionada na lista `construcoesDisponiveis` do menu

### ❌ "Construí mas não funciona"
**Solução:** Certifique-se que o prefab tem o script `GalpaoRecursos` ou `GalpaoMilitar`

### ❌ "ScriptableObject está null"
**Solução:** Arraste o `Dados_Armazem_...` para o campo no componente do galpão

---

## 🎯 Resultado Final

Quando tudo estiver configurado:

1. **Tecla C** → Abre menu
2. **Infraestrutura** → "Armazém de Recursos" ($1000)
3. **Tecnologia** → "Arsenal Militar" ($1500)
4. **Clicar** → Modo fantasma
5. **Clicar no mapa** → Constrói!
6. **Automático** → Conecta ao sistema de recursos ✅

---

**Criado por:** Guia de Integração de Armazéns v1.0  
**Data:** 18/01/2026
