# 🎨 DIAGRAMA VISUAL - Sistema de Navegação Naval

## 📐 Zona de Marcha à Ré

```
                    FRENTE DO NAVIO
                          ▲
                          │
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        │                 │                 │
        │      ZONA       │      ZONA       │
        │     NORMAL      │     NORMAL      │
        │   (Vai Frente)  │   (Vai Frente)  │
        │                 │                 │
45°     │                 🚢                 │  45°
────────┼─────────────────┴─────────────────┼────────
        │                                   │
        │         ZONA DE MARCHA RÉ         │
        │         (135° ~ 225°)             │
        │                                   │
        │    ⬅️  Se clicar aqui, vai de RÉ  │
        │                                   │
        │      Mas só se distância < 20m!   │
        │                                   │
        │    Se > 20m, SEMPRE vai frente    │
        │                                   │
        └───────────────────────────────────┘
                    POPA DO NAVIO
```

---

## 🎯 Exemplos Visuais de Cliques

### Cenário 1: Clique Próximo Atrás (Ré Ativa)

```
         N (Norte)
         ▲
         │
    ─────┼─────
         │
         🚢  ← Navio olhando Norte
         │
         │ 10m
         │
         ❌  ← Destino (180°, 10m)

Resultado: 🔴 MARCHA À RÉ
Motivo: 180° > 135° E 10m < 20m
```

### Cenário 2: Clique Próximo Frente (Normal)

```
         ❌  ← Destino (0°, 10m)
         │
         │ 10m
         │
         🚢  ← Navio
         │
    ─────┼─────
         │
         ▼

Resultado: 🟢 MARCHA À FRENTE
Motivo: 0° < 135° (está à frente)
```

### Cenário 3: Clique Longe Atrás (Normal)

```
         N
         ▲
         │
    ─────┼─────
         │
         🚢  ← Navio
         │
         │
         │ 30m
         │
         │
         ❌  ← Destino (180°, 30m)

Resultado: 🟢 MARCHA À FRENTE
Motivo: 180° > 135° MAS 30m > 20m (muito longe)
```

### Cenário 4: Clique Diagonal (No Limite)

```
              N
              ▲
              │
    ──────────┼──────────
              │
              🚢  ← Navio
             ╱
            ╱ 15m
           ╱
          ╱ 135°
         ❌  ← Destino

Resultado: 🔴 MARCHA À RÉ (no limite)
Motivo: 135° = 135° (limite) E 15m < 20m
```

---

## 🔄 Fluxograma de Decisão

```
┌─────────────────────────────┐
│  Jogador clica em destino   │
└──────────┬──────────────────┘
           │
           ▼
┌─────────────────────────────┐
│  Calcular distância e ângulo│
└──────────┬──────────────────┘
           │
           ▼
       ╔═══════════════╗
       ║ Distância     ║
       ║ > 20m ?       ║
       ╚═══════════════╝
           │
    Sim ───┤
           │
           ▼
    ┌─────────────┐
    │ Vai FRENTE  │
    │     🟢      │
    └─────────────┘
           │
    Não ───┤
           │
           ▼
       ╔═══════════════╗
       ║ Ângulo        ║
       ║ > 135° ?      ║
       ╚═══════════════╝
           │
    Sim ───┼─── Não
           │         │
           ▼         ▼
    ┌─────────┐  ┌─────────┐
    │ Vai RÉ  │  │Vai FRENTE│
    │   🔴    │  │   🟢     │
    └─────────┘  └──────────┘
```

---

## 🚢 Movimento em Marcha à Ré

### Passo a Passo:

```
FRAME 1: Detecta destino atrás
┌─────────────────┐
│       ❌        │  Destino
│                 │
│                 │
│       ⬆️        │
│     🚢 (0°)     │  Navio olhando Norte
└─────────────────┘

FRAME 2: Rotaciona para dar costas
┌─────────────────┐
│       ❌        │
│                 │
│      ↻ ↺        │  Girando...
│       ⬇️        │
│     🚢 (180°)   │  Agora olha Sul
└─────────────────┘

FRAME 3: Move "para trás" (que é em direção ao destino)
┌─────────────────┐
│       ❌        │
│        ▲        │
│        │        │  Movendo de ré
│     🚢 ⬇️       │  (60% velocidade)
│                 │
└─────────────────┘

FRAME 4: Chegou!
┌─────────────────┐
│     🚢⬇️❌      │  Parou de costas
│                 │  para o destino
│                 │
│                 │
└─────────────────┘
```

---

## 📊 Tabela de Parâmetros vs Resultado

| Ângulo | Distância | Resultado | Emoji |
|--------|-----------|-----------|-------|
| 0°     | 10m       | Frente    | 🟢    |
| 45°    | 10m       | Frente    | 🟢    |
| 90°    | 10m       | Frente    | 🟢    |
| 120°   | 10m       | Frente    | 🟢    |
| 135°   | 10m       | **Ré**    | 🔴    |
| 150°   | 10m       | **Ré**    | 🔴    |
| 180°   | 10m       | **Ré**    | 🔴    |
| 180°   | 15m       | **Ré**    | 🔴    |
| 180°   | 20m       | **Ré**    | 🔴    |
| 180°   | 25m       | Frente    | 🟢    |
| 180°   | 50m       | Frente    | 🟢    |

---

## 🎮 Interface de Debug no Jogo

```
┌────────────────────────────────────┐
│  SCENE VIEW (Editor)               │
├────────────────────────────────────┤
│                                    │
│         🟨 Cone Amarelo            │
│        /    (Zona Ré)   \          │
│       /                   \        │
│      /         🚢          \       │
│     /                       \      │
│    └─────────────────────────┘     │
│                                    │
│  Se clicar DENTRO do cone = Ré     │
│  Se clicar FORA = Frente           │
│                                    │
└────────────────────────────────────┘

┌────────────────────────────────────┐
│  GAME VIEW (Play Mode)             │
├────────────────────────────────────┤
│                                    │
│        ❌ (Destino)                │
│         │                          │
│         │ Linha 🔴 = Ré            │
│         │ Linha 🟢 = Frente        │
│         │                          │
│        🚢                           │
│         ➡️ Seta = Direção          │
│                                    │
└────────────────────────────────────┘
```

---

## 🔧 Hierarquia do GameObject

```
NavioVigilante (Prefab)
│
├── 📜 NavMeshAgent
│   ├─ Speed: 5
│   ├─ Angular Speed: 90
│   └─ Stopping Distance: 2
│
├── 📜 ControleUnidade
│   └─ [Gerencia seleção]
│
├── 📜 NavegacaoInteligenteNaval ⭐
│   ├─ anguloParaMarchaRe: 135°
│   ├─ distanciaMaximaRe: 20m
│   ├─ velocidadeRe: 0.6
│   ├─ rastroAgua: → Rastro_Agua
│   ├─ modelo3D: → Modelo3D
│   └─ mostrarDebugVisual: ✅
│
├── 📦 Modelo3D (Transform filho)
│   └── 🎨 Mesh do navio
│
└── 💨 Rastro_Agua (Transform filho)
    └── TrailRenderer
        ├─ Width: 0.5
        ├─ Time: 2.0
        └─ Color: Branco → Transparente
```

---

## 🎯 Linha do Tempo de Execução

```
T=0.00s │ Jogador clica direito
        │
T=0.01s │ GerenteSelecao detecta clique
        │ ├─ Faz Raycast
        │ └─ Pega posição (X, Y, Z)
        │
T=0.02s │ Chama ControleUnidade.MoverParaPonto()
        │ ├─ Detecta NavegacaoInteligenteNaval
        │ └─ Chama navegacao.DefinirDestino()
        │
T=0.03s │ NavegacaoInteligenteNaval.DefinirDestino()
        │ ├─ Calcula distância: 12m
        │ ├─ Calcula ângulo: 170°
        │ └─ Decisão: MARCHA RÉ! 🔴
        │
T=0.04s │ NavegacaoInteligenteNaval.Update()
        │ └─ ExecutarMarchaRe()
        │     ├─ Rotaciona navio (180°)
        │     ├─ Move "para trás"
        │     └─ Velocidade = 3m/s (60% de 5m/s)
        │
T=4.00s │ Chegou no destino!
        │ └─ Para movimento
        │ └─ emMarchaRe = false
```

---

## 📏 Geometria do Sistema

### Cálculo do Ângulo:

```
Vector3.Angle(transform.forward, direcaoDestino)

      transform.forward
            ↑
            │ \
            │  \  ← ângulo calculado
            │   \
       🚢   │    \
            │     ❌ destino
            │    /
            │   / direcaoDestino
            │  /
            │ /
```

### Cone de Ativação (Top View):

```
                 FRENTE
                   ▲
                   │
          ╱────────┼────────╲
         ╱         │         ╲
45°     ╱          🚢          ╲  45°
       ╱                       ╲
      ╱     ZONA SEGURA         ╲
     ╱      (Vai frente)         ╲
    ╱                             ╲
   ╱_______________________________╲
  │                                 │ 135°
  │                                 │
  │       ZONA DE MARCHA RÉ         │
  │       (Vai de ré se < 20m)      │
  │                                 │
  └─────────────────────────────────┘
                 TRÁS
```

---

## 🧮 Fórmulas Usadas

### 1. Cálculo de Distância
```csharp
float distancia = Vector3.Distance(posicaoNavio, destino);
// Exemplo: (0,0,0) até (0,0,10) = 10 metros
```

### 2. Cálculo de Ângulo
```csharp
float angulo = Vector3.Angle(navio.forward, direcaoDestino);
// Retorna 0° ~ 180°
// 0° = Totalmente à frente
// 90° = Lateral
// 180° = Totalmente atrás
```

### 3. Decisão de Marcha
```csharp
bool marcharRe = (angulo >= 135f) && (distancia <= 20f);
```

### 4. Velocidade em Ré
```csharp
velocidadeRe = velocidadeOriginal * 0.6f;
// Exemplo: 5 m/s → 3 m/s
```

---

## 🎨 Feedback Visual

### Estados do Sistema:

| Estado | Cor Linha | Velocidade | Glyph |
|--------|-----------|------------|-------|
| Parado | Nenhuma | 0 m/s | 🛑 |
| Frente | 🟢 Verde | 5 m/s | ➡️ |
| Ré | 🔴 Vermelho | 3 m/s | ⬅️ |

### Rastro de Água:

```
PARADO:   [sem rastro]

FRENTE:   🚢 ~~~~~~~~~  (rastro branco)

RÉ:       🚢 ~~~~~~~~~  (mesmo rastro)
           ↓
         (movendo para trás)
```

---

**🎓 Agora você entende visualmente como funciona!**

Use os diagramas deste arquivo para:
- ✅ Explicar o sistema para a equipe
- ✅ Debugar problemas
- ✅ Ajustar parâmetros
- ✅ Criar tutoriais

🚢 Boa navegação! ⚓
