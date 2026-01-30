# 🚢 Configuração Rápida - Navegação Naval Inteligente

## ⚡ Setup em 3 Passos

### 1️⃣ Preparar o Prefab do Navio

Abra seu prefab de navio (ex: `NavioVigilante.prefab`) e certifique-se que tem:

```
NavioVigilante (GameObject)
├── NavMeshAgent (Component) ✅
├── ControleUnidade (Component) ✅
├── IdentidadeUnidade (Component) ✅
├── NavegacaoInteligenteNaval (Component) ⬅️ ADICIONAR ESTE!
│
├── Modelo3D (GameObject filho)
│   └── Mesh do navio
│
└── Rastro_Agua (GameObject filho)
    └── TrailRenderer
```

### 2️⃣ Configurar NavegacaoInteligenteNaval

Clique no navio e configure o script:

#### **Navegação (Valores Recomendados)**
```
Angulo Para Marcha Re: 135°
Distancia Maxima Re: 20m
Velocidade Re: 0.6 (60% da velocidade normal)
```

#### **Visual**
```
Rastro Agua: Arraste o filho "Rastro_Agua" aqui
Modelo 3D: Arraste o filho "Modelo3D" aqui
Forca Inclinacao: 3
```

#### **Debug**
```
Mostrar Debug Visual: ✅ (marcar para ver os gizmos)
Cor Seta Frente: Verde
Cor Seta Re: Vermelho
```

### 3️⃣ Ajustar NavMeshAgent

Configure o NavMeshAgent do navio:

```
Speed: 5 (velocidade base)
Angular Speed: 90 (rotação médio-rápida)
Acceleration: 4
Stopping Distance: 2
Auto Braking: ✅
```

---

## 🎮 Como Testar

1. **Inicie o jogo**
2. **Selecione o navio** (clique esquerdo)
3. **Clique direito ATRÁS do navio** (na popa)
   - ✅ Se a distância < 20m e ângulo > 135°: Vai de RÉ! 🔴
   - ✅ Se não: Vai de frente normal 🟢

### Cenários de Teste

```
Posição do Navio: (0, 0, 0) olhando para NORTE (0°)

TESTE 1: Clique em (0, 0, -10) - 180° atrás, 10m
└─► RESULTADO: Marcha à RÉ ⬅️

TESTE 2: Clique em (0, 0, 10) - 0° frente, 10m  
└─► RESULTADO: Marcha à frente ➡️

TESTE 3: Clique em (0, 0, -30) - 180° atrás, 30m
└─► RESULTADO: Marcha à frente (muito longe) ➡️

TESTE 4: Clique em (-7, 0, -7) - 135° sudoeste, 10m
└─► RESULTADO: Marcha à RÉ (no limite) ⬅️
```

---

## 🔍 Debug Visual

Com `Mostrar Debug Visual = true`, você verá:

### Durante o Jogo:
- **Linha Verde**: Navio indo de frente
- **Linha Vermelha**: Navio em marcha à ré
- **Esfera Branca**: Destino clicado
- **Seta**: Direção do movimento

### No Scene View (navio selecionado):
- **Cone Amarelo Atrás**: Zona de ativação da marcha à ré
  - Qualquer clique DENTRO desse cone = Marcha à ré
  - Qualquer clique FORA = Frente

---

## 🛠️ Troubleshooting

### ❌ Navio não vai de ré

**Possíveis causas:**
1. Destino muito longe (> `distanciaMaximaRe`)
2. Ângulo muito pequeno (< `anguloParaMarchaRe`)
3. Script `NavegacaoInteligenteNaval` não adicionado

**Solução:**
- Ative `Mostrar Debug Visual`
- Veja o cone amarelo no Scene view
- Clique DENTRO do cone
- Reduza `distanciaMaximaRe` se necessário

### ❌ Navio gira infinitamente

**Causa:** Velocidade de rotação + velocidade de movimento incompatíveis

**Solução:**
```
NavMeshAgent.angularSpeed = 90 (reduzir)
NavMeshAgent.speed = 5 (reduzir)
velocidadeRe = 0.5 (reduzir)
```

### ❌ Log mostra "usando sistema naval inteligente" mas nada acontece

**Causa:** NavMeshAgent está bloqueado

**Solução:**
```csharp
// Verifique se o NavMeshAgent está ativo:
agente.enabled = true;
agente.isStopped = false;
```

---

## 📊 Comparação de Configurações

### Navio Rápido (Patrulha)
```
NavMeshAgent.speed = 8
anguloParaMarchaRe = 120°
distanciaMaximaRe = 15m
velocidadeRe = 0.7
```

### Navio Médio (Fragata)  
```
NavMeshAgent.speed = 5
anguloParaMarchaRe = 135°
distanciaMaximaRe = 20m
velocidadeRe = 0.6
```

### Navio Pesado (Destroyer)
```
NavMeshAgent.speed = 3
anguloParaMarchaRe = 150°
distanciaMaximaRe = 25m
velocidadeRe = 0.4
```

---

## 🎯 Dicas Avançadas

### 1. Combinar com ControladorNavioVigilante

```csharp
// Ambos scripts podem coexistir!
// NavegacaoInteligenteNaval = Movimento
// ControladorNavioVigilante = Combate

GameObject navio = ...;
navio.AddComponent<NavegacaoInteligenteNaval>();
navio.AddComponent<ControladorNavioVigilante>();
```

### 2. Sons de Marcha à Ré

```csharp
// Adicione ao NavegacaoInteligenteNaval.cs:
public AudioClip somMarchaRe;

// No método ExecutarMarchaRe():
if (!somTocando)
{
    AudioSource.PlayOneShot(somMarchaRe);
    somTocando = true;
}
```

### 3. Partículas de Espuma

```csharp
public ParticleSystem espumaRe; // Espuma na popa

// No Update():
if (emMarchaRe && !espumaRe.isPlaying)
    espumaRe.Play();
else if (!emMarchaRe && espumaRe.isPlaying)
    espumaRe.Stop();
```

---

## ✅ Checklist Final

Antes de salvar o prefab, confirme:

- [ ] NavMeshAgent configurado
- [ ] NavegacaoInteligenteNaval adicionado
- [ ] ControleUnidade presente
- [ ] Rastro_Agua referenciado
- [ ] Modelo3D referenciado
- [ ] Debug visual ativado (para testes)
- [ ] Testado em jogo (ré funciona)

---

## 📚 Próximos Passos

Depois de configurar o sistema básico:

1. **Ajuste fino dos parâmetros** para cada tipo de navio
2. **Adicione efeitos visuais/sonoros** para marcha à ré
3. **Crie variantes** (navio lento, navio rápido, etc)
4. **Teste em mapas complexos** com obstáculos

---

**Bem-vindo ao sistema de navegação naval do Liberty! 🚢⚓**
