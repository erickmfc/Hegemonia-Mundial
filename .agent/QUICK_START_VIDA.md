# ⚡ Quick Start - Sistema de Vida

## 🚀 Implementação em 3 Passos

### 1️⃣ **Adicionar Vida em uma Unidade**
```
1. Selecione a unidade (helicóptero, tanque, soldado)
2. Add Component → CriarBarraDeVida
3. Pronto! ✅
```

### 2️⃣ **Testar o Sistema**
```
1. Adicione o componente TesteVida na mesma unidade
2. Rode o jogo
3. Pressione as teclas:
   - [1] = Dano pequeno (10 HP)
   - [2] = Dano médio (25 HP)  
   - [3] = Dano grande (50 HP)
   - [H] = Curar (30 HP)
```

### 3️⃣ **Ver a Barra de Vida**
Rode o jogo e observe:
- ✅ Barra verde aparece acima da unidade
- ✅ Barra muda de cor conforme perde vida (verde → amarelo → vermelho)
- ✅ Barra desaparece quando a vida chega a 0

---

## 📋 Checklist Rápido

**Para cada tipo de unidade:**

| Unidade        | Componentes              | Vida Maxima | Altura Barra |
|----------------|--------------------------|-------------|--------------|
| Helicóptero    | `CriarBarraDeVida`       | 100         | 3.5          |
| Tanque         | `CriarBarraDeVida`       | 300         | 2.0          |
| Soldado        | `CriarBarraDeVida`       | 50          | 2.5          |

**Tags necessárias:**
- ✅ Helicóptero: `Aereo`
- ✅ Tanque: `Inimigo` ou `Player`
- ✅ Soldado: `Inimigo` ou `Player`

---

## 🎯 Configuração de Armas

**No prefab do projétil/míssil:**
- Dano: `20` (ajuste conforme a arma)

**Exemplos:**
- Metralhadora: 15-25
- Míssil: 50-100
- Canhão: 150-200

---

## 🧪 Teste Completo

### Passo a Passo:
1. Crie uma cena de teste
2. Adicione um helicóptero com `CriarBarraDeVida` e `TesteVida`
3. Rode o jogo
4. Pressione `1`, `2`, `3` para aplicar dano
5. Observe a barra mudando de cor
6. Pressione `H` para curar
7. Continue aplicando dano até a vida chegar a 0
8. ✅ A unidade deve ser destruída

---

## 📖 Guia Completo

Veja `GUIA_SISTEMA_VIDA.md` para:
- Customização avançada
- Solução de problemas
- Recursos extras (cura, porcentagem, etc.)

---

✨ **Pronto para batalhas realistas!**
