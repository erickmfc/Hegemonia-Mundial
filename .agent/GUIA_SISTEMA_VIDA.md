# 🩸 Sistema de Vida e Dano - Guia de Implementação Completo

## 📦 O que foi criado:

### ✅ **Scripts Disponíveis:**

1. **`Vida.cs`** (já existia) - Sistema base de HP
2. **`BarraDeVida.cs`** (NOVO) - Barra visual em World Space
3. **`CriarBarraDeVida.cs`** (NOVO) - Criador automático de barras

---

## 🎯 Como Usar - Passo a Passo

### Método 1: **Configuração Manual** (Mais Controle)

#### Passo 1: Adicionar Sistema de Vida
1. Selecione uma unidade (helicóptero, tanque, soldado)
2. No **Inspector**, clique em **Add Component**
3. Procure por **"Vida"** e adicione
4. Configure no Inspector:
   - **Vida Maxima:** `100` (padrão)
   - **Efeito Dano:** Arraste um efeito de partículas (opcional)
   - **Efeito Morte:** Arraste uma explosão (opcional)

#### Passo 2: Adicionar Barra de Vida Automática
1. Com a mesma unidade selecionada
2. Clique em **Add Component**
3. Procure por **"CriarBarraDeVida"** e adicione
4. Configure:
   - **Altura:** `2.5` (altura da barra acima da cabeça)
   - **Criar Automaticamente:** ✅ Marcado

**Pronto!** Quando o jogo rodar, a barra aparecerá automaticamente.

---

### Método 2: **Super Rápido** (Recomendado)

Apenas adicione o componente **`CriarBarraDeVida`** na unidade.
Ele automaticamente:
- Detecta o componente `Vida` (ou o adiciona se não existir)
- Cria toda a estrutura da barra (Canvas + Background + Fill)
- Configura cores (Verde → Amarelo → Vermelho)

---

## 🎨 Customização da Barra de Vida

### Cores Dinâmicas:
A barra muda de cor automaticamente:
- **Verde** (> 60% de vida)
- **Amarelo** (30% - 60% de vida)
- **Vermelho** (< 30% de vida)

### Configurações da Barra (Inspector):
- **Altura Acima Da Unidade:** Distância da barra em relação ao chão
- **Esconder Se Vida Cheia:** Mostra apenas quando levar dano
- **Esconder Ao Morrer:** Esconde quando a unidade morre
- **Olhar Para Camera:** Sempre fica de frente para o jogador

---

## ⚔️ Sistema de Dano

### Armas que já usam o sistema:

✅ **Projétil.cs** - Balas de metralhadora (dano configurável)
✅ **MissilTeleguiado.cs** - Mísseis teleguiados (20 de dano padrão)

### Como configurar dano em armas:

```csharp
// No prefab do projétil ou míssil, no Inspector:
Dano: 20  // Quantidade de HP que remove
```

**Exemplos realistas:**
- Bala de fuzil: `10-15`
- Bala de metralhadora: `20-25`
- Míssil pequeno: `50`
- Míssil grande: `100`
- Canhão de tanque: `150-200`

---

## 🔧 Aplicar em Unidades Existentes

### Para Helicópteros:
1. Abra o prefab: `Assets/Prefabs/Helicoptero_ray/Helicoptero_Ray.prefab`
2. Adicione:
   - Componente **Vida** → Vida Maxima: `100`
   - Componente **CriarBarraDeVida** → Altura: `3.5` (helicópteros são altos)
3. Salve o prefab
4. **Tag:** Certifique-se que está marcado como `Aereo`

### Para Tanques:
1. Abra o prefab do tanque
2. Adicione:
   - Componente **Vida** → Vida Maxima: `300` (mais resistente)
   - Componente **CriarBarraDeVida** → Altura: `2.0`
3. **Tag:** `Inimigo` ou `Player` (dependendo do time)

### Para Soldados:
1. Abra o prefab do soldado
2. Adicione:
   - Componente **Vida** → Vida Maxima: `50` (mais fraco)
   - Componente **CriarBarraDeVida** → Altura: `2.5`
3. **Tag:** `Inimigo` ou `Player`

---

## 🧪 Testando o Sistema

### Teste Básico:
1. Coloque uma unidade com `Vida` na cena
2. Coloque outra unidade que atira (torreta, helicóptero inimigo)
3. Rode o jogo
4. **Observe:**
   - A barra de vida aparece acima da cabeça
   - Quando levar dano, a barra diminui e muda de cor
   - Quando a vida chega a 0, a unidade é destruída

### Console Debug:
Com o jogo rodando, abra o **Console** e veja:
```
✅ Barra de vida criada para Helicoptero_Ray!
Helicoptero_Ray recebeu 20 de dano! Vida: 80/100
💥💥💥 PROJÉTIL ATINGIU: Helicoptero_Ray
Helicoptero_Ray foi destruído!
```

---

## 📊 Valores de Vida Recomendados

| Unidade          | Vida Maxima | Raciocínio                      |
|------------------|-------------|---------------------------------|
| Soldado          | 50-100      | Frágil, morre com 2-5 tiros     |
| Tanque Leve      | 200-300     | Blindagem moderada              |
| Tanque Pesado    | 500-800     | Muito resistente                |
| Helicóptero      | 100-150     | Rápido mas vulnerável           |
| Avião            | 80-120      | Muito rápido, pouca armadura    |
| Prédio Pequeno   | 300-500     | Estrutura básica                |
| Prédio Grande    | 1000-2000   | Fortaleza                       |

---

## 🎮 Recursos Avançados

### 1. Curar Unidades:
```csharp
Vida vida = GetComponent<Vida>();
vida.Curar(50); // Recupera 50 HP
```

### 2. Verificar se está vivo:
```csharp
if (vida.EstaVivo())
{
    // Unidade ainda está ativa
}
```

### 3. Obter porcentagem de vida:
```csharp
float porcentagem = vida.PorcentagemVida(); // 0.0 a 1.0
if (porcentagem < 0.3f)
{
    Debug.Log("Vida crítica!");
}
```

---

## 🐛 Solução de Problemas

### **Problema:** Barra não aparece
**Solução:** 
- Verifique se a unidade tem o componente `Vida`
- Certifique-se que `CriarBarraDeVida` está com `Criar Automaticamente` marcado
- Rode o jogo (a barra é criada no Start)

### **Problema:** Barra não diminui quando leva dano
**Solução:**
- Verifique se a arma está chamando `vida.ReceberDano(quantidade)`
- Veja o Console para confirmar que o dano está sendo aplicado

### **Problema:** Barra está muito alta ou baixa
**Solução:**
- Ajuste o parâmetro `Altura` no componente `CriarBarraDeVida`

### **Problema:** Barra não olha para a câmera
**Solução:**
- No componente `BarraDeVida`, marque `Olhar Para Camera`
- Certifique-se que a cena tem uma **Main Camera**

---

## 📝 Estrutura da Barra (Hierarquia)

Quando criada automaticamente, a estrutura é:
```
Unidade (Helicoptero_Ray)
├─ Vida (script)
├─ CriarBarraDeVida (script)
└─ BarraDeVida (GameObject)
   ├─ Canvas (World Space)
   ├─ BarraDeVida (script)
   └─ Fundo (Image)
      └─ Preenchimento (Image - Fill)
```

---

## 🚀 Próximos Passos Sugeridos

1. **Adicionar efeitos visuais:**
   - Partículas de sangue/metal quando leva dano
   - Explosão quando morre
   
2. **Som:**
   - Som ao receber dano
   - Som de explosão ao morrer

3. **Animações:**
   - Animação de dano (sacudir)
   - Animação de morte (cair, explodir)

4. **Sistema de Equipes:**
   - Vida com cores diferentes para aliados/inimigos
   - Barra verde para aliados, vermelha para inimigos

---

✨ **Sistema completo e pronto para uso!**

**Última atualização:** 2025-12-29
