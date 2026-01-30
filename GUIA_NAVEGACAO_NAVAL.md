# 🚢 Sistema de Navegação Inteligente Naval

## 📋 Visão Geral

O sistema **NavegacaoInteligenteNaval.cs** implementa navegação inteligente para navios, similar ao sistema do **Navio de Vigilância no Liberty**. 

### ✨ Funcionalidades Principais

1. **🔄 Marcha à Ré Automática**: Se você clicar em lugares próximos mas **atrás do navio** (na popa), ele automaticamente vai **DE RÉ** ao invés de virar 180° e ir de frente.

2. **🧠 Decisão Inteligente**: O sistema analisa:
   - **Ângulo** do destino em relação à proa do navio
   - **Distância** até o destino
   - Decide automaticamente se usa marcha à frente ou à ré

3. **🎨 Feedback Visual**:
   - Rastro de água que ativa/desativa baseado na velocidade
   - Inclinação do navio nas curvas (banking effect)
   - Gizmos de debug coloridos (Verde = Frente, Vermelho = Ré)

---

## 🔧 Como Usar

### 1️⃣ Adicionar ao Seu Navio

1. Abra seu prefab de navio no Unity
2. Adicione o componente `NavegacaoInteligenteNaval`
3. Certifique-se que o navio tenha:
   - ✅ `NavMeshAgent` (obrigatório)
   - ✅ `TrailRenderer` para rastro de água (opcional)
   - ✅ Transform do modelo 3D (opcional, para inclinação)

### 2️⃣ Configurar Parâmetros

#### **Configurações de Navegação**

| Parâmetro | Valor Padrão | Descrição |
|-----------|--------------|-----------|
| `anguloParaMarchaRe` | 135° | Ângulo mínimo para ativar marcha à ré |
| `distanciaMaximaRe` | 20m | Distância máxima para usar ré (destinos longe sempre vão de frente) |
| `velocidadeRe` | 60% | Velocidade da marcha à ré (% da velocidade normal) |

#### **Configurações Visuais**

| Parâmetro | Descrição |
|-----------|-----------|
| `rastroAgua` | Arraste o TrailRenderer aqui |
| `modelo3D` | Arraste o modelo visual do navio |
| `forcaInclinacao` | Intensidade da inclinação nas curvas |

#### **Debug Visual**

| Parâmetro | Descrição |
|-----------|-----------|
| `mostrarDebugVisual` | Ativa/desativa gizmos de debug |
| `corSetaFrente` | Cor da seta quando indo de frente (Verde) |
| `corSetaRe` | Cor da seta quando em marcha à ré (Vermelho) |

---

## 🎮 Como Funciona

### Lógica de Decisão

```
1. Jogador clica em um destino
2. Sistema calcula:
   - Distância até o destino
   - Ângulo entre a PROA e o destino

3. Decisão:
   ┌─────────────────────────────────────┐
   │ Destino > 20m de distância?         │
   │ └─► SIM: Vai de FRENTE              │
   │                                      │
   │ Ângulo > 135° (está atrás)?         │
   │ └─► SIM: Vai de RÉ                  │
   │ └─► NÃO: Vai de FRENTE              │
   └─────────────────────────────────────┘
```

### Exemplo Prático

```
Navio está virado para o NORTE (↑)

Destino A: 10m ao NORTE (0°)
└─► Vai de FRENTE ✅

Destino B: 5m ao SUL (180°)  
└─► Vai de RÉ (marcha à ré) ⬅️

Destino C: 30m ao SUL (180°)
└─► Vai de FRENTE (muito longe para ré) ✅

Destino D: 8m a SUDOESTE (135°)
└─► Vai de RÉ (na zona de marcha à ré) ⬅️
```

---

## 🎨 Visualização de Debug

### No Scene View (quando selecionado):

- **Cone Amarelo**: Área de detecção de marcha à ré
  - Se clicar dentro desse cone = Marcha à ré
  - Se clicar fora = Marcha à frente

### Durante o Jogo:

- **Linha Verde**: Indo de frente
- **Linha Vermelha**: Indo de ré
- **Esfera no destino**: Mostra onde você clicou
- **Seta na frente/trás**: Mostra direção do movimento

---

## 🔄 Comparação com Sistema Antigo

| Característica | **MovimentoNaval.cs** (Antigo) | **NavegacaoInteligenteNaval.cs** (Novo) |
|----------------|--------------------------------|-------------------------------------------|
| Marcha à Ré | ❌ Não | ✅ Sim |
| Decisão Inteligente | ❌ Não | ✅ Automática |
| Rastro de Água | ✅ Sim | ✅ Sim (melhorado) |
| Inclinação | ✅ Sim | ✅ Sim (funciona em ré também) |
| Debug Visual | ❌ Não | ✅ Sim (completo) |

---

## 🛠️ Integração com Outros Scripts

### Com `ControladorNavioVigilante.cs`

```csharp
// ANTES: MovimentoNaval.cs apenas adiciona efeitos visuais
// AGORA: NavegacaoInteligenteNaval.cs substitui completamente

// Você pode usar os dois! 
// ControladorNavioVigilante.cs = Combate
// NavegacaoInteligenteNaval.cs = Navegação
```

### Com `MovimentoInteligente.cs`

```csharp
// MovimentoInteligente.cs = Para tropas terrestres
// NavegacaoInteligenteNaval.cs = Para navios

// Não use os dois no mesmo objeto!
```

---

## 📝 Código de Exemplo

### Mover Navio via Script

```csharp
// Pegue a referência
NavegacaoInteligenteNaval navegacao = GetComponent<NavegacaoInteligenteNaval>();

// Defina um destino
Vector3 destino = new Vector3(100, 0, 50);
navegacao.DefinirDestino(destino);

// Verifique se está em marcha à ré
if (navegacao.EstaEmMarchaRe())
{
    Debug.Log("Navio indo de RÉ!");
}
```

---

## ⚠️ Notas Importantes

1. **NavMesh Obrigatório**: O navio precisa estar em uma área com NavMesh configurada
2. **Água em Y=0**: O sistema assume que a água está em Y=0
3. **Distância Máxima**: Destinos muito longe SEMPRE vão de frente (por realismo)
4. **Performance**: O sistema é leve, pode usar em vários navios simultaneamente

---

## 🎯 Ajustes Recomendados por Tipo de Navio

### Navio Pequeno (Patrulha)
```
anguloParaMarchaRe: 120°
distanciaMaximaRe: 15m
velocidadeRe: 70%
```

### Navio Médio (Fragata)
```
anguloParaMarchaRe: 135°
distanciaMaximaRe: 20m
velocidadeRe: 60%
```

### Navio Grande (Destroyer)
```
anguloParaMarchaRe: 150°
distanciaMaximaRe: 25m
velocidadeRe: 40%
```

---

## 🐛 Troubleshooting

### O navio não vai de ré

**Possíveis causas:**
1. Destino muito longe (> distanciaMaximaRe)
2. Ângulo muito pequeno (< anguloParaMarchaRe)
3. NavMeshAgent desabilitado

**Solução:**
- Ative `mostrarDebugVisual = true`
- Observe os gizmos no Scene view
- Ajuste os parâmetros conforme necessário

### O navio gira infinitamente

**Causa:** Velocidade muito alta + stopping distance muito pequena

**Solução:**
```
NavMeshAgent.stoppingDistance = 2.0f; // Aumentar
NavMeshAgent.angularSpeed = 120; // Diminuir
```

---

## 📚 Referências

- Baseado no sistema do **Navio de Vigilância** do jogo **Liberty**
- Usa `NavMeshAgent` do Unity para pathfinding
- Implementa física naval simplificada

---

**Criado por:** Sistema de IA Antigravity  
**Versão:** 1.0  
**Data:** Janeiro 2026
