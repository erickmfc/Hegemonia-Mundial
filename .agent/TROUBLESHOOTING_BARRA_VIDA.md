# 🔧 Barra de Vida - Solução de Problemas

## ⚠️ Problema: "Barra não aparece"

### ✅ SOLUÇÕES APLICADAS AUTOMATICAMENTE:

1. **Barra agora SEMPRE visível** (mesmo com vida cheia)
2. **Barra 2X MAIOR** (150x20 pixels ao invés de 100x10)
3. **Texto MAIOR e em NEGRITO** (18px ao invés de 14px)
4. **Altura aumentada** (3.5m ao invés de 2.5m)

---

## 🎯 Como Usar (Após Atualização):

### Passo 1: Adicionar na Unidade
```
1. Selecione o navio/helicóptero/tanque
2. Add Component → CriarBarraDeVida
3. Salve a cena
```

### Passo 2: Rodar o Jogo
- ✅ A barra aparece IMEDIATAMENTE (verde, "100/100")
- ✅ É visível mesmo de longe
- ✅ Texto grande e legível

---

## 🧪 Teste Rápido:

### Verificar se a barra foi criada:
1. Rode o jogo
2. Selecione a unidade na Hierarchy
3. Expanda a hierarquia
4. Deve ver: `BarraDeVida` como filho

### Se ainda não aparecer:
```
1. Pause o jogo
2. Selecione a unidade
3. Veja no Inspector se tem:
   - ✅ Vida (Script)
   - ✅ CriarBarraDeVida (Script)
   - ✅ BarraDeVida (GameObject filho)
```

---

## 🔍 Checklist de Diagnóstico:

### ✅ Componentes necessários:
- [ ] `Vida` (script)
- [ ] `CriarBarraDeVida` (script)
- [ ] Criar Automaticamente = ✅ (marcado)

### ✅ Durante o jogo (Runtime):
- [ ] GameObject `BarraDeVida` foi criado (filho da unidade)
- [ ] Canvas está ativo
- [ ] Texto está visível

### ✅ Console (F5 no editor):
Procure por mensagens:
```
✅ Barra de vida criada para [Nome da Unidade]!
✅ Barra de vida configurada para [Nome da Unidade]
```

---

## 🐛 Se AINDA não aparecer:

### Opção 1: Recriar a barra
```
1. Selecione a unidade
2. Remova o componente CriarBarraDeVida
3. Adicione novamente
4. Rode o jogo
```

### Opção 2: Verificar a câmera
```
1. Certifique-se que tem uma Main Camera na cena
2. Tag: MainCamera
```

### Opção 3: Verificar distância
```
1. Aproxime a câmera da unidade
2. Rotacione a câmera para ver de cima
```

---

## 📊 Configurações Padrão (Novas):

| Parâmetro | Valor Antigo | Valor NOVO |
|-----------|--------------|------------|
| Tamanho Barra | 100x10 | **150x20** |
| Escala | 0.01 | **0.02** (2x) |
| Altura | 2.5m | **3.5m** |
| Tamanho Texto | 14px | **18px** |
| Estilo Texto | Normal | **Negrito** |
| Sempre Visível | Não | **SIM** |

---

## 🎮 Para Unidades Específicas:

### Navio/Corveta (como na imagem):
```
Altura recomendada: 4.0 - 5.0m
Razão: Navios são grandes, barra precisa ficar bem acima
```

### Helicóptero:
```
Altura: 3.5m (padrão está OK)
```

### Tanque:
```
Altura: 3.0m
```

### Soldado:
```
Altura: 2.5m
```

---

## ✨ Resultado Esperado:

Quando rodar o jogo, você deve ver:

```
        [ 100/100 ]    ← Texto branco GRANDE em negrito
      ███████████      ← Barra verde GRANDE
         🚢            ← Sua unidade
```

**SEMPRE VISÍVEL**, mesmo com vida cheia!

---

## 💡 Dica Extra:

Para testar rapidamente:
1. Adicione também `TesteVida` na unidade
2. Rode o jogo
3. Pressione `[1]` para aplicar dano
4. Observe a barra mudando

---

**Última atualização:** 2025-12-29
**Status:** Barra otimizada para máxima visibilidade
